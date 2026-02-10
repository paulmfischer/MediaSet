using MediaSet.Api.Infrastructure.Lookup.Models;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Serilog;
using SerilogTracing;

namespace MediaSet.Api.Infrastructure.Lookup.Clients.UpcItemDb;

public class UpcItemDbClient : IUpcItemDbClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<UpcItemDbClient> _logger;
    private readonly UpcItemDbConfiguration _configuration;

    // Thread-safe request serialization (following MusicBrainzClient pattern)
    private static readonly SemaphoreSlim _rateLimiter = new(1, 1);
    private static DateTime _lastRequestTime = DateTime.MinValue;

    // Per-minute tracking
    private static int _requestsInCurrentMinute = 0;
    private static DateTime _currentMinuteStart = DateTime.MinValue;

    // Daily tracking
    private static int _requestsToday = 0;
    private static DateOnly _currentDay = DateOnly.MinValue;

    // Burst limit cooldown
    private static DateTime? _burstLimitResetTime = null;

    public UpcItemDbClient(
        HttpClient httpClient,
        IOptions<UpcItemDbConfiguration> configuration,
        ILogger<UpcItemDbClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _configuration = configuration.Value;
    }

    public async Task<UpcItemResponse?> GetItemByCodeAsync(string code, CancellationToken cancellationToken)
    {
        await _rateLimiter.WaitAsync(cancellationToken);
        try
        {
            using var activity = Log.Logger.StartActivity("UpcItemDb.GetItemByCode", new { code });

            // Check if we're in a burst limit cooldown period
            if (_burstLimitResetTime.HasValue && DateTime.UtcNow < _burstLimitResetTime.Value)
            {
                var waitDuration = _burstLimitResetTime.Value - DateTime.UtcNow;
                _logger.LogInformation("Still in burst limit cooldown for {Code}, waiting {Seconds} seconds",
                    code, waitDuration.TotalSeconds);
                await Task.Delay(waitDuration, cancellationToken);
                _burstLimitResetTime = null;
            }

            // Apply proactive throttling
            await ApplyProactiveThrottlingAsync(cancellationToken);

            _logger.LogInformation("Looking up UPC/EAN code: {Code}", code);

            var response = await _httpClient.GetAsync($"prod/trial/lookup?upc={code}", cancellationToken);

            // Parse and log rate limit headers
            var rateLimitInfo = ParseRateLimitHeaders(response.Headers);
            LogRateLimitHeaders(code, rateLimitInfo);

            if (!response.IsSuccessStatusCode)
            {
                if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                {
                    return await HandleRateLimitErrorAsync(code, response.Headers, cancellationToken);
                }

                _logger.LogWarning("UPCitemdb returned status code {StatusCode} for code: {Code}",
                    response.StatusCode, code);
                return null;
            }

            // Track successful request
            TrackRequest();

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            var result = JsonSerializer.Deserialize<UpcItemResponse>(content, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            _logger.LogInformation("Successfully retrieved UPC/EAN data for code: {Code}, found {Count} items",
                code, result?.Items.Count ?? 0);

            return result;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error while looking up UPC/EAN code: {Code}", code);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error looking up UPC/EAN code: {Code}", code);
            return null;
        }
        finally
        {
            _rateLimiter.Release();
        }
    }

    private async Task ApplyProactiveThrottlingAsync(CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var today = DateOnly.FromDateTime(now);

        // Reset minute counter if we're in a new minute
        if (_currentMinuteStart == DateTime.MinValue || now - _currentMinuteStart >= TimeSpan.FromMinutes(1))
        {
            _requestsInCurrentMinute = 0;
            _currentMinuteStart = now;
        }

        // Reset daily counter if we're in a new day
        if (_currentDay == DateOnly.MinValue || today > _currentDay)
        {
            _requestsToday = 0;
            _currentDay = today;
        }

        // Check if we've hit the daily limit
        if (_requestsToday >= _configuration.MaxRequestsPerDay)
        {
            _logger.LogError("Daily rate limit of {Limit} requests reached, cannot make more requests today",
                _configuration.MaxRequestsPerDay);
            throw new InvalidOperationException(
                $"UpcItemDb daily rate limit of {_configuration.MaxRequestsPerDay} requests exceeded");
        }

        // Pause if we're approaching the per-minute limit
        if (_requestsInCurrentMinute >= _configuration.MaxRequestsPerMinute)
        {
            var timeIntoCurrentMinute = now - _currentMinuteStart;
            var waitTime = TimeSpan.FromMinutes(1) - timeIntoCurrentMinute;

            if (waitTime > TimeSpan.Zero)
            {
                _logger.LogWarning(
                    "Approaching per-minute rate limit ({Current}/{Max}), pausing for {Seconds} seconds until next minute",
                    _requestsInCurrentMinute, _configuration.MaxRequestsPerMinute, waitTime.TotalSeconds);
                await Task.Delay(waitTime, cancellationToken);

                // Reset minute counter after waiting
                _requestsInCurrentMinute = 0;
                _currentMinuteStart = DateTime.UtcNow;
            }
        }

        // Enforce minimum delay between requests
        if (_lastRequestTime != DateTime.MinValue && _configuration.MinDelayBetweenRequestsMs > 0)
        {
            var timeSinceLastRequest = now - _lastRequestTime;
            var minDelay = TimeSpan.FromMilliseconds(_configuration.MinDelayBetweenRequestsMs);

            if (timeSinceLastRequest < minDelay)
            {
                var delay = minDelay - timeSinceLastRequest;
                _logger.LogDebug("Enforcing minimum delay between requests: {Milliseconds}ms", delay.TotalMilliseconds);
                await Task.Delay(delay, cancellationToken);
            }
        }
    }

    private RateLimitInfo ParseRateLimitHeaders(HttpResponseHeaders headers)
    {
        return new RateLimitInfo
        {
            Limit = GetHeaderValue(headers, "X-RateLimit-Limit"),
            Remaining = GetHeaderValue(headers, "X-RateLimit-Remaining"),
            Reset = GetHeaderValue(headers, "X-RateLimit-Reset"),
            Current = GetHeaderValue(headers, "X-RateLimit-Current")
        };
    }

    private void LogRateLimitHeaders(string code, RateLimitInfo rateLimitInfo)
    {
        _logger.LogInformation(
            "UpcItemDb rate limit status for {Code}: Limit={Limit}, Remaining={Remaining}, Reset={Reset}, Current={Current}, LocalMinute={LocalMinute}, LocalDay={LocalDay}",
            code,
            rateLimitInfo.Limit ?? "null",
            rateLimitInfo.Remaining ?? "null",
            rateLimitInfo.Reset ?? "null",
            rateLimitInfo.Current ?? "null",
            _requestsInCurrentMinute,
            _requestsToday);
    }

    private async Task<UpcItemResponse?> HandleRateLimitErrorAsync(
        string code,
        HttpResponseHeaders headers,
        CancellationToken cancellationToken)
    {
        var resetHeader = GetHeaderValue(headers, "X-RateLimit-Reset");

        if (string.IsNullOrEmpty(resetHeader) || !long.TryParse(resetHeader, out var resetUnixTime))
        {
            _logger.LogWarning("UPCitemdb rate limit exceeded for code: {Code} but no valid reset time provided", code);
            return null;
        }

        var resetTime = DateTimeOffset.FromUnixTimeSeconds(resetUnixTime).UtcDateTime;
        var pauseDuration = resetTime - DateTime.UtcNow;

        // If pause duration is within acceptable range, this is a burst limit - pause and retry
        if (pauseDuration.TotalSeconds > 0 && pauseDuration.TotalSeconds <= _configuration.MaxRetryPauseSeconds)
        {
            _logger.LogWarning(
                "UpcItemDb burst rate limit hit for {Code}, pausing for {Seconds} seconds until {ResetTime}",
                code, pauseDuration.TotalSeconds, resetTime);

            await PauseForRateLimitAsync("BurstLimit", pauseDuration, cancellationToken);

            // Set burst limit reset time so concurrent requests also wait
            _burstLimitResetTime = resetTime;

            // Reset minute counter after burst limit pause
            _requestsInCurrentMinute = 0;
            _currentMinuteStart = DateTime.UtcNow;

            _logger.LogInformation("Retrying request for {Code} after burst rate limit pause", code);

            // Retry the request once
            var retryResponse = await _httpClient.GetAsync($"prod/trial/lookup?upc={code}", cancellationToken);

            if (!retryResponse.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Retry after burst rate limit pause failed with status {StatusCode} for code: {Code}",
                    retryResponse.StatusCode, code);
                return null;
            }

            // Track successful retry
            TrackRequest();

            var rateLimitInfo = ParseRateLimitHeaders(retryResponse.Headers);
            LogRateLimitHeaders(code, rateLimitInfo);

            var content = await retryResponse.Content.ReadAsStringAsync(cancellationToken);
            var result = JsonSerializer.Deserialize<UpcItemResponse>(content, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            _logger.LogInformation(
                "Successfully retrieved UPC/EAN data after retry for code: {Code}, found {Count} items",
                code, result?.Items.Count ?? 0);

            return result;
        }

        // Daily limit exceeded - don't retry
        var hoursUntilReset = pauseDuration.TotalHours;
        _logger.LogError(
            "UpcItemDb daily rate limit exceeded for {Code}, cannot retry (reset in {Hours} hours)",
            code, hoursUntilReset);
        return null;
    }

    private async Task PauseForRateLimitAsync(string reason, TimeSpan duration, CancellationToken cancellationToken)
    {
        using var activity = Log.Logger.StartActivity("UpcItemDb.RateLimitPause", new
        {
            Reason = reason,
            DurationSeconds = duration.TotalSeconds,
            RequestsInCurrentMinute = _requestsInCurrentMinute,
            RequestsToday = _requestsToday
        });

        _logger.LogWarning(
            "Pausing UpcItemDb requests for {Seconds} seconds due to {Reason}",
            duration.TotalSeconds, reason);

        await Task.Delay(duration, cancellationToken);

        _logger.LogInformation("UpcItemDb rate limit pause complete");
    }

    private void TrackRequest()
    {
        _requestsInCurrentMinute++;
        _requestsToday++;
        _lastRequestTime = DateTime.UtcNow;
    }

    private string? GetHeaderValue(HttpResponseHeaders headers, string headerName)
    {
        if (headers.TryGetValues(headerName, out var values))
        {
            return values.FirstOrDefault();
        }
        return null;
    }

    private record RateLimitInfo
    {
        public string? Limit { get; init; }
        public string? Remaining { get; init; }
        public string? Reset { get; init; }
        public string? Current { get; init; }
    }
}
