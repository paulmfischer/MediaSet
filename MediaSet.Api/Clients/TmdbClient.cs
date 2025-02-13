using System.Text.Json;
using Microsoft.Extensions.Options;

namespace MediaSet.Api.Clients;

public class TmdbClient(ILogger<TmdbClient> logger, HttpClient httpClient, IOptions<TmdbClientConfiguration> tmdbConfig)
{
  private string ApiKey => string.IsNullOrWhiteSpace(tmdbConfig.Value.ApiKey)
    ? throw new ArgumentException("ApiKey is required for Tmdb client", nameof(ApiKey))
    : tmdbConfig.Value.ApiKey;

  public async Task<MovieResponse?> SearchMovieAsync(string query)
  {
    var response = await httpClient.GetAsync($"search/movie?api_key={ApiKey}&query={Uri.EscapeDataString(query)}");
    response.EnsureSuccessStatusCode();
    var responseData = await response.Content.ReadFromJsonAsync<MovieResponse>();
    logger.LogInformation("Tmdb search results: {response}", JsonSerializer.Serialize(responseData));

    return responseData;
  }
}

public class MovieResponse
{
  public int Page { get; set; }
  public List<MovieResult> Results { get; set; } = [];
  public int TotalPages { get; set; }
  public int TotalResults { get; set; }
  public override string ToString()
  {
    return JsonSerializer.Serialize(this);
  }
}

public class MovieResult
{
  public int Id { get; set; }
  public string Title { get; set; } = string.Empty;
  public string Overview { get; set; } = string.Empty;
  public string ReleaseDate { get; set; } = string.Empty;
  public double VoteAverage { get; set; }
}

public class MovieDetails
{
  public int Id { get; set; }
  public string Title { get; set; } = string.Empty;
  public string Overview { get; set; } = string.Empty;
  public string ReleaseDate { get; set; } = string.Empty;
  public int Runtime { get; set; }
  public List<Genre> Genres { get; set; } = [];
}

public class Genre
{
  public int Id { get; set; }
  public string Name { get; set; } = string.Empty;
}

public class TmdbClientConfiguration
{
  public string BaseUrl { get; set; } = "";
  public string? ApiKey { get; set; }
}