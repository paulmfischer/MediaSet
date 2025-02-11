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

public class OpenLibraryConfiguration
{
  public string BaseUrl { get; set; } = "https://openlibrary.org/";
  public int Timeout { get; set; } = 30;
  public string ContactEmail { get; set; } = "me@paulmfischer.dev";
}