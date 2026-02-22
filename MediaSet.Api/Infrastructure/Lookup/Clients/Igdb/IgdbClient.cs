using MediaSet.Api.Infrastructure.Lookup.Models;
using Microsoft.Extensions.Options;
using System.Text;

namespace MediaSet.Api.Infrastructure.Lookup.Clients.Igdb;

public class IgdbClient : IIgdbClient
{
    private readonly HttpClient _httpClient;
    private readonly IIgdbTokenService _tokenService;
    private readonly IgdbConfiguration _configuration;
    private readonly ILogger<IgdbClient> _logger;

    public IgdbClient(
        HttpClient httpClient,
        IIgdbTokenService tokenService,
        IOptions<IgdbConfiguration> configuration,
        ILogger<IgdbClient> logger)
    {
        _httpClient = httpClient;
        _tokenService = tokenService;
        _configuration = configuration.Value;
        _logger = logger;
    }

    public async Task<List<IgdbGame>?> SearchGameAsync(string title, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Searching IGDB for game: {Title}", title);

            var escapedTitle = title.Replace("\"", "\\\"");
            var body = $"fields id,name,summary,first_release_date,genres.name,involved_companies.company.name,involved_companies.developer,involved_companies.publisher,platforms.name,platforms.abbreviation,age_ratings.category,age_ratings.rating,cover.url; search \"{escapedTitle}\"; limit 10;";
            var response = await SendRequestAsync("games", body, cancellationToken);

            if (response == null)
            {
                return null;
            }

            var results = await response.Content.ReadFromJsonAsync<List<IgdbGame>>(cancellationToken);

            _logger.LogInformation("IGDB search found {Count} results for game: {Title}",
                results?.Count ?? 0, title);

            return results;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error while searching IGDB for game: {Title}", title);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching IGDB for game: {Title}", title);
            return null;
        }
    }

    public async Task<IgdbGame?> GetGameDetailsAsync(int igdbId, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Getting IGDB game details for id: {Id}", igdbId);

            var body = $"fields name,summary,first_release_date,genres.name,involved_companies.company.name,involved_companies.developer,involved_companies.publisher,platforms.name,platforms.abbreviation,age_ratings.category,age_ratings.rating,cover.url; where id = {igdbId};";
            var response = await SendRequestAsync("games", body, cancellationToken);

            if (response == null)
            {
                return null;
            }

            var results = await response.Content.ReadFromJsonAsync<List<IgdbGame>>(cancellationToken);

            return results?.FirstOrDefault();
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error while getting IGDB game details for id: {Id}", igdbId);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting IGDB game details for id: {Id}", igdbId);
            return null;
        }
    }

    private async Task<HttpResponseMessage?> SendRequestAsync(string endpoint, string body, CancellationToken cancellationToken)
    {
        var token = await _tokenService.GetAccessTokenAsync(cancellationToken);

        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = new StringContent(body, Encoding.UTF8, "text/plain")
        };
        request.Headers.TryAddWithoutValidation("Client-ID", _configuration.ClientId);
        request.Headers.TryAddWithoutValidation("Authorization", $"Bearer {token}");

        var response = await _httpClient.SendAsync(request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
            {
                _logger.LogWarning("IGDB rate limit exceeded for endpoint: {Endpoint}", endpoint);
                throw new HttpRequestException("IGDB rate limit exceeded", null, response.StatusCode);
            }

            _logger.LogWarning("IGDB returned status code {StatusCode} for endpoint: {Endpoint}",
                response.StatusCode, endpoint);
            return null;
        }

        return response;
    }

    internal static string? FixCoverUrl(string? url)
    {
        if (string.IsNullOrEmpty(url))
        {
            return url;
        }

        // Prepend https: if protocol-relative
        if (url.StartsWith("//"))
        {
            url = "https:" + url;
        }

        // Use larger image size
        url = url.Replace("t_thumb", "t_cover_big");

        return url;
    }
}
