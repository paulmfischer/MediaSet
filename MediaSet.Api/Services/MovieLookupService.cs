using System.Text.Json;
using MediaSet.Api.Clients;

namespace MediaSet.Api.Services;

public class MovieLookupService
{
  private readonly TmdbClient tmdbClient;
  private readonly IHttpClientFactory httpClientFactory;

  public MovieLookupService(TmdbClient _tmdbClient, IHttpClientFactory _httpClientFactory)
  {
    tmdbClient = _tmdbClient;
    httpClientFactory = _httpClientFactory;
  }
  
  public async Task<MovieResult?> SearchByUpcAsync(string upc)
  {
    var client = httpClientFactory.CreateClient("UpcLookup");
    var upcResponse = await client.GetAsync($"https://api.upcitemdb.com/prod/trial/lookup?upc={upc}");
    upcResponse.EnsureSuccessStatusCode();
    
    var content = await upcResponse.Content.ReadAsStringAsync();
    var upcData = JsonSerializer.Deserialize<UpcResponse>(content);

    if (upcData == null)
    {
      return null;
    }
    var title = upcData.Items[0].Title;
    var movieResponse = await tmdbClient.SearchMovieAsync(title);
    return movieResponse?.Results.FirstOrDefault();
  }
}

public class UpcResponse
{
  public List<UpcItem> Items { get; set; } = [];
}

public class UpcItem
{
  public string Title { get; set; } = string.Empty;
  public string Description { get; set; } = string.Empty;
  public string Brand { get; set; } = string.Empty;
}