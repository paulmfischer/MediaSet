using System.Text.Json;

namespace MediaSet.Api.Clients;

public class OpenLibraryClient : IDisposable
{
  private readonly HttpClient httpClient;
  private readonly ILogger<OpenLibraryClient> logger;

  public OpenLibraryClient(HttpClient _httpClient, ILogger<OpenLibraryClient> _logger)
  {
    httpClient = _httpClient;
    logger = _logger;
  }

  public void Dispose() => httpClient?.Dispose();

  public async Task<BookResponse?> GetBookByIsbnAsync(string isbn)
  {
    var response = await httpClient.GetFromJsonAsync<Dictionary<string, BookResponse>>($"api/books?bibkeys=ISBN:{isbn}&format=json&jscmd=data", new JsonSerializerOptions {
      PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    });
    logger.LogInformation("books lookup by isbn: {response}", JsonSerializer.Serialize(response));

    var key = $"ISBN:{isbn}";
    return response?.ContainsKey(key) == true ? response[key] : null;
  }

  public async Task<ReadApiResponse?> GetReadableBookAsync(string identifierType, string identifierValue)
  {
    try
    {
      var response = await httpClient.GetFromJsonAsync<ReadApiResponse>($"api/volumes/brief/{identifierType}/{identifierValue}.json", new JsonSerializerOptions {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
      });
      logger.LogInformation("readable book lookup by {identifierType}:{identifierValue}: {response}", identifierType, identifierValue, JsonSerializer.Serialize(response));
      
      return response;
    }
    catch (HttpRequestException ex)
    {
      logger.LogWarning("Failed to get readable book for {identifierType}:{identifierValue}: {error}", identifierType, identifierValue, ex.Message);
      return null;
    }
  }

  public async Task<ReadApiResponse?> GetReadableBookByIsbnAsync(string isbn)
  {
    return await GetReadableBookAsync("isbn", isbn);
  }

  public async Task<ReadApiResponse?> GetReadableBookByLccnAsync(string lccn)
  {
    return await GetReadableBookAsync("lccn", lccn);
  }

  public async Task<ReadApiResponse?> GetReadableBookByOclcAsync(string oclc)
  {
    return await GetReadableBookAsync("oclc", oclc);
  }

  public async Task<ReadApiResponse?> GetReadableBookByOlidAsync(string olid)
  {
    return await GetReadableBookAsync("olid", olid);
  }
}

public record BookResponse(
  string Title,
  string Subtitle,
  List<Author> Authors,
  int NumberOfPages,
  List<Publisher> Publishers,
  string PublishDate,
  List<Subject> Subjects
);

public record Author(
  string Name,
  string Url
);

public record Publisher(string Name);

public record Subject(string Name, string Url);

public record ReadApiResponse(
  List<ReadApiItem> Items,
  Dictionary<string, ReadApiRecord> Records
);

public record ReadApiItem(
  string Match,
  string Status,
  string ItemUrl,
  ReadApiCover? Cover,
  string FromRecord,
  string PublishDate,
  string OlEditionId,
  string OlWorkId
);

public record ReadApiCover(
  string Small,
  string Medium,
  string Large
);

public record ReadApiRecord(
  List<string> Isbns,
  List<string> Lccns,
  List<string> Oclcs,
  List<string> Olids,
  List<string> PublishDates,
  string RecordUrl,
  Dictionary<string, object>? Data
);

public class OpenLibraryConfiguration
{
  public string BaseUrl { get; set; } = "https://openlibrary.org/";
  public int Timeout { get; set; } = 30;
  public string ContactEmail { get; set; } = "me@paulmfischer.dev";
}