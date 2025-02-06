using System.Text.Json;
using MediaSet.Api.Models;
using Microsoft.Extensions.Options;

namespace MediaSet.Api.Clients;

public class TmdbClient
{
    private readonly IHttpClientFactory httpClientFactory;
    private readonly string apiKey;
    private const string BaseUrl = "https://api.themoviedb.org/3";

    public TmdbClient(IHttpClientFactory _httpClientFactory, IOptions<ClientApiSettings> clientApiSettings)
    {
      httpClientFactory = _httpClientFactory;
      apiKey = clientApiSettings.Value.TmdbApiKey;
    }

    public async Task<MovieResponse?> SearchMovieAsync(string query)
    {
      var client = httpClientFactory.CreateClient("Tmdb");
      var response = await client.GetAsync($"{BaseUrl}/search/movie?api_key={apiKey}&query={Uri.EscapeDataString(query)}");
        
      response.EnsureSuccessStatusCode();
      var content = await response.Content.ReadAsStringAsync();
      return JsonSerializer.Deserialize<MovieResponse>(content);
    }

    public async Task<MovieDetails?> GetMovieDetailsAsync(int movieId)
    {
      var client = httpClientFactory.CreateClient("Tmdb");
      var response = await client.GetAsync($"{BaseUrl}/movie/{movieId}?api_key={apiKey}");
      
      response.EnsureSuccessStatusCode();
      var content = await response.Content.ReadAsStringAsync();
      return JsonSerializer.Deserialize<MovieDetails>(content);
    }
}

public class MovieResponse
{
  public int Page { get; set; }
  public List<MovieResult> Results { get; set; } = [];
  public int TotalPages { get; set; }
  public int TotalResults { get; set; }
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