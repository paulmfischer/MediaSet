using MediaSet.Api.Infrastructure.Lookup.Models;
using System.Web;
using Microsoft.Extensions.Options;

namespace MediaSet.Api.Infrastructure.Lookup.Clients.Tmdb;

public class TmdbClient : ITmdbClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<TmdbClient> _logger;
    private readonly TmdbConfiguration _configuration;

    public TmdbClient(
        HttpClient httpClient,
        IOptions<TmdbConfiguration> configuration,
        ILogger<TmdbClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _configuration = configuration.Value;
    }

    public async Task<TmdbSearchResponse?> SearchMovieAsync(string title, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Searching TMDB for movie: {Title}", title);

            var encodedTitle = HttpUtility.UrlEncode(title);
            var response = await _httpClient.GetProviderAsync($"search/movie?query={encodedTitle}", cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                MediaSet.Api.Infrastructure.Lookup.LookupFailureScope.RecordStatus(response);
                if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                {
                    _logger.LogWarning("TMDB rate limit exceeded for movie search: {Title}", title);
                    throw new HttpRequestException("TMDB rate limit exceeded", null, response.StatusCode);
                }

                _logger.LogWarning("TMDB search returned status code {StatusCode} for movie: {Title}", 
                    response.StatusCode, title);
                return null;
            }

            var result = await response.Content.ReadFromJsonAsync<TmdbSearchResponse>(cancellationToken);

            _logger.LogInformation("TMDB search found {Count} results for movie: {Title}", 
                result?.Results.Count ?? 0, title);

            return result;
        }
        catch (HttpRequestException ex)
        {
            MediaSet.Api.Infrastructure.Lookup.LookupFailureScope.Record(ex);
            _logger.LogError(ex, "HTTP error while searching TMDB for movie: {Title}", title);
            throw;
        }
        catch (Exception ex)
        {
            MediaSet.Api.Infrastructure.Lookup.LookupFailureScope.Record(ex);
            _logger.LogError(ex, "Error searching TMDB for movie: {Title}", title);
            return null;
        }
    }

    public Task<TmdbTvSearchResponse?> SearchTvAsync(string title, CancellationToken cancellationToken) =>
        GetTvResourceAsync<TmdbTvSearchResponse>($"search/tv?query={HttpUtility.UrlEncode(title)}", cancellationToken);

    public Task<TmdbTvResponse?> GetTvDetailsAsync(int seriesId, CancellationToken cancellationToken) =>
        GetTvResourceAsync<TmdbTvResponse>($"tv/{seriesId}", cancellationToken);

    public Task<TmdbSeasonResponse?> GetSeasonDetailsAsync(
        int seriesId, int seasonNumber, CancellationToken cancellationToken) =>
        GetTvResourceAsync<TmdbSeasonResponse>($"tv/{seriesId}/season/{seasonNumber}", cancellationToken);

    private async Task<T?> GetTvResourceAsync<T>(string resource, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Getting TMDB TV resource: {Resource}", resource);
            using var response = await _httpClient.GetProviderAsync(resource, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                LookupFailureScope.RecordStatus(response);
                if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                {
                    throw new HttpRequestException("TMDB rate limit exceeded", null, response.StatusCode);
                }
                return default;
            }
            return await response.Content.ReadFromJsonAsync<T>(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (HttpRequestException ex)
        {
            LookupFailureScope.Record(ex);
            _logger.LogError(ex, "HTTP error getting TMDB TV resource: {Resource}", resource);
            throw;
        }
        catch (Exception ex)
        {
            LookupFailureScope.Record(ex);
            _logger.LogError(ex, "Error getting TMDB TV resource: {Resource}", resource);
            return default;
        }
    }

    public async Task<TmdbMovieResponse?> GetMovieDetailsAsync(int movieId, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Getting TMDB movie details for ID: {MovieId}", movieId);

            var response = await _httpClient.GetProviderAsync($"movie/{movieId}", cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                MediaSet.Api.Infrastructure.Lookup.LookupFailureScope.RecordStatus(response);
                if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                {
                    _logger.LogWarning("TMDB rate limit exceeded for movie details: {MovieId}", movieId);
                    throw new HttpRequestException("TMDB rate limit exceeded", null, response.StatusCode);
                }

                _logger.LogWarning("TMDB movie details returned status code {StatusCode} for ID: {MovieId}", 
                    response.StatusCode, movieId);
                return null;
            }

            var result = await response.Content.ReadFromJsonAsync<TmdbMovieResponse>(cancellationToken);

            _logger.LogInformation("Successfully retrieved TMDB movie details for ID: {MovieId}", movieId);

            return result;
        }
        catch (HttpRequestException ex)
        {
            MediaSet.Api.Infrastructure.Lookup.LookupFailureScope.Record(ex);
            _logger.LogError(ex, "HTTP error while getting TMDB movie details for ID: {MovieId}", movieId);
            throw;
        }
        catch (Exception ex)
        {
            MediaSet.Api.Infrastructure.Lookup.LookupFailureScope.Record(ex);
            _logger.LogError(ex, "Error getting TMDB movie details for ID: {MovieId}", movieId);
            return null;
        }
    }
}
