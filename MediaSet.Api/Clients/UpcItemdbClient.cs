using System.Text.Json;

namespace MediaSet.Api.Clients;

public class UpcItemdbClient(ILogger<UpcItemdbClient> logger, HttpClient httpClient)
{
  private const string RateLimitHeader = "X-RateLimit-Limit";
  private const string RemainingHeader = "X-RateLimit-Remaining";
  private const string ResetHeader = "X-RateLimit-Reset";

  public async Task<UpcResponse?> SearchByUpc(string upc)
  {
    var upcResponse = await httpClient.GetAsync($"prod/trial/lookup?upc={upc}");
    upcResponse.EnsureSuccessStatusCode();
    if (upcResponse.Headers.Contains(RateLimitHeader))
    {
      var headers = upcResponse.Headers;
      logger.LogInformation(
        "UpcItem Limit: {limit} - Remaining: {remaining} - Reset: {reset}",
        headers.GetValues(RateLimitHeader).First(),
        headers.GetValues(RemainingHeader).First(),
        headers.GetValues(ResetHeader).First()
      );
    }
    var upcData = await upcResponse.Content.ReadFromJsonAsync<UpcResponse>();
    logger.LogInformation("UpcItem search results: {response}", JsonSerializer.Serialize(upcData));
    return upcData;
  }
}

public class UpcResponse
{
  public List<UpcItem> Items { get; set; } = [];
  public override string ToString()
  {
    return JsonSerializer.Serialize(this);
  }
}

public class UpcItem
{
  public string Title { get; set; } = string.Empty;
  public string Description { get; set; } = string.Empty;
  public string Brand { get; set; } = string.Empty;
}

public class UpcItemClientConfiguration
{
  public string BaseUrl { get; set; } = "https://api.upcitemdb.com";
}