using MediaSet.Api.Infrastructure.Lookup.Models;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace MediaSet.Api.Infrastructure.Lookup.Clients.Igdb;

public class IgdbTokenService : IIgdbTokenService
{
    private readonly IgdbConfiguration _configuration;
    private readonly IMemoryCache _cache;
    private readonly ILogger<IgdbTokenService> _logger;
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private const string CacheKey = "igdb_access_token";

    public IgdbTokenService(
        IOptions<IgdbConfiguration> configuration,
        IMemoryCache cache,
        ILogger<IgdbTokenService> logger)
    {
        _configuration = configuration.Value;
        _cache = cache;
        _logger = logger;
    }

    public async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken)
    {
        if (_cache.TryGetValue(CacheKey, out string? cachedToken) && cachedToken != null)
        {
            return cachedToken;
        }

        await _semaphore.WaitAsync(cancellationToken);
        try
        {
            // Double-check after acquiring semaphore
            if (_cache.TryGetValue(CacheKey, out cachedToken) && cachedToken != null)
            {
                return cachedToken;
            }

            _logger.LogInformation("Fetching new IGDB access token");

            using var httpClient = new HttpClient();
            var requestContent = new FormUrlEncodedContent(
            [
                new KeyValuePair<string, string>("client_id", _configuration.ClientId),
                new KeyValuePair<string, string>("client_secret", _configuration.ClientSecret),
                new KeyValuePair<string, string>("grant_type", "client_credentials")
            ]);

            var response = await httpClient.PostAsync(_configuration.TokenUrl, requestContent, cancellationToken);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            var tokenResponse = JsonSerializer.Deserialize<IgdbTokenResponse>(content)
                ?? throw new InvalidOperationException("Failed to deserialize IGDB token response");

            var ttl = TimeSpan.FromSeconds(Math.Max(tokenResponse.ExpiresIn - 60, 60));
            _cache.Set(CacheKey, tokenResponse.AccessToken, ttl);

            _logger.LogInformation("IGDB access token obtained, expires in {ExpiresIn}s", tokenResponse.ExpiresIn);
            return tokenResponse.AccessToken;
        }
        finally
        {
            _semaphore.Release();
        }
    }
}
