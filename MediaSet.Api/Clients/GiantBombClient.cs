using System.Text.Json;
using System.Web;
using MediaSet.Api.Models;
using Microsoft.Extensions.Options;

namespace MediaSet.Api.Clients;

public class GiantBombClient : IGiantBombClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<GiantBombClient> _logger;
    private readonly GiantBombConfiguration _configuration;

    public GiantBombClient(
        HttpClient httpClient,
        IOptions<GiantBombConfiguration> configuration,
        ILogger<GiantBombClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _configuration = configuration.Value;

        _httpClient.BaseAddress = new Uri(_configuration.BaseUrl);
        _httpClient.Timeout = TimeSpan.FromSeconds(_configuration.Timeout);
        _httpClient.DefaultRequestHeaders.Add("Accept", "application/json");
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "MediaSet/1.0 (GiantBomb)");
    }

    public async Task<List<GiantBombSearchResult>?> SearchGameAsync(string title, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Searching GiantBomb for game: {Title}", title);

            var encodedTitle = HttpUtility.UrlEncode(title);
            var url = $"search/?api_key={_configuration.ApiKey}&format=json&resources=game&query={encodedTitle}";
            var response = await _httpClient.GetAsync(url, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                {
                    _logger.LogWarning("GiantBomb rate limit exceeded for game search: {Title}", title);
                    throw new HttpRequestException("GiantBomb rate limit exceeded", null, response.StatusCode);
                }

                _logger.LogWarning("GiantBomb search returned status code {StatusCode} for title: {Title}",
                    response.StatusCode, title);
                return null;
            }

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            var result = JsonSerializer.Deserialize<GiantBombSearchResponse>(content, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (result == null || result.StatusCode != 1)
            {
                _logger.LogWarning("GiantBomb search error: {Error}", result?.Error ?? "unknown error");
                return null;
            }

            _logger.LogInformation("GiantBomb search found {Count} results for game: {Title}",
                result.Results.Count, title);

            return result.Results;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error while searching GiantBomb for game: {Title}", title);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching GiantBomb for game: {Title}", title);
            return null;
        }
    }

    public async Task<GiantBombGameDetails?> GetGameDetailsAsync(string apiDetailUrlOrGuid, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Getting GiantBomb game details from: {Path}", apiDetailUrlOrGuid);

            // apiDetailUrlOrGuid might be a full URL or a GUID; handle both
            string relativePath = apiDetailUrlOrGuid;
            if (Uri.IsWellFormedUriString(apiDetailUrlOrGuid, UriKind.Absolute))
            {
                var uri = new Uri(apiDetailUrlOrGuid);
                relativePath = uri.PathAndQuery.TrimStart('/');
                
                // Remove "api/" prefix since BaseAddress already includes it
                if (relativePath.StartsWith("api/", StringComparison.OrdinalIgnoreCase))
                {
                    relativePath = relativePath.Substring(4);
                }
                
                // Ensure trailing slash for GiantBomb API
                if (!relativePath.EndsWith('/'))
                {
                    relativePath += '/';
                }
            }
            else if (!apiDetailUrlOrGuid.StartsWith("game/", StringComparison.OrdinalIgnoreCase))
            {
                relativePath = $"game/{apiDetailUrlOrGuid}/";
            }
            else if (!apiDetailUrlOrGuid.EndsWith('/'))
            {
                // Ensure trailing slash if already starts with game/
                relativePath = $"{apiDetailUrlOrGuid}/";
            }

            var url = $"{relativePath}?api_key={_configuration.ApiKey}&format=json";
            var response = await _httpClient.GetAsync(url, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                {
                    _logger.LogWarning("GiantBomb rate limit exceeded for game details: {Path}", apiDetailUrlOrGuid);
                    throw new HttpRequestException("GiantBomb rate limit exceeded", null, response.StatusCode);
                }

                _logger.LogWarning("GiantBomb details returned status code {StatusCode} for: {Path}",
                    response.StatusCode, apiDetailUrlOrGuid);
                return null;
            }

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            var result = JsonSerializer.Deserialize<GiantBombDetailsResponse>(content, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (result == null || result.StatusCode != 1)
            {
                _logger.LogWarning("GiantBomb details error: {Error}", result?.Error ?? "unknown error");
                return null;
            }

            return result.Results;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error while getting GiantBomb game details from: {Path}", apiDetailUrlOrGuid);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting GiantBomb game details from: {Path}", apiDetailUrlOrGuid);
            return null;
        }
    }
}
