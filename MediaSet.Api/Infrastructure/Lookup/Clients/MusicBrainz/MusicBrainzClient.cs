using MediaSet.Api.Features.Lookup.Models;
using MediaSet.Api.Features.Entities.Models;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace MediaSet.Api.Infrastructure.Lookup;

public class MusicBrainzClient : IMusicBrainzClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<MusicBrainzClient> _logger;
    private readonly MusicBrainzConfiguration _configuration;
    private static readonly SemaphoreSlim _rateLimiter = new(1, 1);
    private static DateTime _lastRequestTime = DateTime.MinValue;

    public MusicBrainzClient(
        HttpClient httpClient,
        IOptions<MusicBrainzConfiguration> configuration,
        ILogger<MusicBrainzClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _configuration = configuration.Value;
    }

    public async Task<MusicBrainzRelease?> GetReleaseByBarcodeAsync(string barcode, CancellationToken cancellationToken)
    {
        await _rateLimiter.WaitAsync(cancellationToken);
        try
        {
            // Ensure at least 1 second between requests
            var timeSinceLastRequest = DateTime.UtcNow - _lastRequestTime;
            if (timeSinceLastRequest < TimeSpan.FromSeconds(1))
            {
                await Task.Delay(TimeSpan.FromSeconds(1) - timeSinceLastRequest, cancellationToken);
            }

            _logger.LogInformation("Looking up music release by barcode: {Barcode}", barcode);

            var response = await _httpClient.GetAsync(
                $"ws/2/release/?query=barcode:{barcode}&fmt=json",
                cancellationToken);

            _lastRequestTime = DateTime.UtcNow;

            if (!response.IsSuccessStatusCode)
            {
                if (response.StatusCode == System.Net.HttpStatusCode.ServiceUnavailable)
                {
                    _logger.LogWarning("MusicBrainz rate limit exceeded for barcode: {Barcode}", barcode);
                    throw new HttpRequestException("MusicBrainz rate limit exceeded", null, response.StatusCode);
                }

                _logger.LogWarning("MusicBrainz returned status code {StatusCode} for barcode: {Barcode}",
                    response.StatusCode, barcode);
                return null;
            }

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            var result = JsonSerializer.Deserialize<MusicBrainzSearchResponse>(content,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (result?.Releases == null || result.Releases.Count == 0)
            {
                _logger.LogInformation("No releases found for barcode: {Barcode}", barcode);
                return null;
            }

            // Get the first release (best match)
            var release = result.Releases[0];

            _logger.LogInformation("Successfully retrieved release for barcode: {Barcode}, Title: {Title}",
                barcode, release.Title);

            return release;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error while looking up release for barcode: {Barcode}", barcode);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error looking up release for barcode: {Barcode}", barcode);
            return null;
        }
        finally
        {
            _rateLimiter.Release();
        }
    }

    public async Task<MusicBrainzRelease?> GetReleaseByIdAsync(string releaseId, CancellationToken cancellationToken)
    {
        await _rateLimiter.WaitAsync(cancellationToken);
        try
        {
            // Ensure at least 1 second between requests
            var timeSinceLastRequest = DateTime.UtcNow - _lastRequestTime;
            if (timeSinceLastRequest < TimeSpan.FromSeconds(1))
            {
                await Task.Delay(TimeSpan.FromSeconds(1) - timeSinceLastRequest, cancellationToken);
            }

            _logger.LogInformation("Looking up music release by ID: {ReleaseId}", releaseId);

            var response = await _httpClient.GetAsync(
                $"ws/2/release/{releaseId}?inc=artist-credits+labels+recordings+tags&fmt=json",
                cancellationToken);

            _lastRequestTime = DateTime.UtcNow;

            if (!response.IsSuccessStatusCode)
            {
                if (response.StatusCode == System.Net.HttpStatusCode.ServiceUnavailable)
                {
                    _logger.LogWarning("MusicBrainz rate limit exceeded for release ID: {ReleaseId}", releaseId);
                    throw new HttpRequestException("MusicBrainz rate limit exceeded", null, response.StatusCode);
                }

                _logger.LogWarning("MusicBrainz returned status code {StatusCode} for release ID: {ReleaseId}",
                    response.StatusCode, releaseId);
                return null;
            }

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            var release = JsonSerializer.Deserialize<MusicBrainzRelease>(content,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (release == null)
            {
                _logger.LogInformation("No release found for release ID: {ReleaseId}", releaseId);
                return null;
            }

            _logger.LogInformation("Successfully retrieved release for ID: {ReleaseId}, Title: {Title}",
                releaseId, release.Title);

            return release;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error while looking up release for ID: {ReleaseId}", releaseId);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error looking up release for ID: {ReleaseId}", releaseId);
            return null;
        }
        finally
        {
            _rateLimiter.Release();
        }
    }
}
