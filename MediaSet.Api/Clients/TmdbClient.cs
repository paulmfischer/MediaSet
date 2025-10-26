using System.Text.Json;
using System.Text.Json.Serialization;
using MediaSet.Api.Models;
using MediaSet.Api.Services;
using Microsoft.Extensions.Options;

namespace MediaSet.Api.Clients;

public class TmdbClient : IMovieMetadataClient, IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<TmdbClient> _logger;
    private readonly ICacheService _cacheService;
    private readonly TmdbConfiguration _config;

    public TmdbClient(
        HttpClient httpClient,
        ILogger<TmdbClient> logger,
        ICacheService cacheService,
        IOptions<TmdbConfiguration> config)
    {
        _httpClient = httpClient;
        _logger = logger;
        _cacheService = cacheService;
        _config = config.Value;
    }

    public void Dispose() => _httpClient?.Dispose();

    public async Task<MovieMetadataResponse?> SearchAndGetDetailsAsync(string title, int? year = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            _logger.LogWarning("Movie search called with empty title");
            return null;
        }

        var cacheKey = $"tmdb:movie:{title}:{year}";
        var cached = await _cacheService.GetAsync<MovieMetadataResponse>(cacheKey);
        if (cached != null)
        {
            _logger.LogInformation("Cache hit for movie {title} ({year})", title, year);
            return cached;
        }

        try
        {
            // Search for the movie
            var searchUrl = $"search/movie?query={Uri.EscapeDataString(title)}&api_key={_config.ApiKey}";
            if (year.HasValue)
            {
                searchUrl += $"&year={year.Value}";
            }

            var searchResponse = await _httpClient.GetFromJsonAsync<TmdbSearchResponse>(searchUrl, cancellationToken);

            if (searchResponse?.Results == null || searchResponse.Results.Count == 0)
            {
                _logger.LogInformation("No movie found for title {title} ({year})", title, year);
                // Don't cache null results
                return null;
            }

            // Get the first result (most relevant)
            var movieId = searchResponse.Results[0].Id;
            _logger.LogInformation("Found movie ID {movieId} for title {title}", movieId, title);

            // Get movie details
            var detailsUrl = $"movie/{movieId}?api_key={_config.ApiKey}&append_to_response=release_dates";
            var details = await _httpClient.GetFromJsonAsync<TmdbMovieDetails>(detailsUrl, cancellationToken);

            if (details == null)
            {
                _logger.LogWarning("Failed to get details for movie ID {movieId}", movieId);
                return null;
            }

            // Extract US certification (rating)
            var usCertification = details.ReleaseDates?.Results?
                .FirstOrDefault(r => r.Iso31661 == "US")?
                .ReleaseDates?
                .FirstOrDefault()?
                .Certification ?? string.Empty;

            var result = new MovieMetadataResponse(
                Title: details.Title ?? string.Empty,
                Genres: details.Genres?.Select(g => g.Name).ToList() ?? [],
                ProductionCompanies: details.ProductionCompanies?.Select(pc => pc.Name).ToList() ?? [],
                Overview: details.Overview ?? string.Empty,
                ReleaseDate: details.ReleaseDate ?? string.Empty,
                Runtime: details.Runtime,
                Certification: usCertification,
                IsTvSeries: false
            );

            await _cacheService.SetAsync(cacheKey, result, TimeSpan.FromDays(7));
            _logger.LogInformation("Successfully retrieved movie details for {title}", title);
            return result;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Failed to search or retrieve movie details for {title}", title);
            return null;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse TMDb response for {title}", title);
            return null;
        }
    }
}

internal record TmdbSearchResponse(
    [property: JsonPropertyName("results")] List<TmdbSearchResult> Results
);

internal record TmdbSearchResult(
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("release_date")] string? ReleaseDate
);

internal record TmdbMovieDetails(
    [property: JsonPropertyName("title")] string? Title,
    [property: JsonPropertyName("overview")] string? Overview,
    [property: JsonPropertyName("release_date")] string? ReleaseDate,
    [property: JsonPropertyName("runtime")] int? Runtime,
    [property: JsonPropertyName("genres")] List<TmdbGenre>? Genres,
    [property: JsonPropertyName("production_companies")] List<TmdbProductionCompany>? ProductionCompanies,
    [property: JsonPropertyName("release_dates")] TmdbReleaseDatesWrapper? ReleaseDates
);

internal record TmdbGenre(
    [property: JsonPropertyName("name")] string Name
);

internal record TmdbProductionCompany(
    [property: JsonPropertyName("name")] string Name
);

internal record TmdbReleaseDatesWrapper(
    [property: JsonPropertyName("results")] List<TmdbReleaseDateResult>? Results
);

internal record TmdbReleaseDateResult(
    [property: JsonPropertyName("iso_3166_1")] string Iso31661,
    [property: JsonPropertyName("release_dates")] List<TmdbReleaseDate>? ReleaseDates
);

internal record TmdbReleaseDate(
    [property: JsonPropertyName("certification")] string Certification
);
