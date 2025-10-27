using System.Text.Json;
using System.Web;
using Microsoft.Extensions.Options;
using MediaSet.Api.Models;

namespace MediaSet.Api.Clients;

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

        _httpClient.BaseAddress = new Uri(_configuration.BaseUrl);
        _httpClient.Timeout = TimeSpan.FromSeconds(_configuration.Timeout);
        
        if (!string.IsNullOrEmpty(_configuration.BearerToken))
        {
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_configuration.BearerToken}");
        }
    }

    public async Task<TmdbSearchResponse?> SearchMovieAsync(string title, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Searching TMDB for movie: {Title}", title);

            var encodedTitle = HttpUtility.UrlEncode(title);
            var response = await _httpClient.GetAsync($"search/movie?query={encodedTitle}", cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                {
                    _logger.LogWarning("TMDB rate limit exceeded for movie search: {Title}", title);
                    throw new HttpRequestException("TMDB rate limit exceeded", null, response.StatusCode);
                }

                _logger.LogWarning("TMDB search returned status code {StatusCode} for movie: {Title}", 
                    response.StatusCode, title);
                return null;
            }

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            var result = JsonSerializer.Deserialize<TmdbSearchResponse>(content, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            _logger.LogInformation("TMDB search found {Count} results for movie: {Title}", 
                result?.Results.Count ?? 0, title);

            return result;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error while searching TMDB for movie: {Title}", title);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching TMDB for movie: {Title}", title);
            return null;
        }
    }

    public async Task<TmdbMovieResponse?> GetMovieDetailsAsync(int movieId, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Getting TMDB movie details for ID: {MovieId}", movieId);

            var response = await _httpClient.GetAsync($"movie/{movieId}", cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                {
                    _logger.LogWarning("TMDB rate limit exceeded for movie details: {MovieId}", movieId);
                    throw new HttpRequestException("TMDB rate limit exceeded", null, response.StatusCode);
                }

                _logger.LogWarning("TMDB movie details returned status code {StatusCode} for ID: {MovieId}", 
                    response.StatusCode, movieId);
                return null;
            }

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            var result = JsonSerializer.Deserialize<TmdbMovieResponse>(content, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            _logger.LogInformation("Successfully retrieved TMDB movie details for ID: {MovieId}", movieId);

            return result;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error while getting TMDB movie details for ID: {MovieId}", movieId);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting TMDB movie details for ID: {MovieId}", movieId);
            return null;
        }
    }
}
