using System.Globalization;
using System.Text;
using System.Text.Json;
using MediaSet.Api.Helpers;
using MediaSet.Api.Models;

namespace MediaSet.Api.Clients;

public class OpenLibraryClient : IOpenLibraryClient, IDisposable
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
        var response = await httpClient.GetFromJsonAsync<Dictionary<string, BookResponse>>($"api/books?bibkeys=ISBN:{isbn}&format=json&jscmd=data", new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
        });
        logger.LogInformation("books lookup by isbn: {response}", JsonSerializer.Serialize(response));

        var key = $"ISBN:{isbn}";
        return response?.ContainsKey(key) == true ? response[key] : null;
    }

    public async Task<BookResponse?> GetReadableBookAsync(string identifierType, string identifierValue)
    {
        try
        {
            var response = await httpClient.GetFromJsonAsync<ReadApiResponse>($"api/volumes/brief/{identifierType}/{identifierValue}.json", new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
            });
            logger.LogInformation("readable book lookup by {identifierType}:{identifierValue}: {response}", identifierType, identifierValue, JsonSerializer.Serialize(response));

            return MapReadApiResponseToBookResponse(response);
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning("Failed to get readable book for {identifierType}:{identifierValue}: {error}", identifierType, identifierValue, ex.Message);
            return null;
        }
    }

    public async Task<BookResponse?> GetReadableBookByIsbnAsync(string isbn)
    {
        return await GetReadableBookAsync("isbn", isbn);
    }

    public async Task<BookResponse?> GetReadableBookByLccnAsync(string lccn)
    {
        return await GetReadableBookAsync("lccn", lccn);
    }

    public async Task<BookResponse?> GetReadableBookByOclcAsync(string oclc)
    {
        return await GetReadableBookAsync("oclc", oclc);
    }

    public async Task<BookResponse?> GetReadableBookByOlidAsync(string olid)
    {
        return await GetReadableBookAsync("olid", olid);
    }

    public async Task<BookResponse?> GetReadableBookAsync(IdentifierType identifierType, string identifierValue)
    {
        return identifierType switch
        {
            IdentifierType.Isbn => await GetReadableBookByIsbnAsync(identifierValue),
            IdentifierType.Lccn => await GetReadableBookByLccnAsync(identifierValue),
            IdentifierType.Oclc => await GetReadableBookByOclcAsync(identifierValue),
            IdentifierType.Olid => await GetReadableBookByOlidAsync(identifierValue),
            _ => throw new ArgumentOutOfRangeException(nameof(identifierType), identifierType, null)
        };
    }

    private static BookResponse? MapReadApiResponseToBookResponse(ReadApiResponse? readApiResponse)
    {
        if (readApiResponse == null || !readApiResponse.Records.Any())
            return null;

        // Get the first record (usually there's only one)
        var firstRecord = readApiResponse.Records.Values.First();
        var data = firstRecord.Data;

        if (data == null)
            return null;

        // Extract title and subtitle from data
        var title = data.ExtractStringFromData("title");
        var subtitle = data.ExtractStringFromData("subtitle");

        // Extract authors
        var authors = data.ExtractAuthorsFromData();

        // Extract number of pages
        var numberOfPages = data.ExtractNumberOfPagesFromData();

        // Extract publishers
        var publishers = data.ExtractPublishersFromData();

        // Extract publish date
        var publishDate = firstRecord.PublishDates?.FirstOrDefault() ??
                         data.ExtractStringFromData("publish_date");

        // Extract subjects and remove duplicates using a robust normalization key
        // that ignores case, whitespace, punctuation, and diacritics so near-identical
        // variants from OpenLibrary collapse to a single subject.
        var subjects = data.ExtractSubjectsFromData()
            .GroupBy(NormalizeSubjectKey)
            .Select(g => g.First())
            .Select(s => new Subject(
                NormalizeDisplaySubject(s.Name),
                s.Url
            ))
            .ToList();

        // Extract format from details object
        var format = string.Empty;
        if (firstRecord.Details?.TryGetValue("details", out var detailsObj) == true &&
            detailsObj is JsonElement detailsElement &&
            detailsElement.ValueKind == JsonValueKind.Object &&
            detailsElement.TryGetProperty("physical_format", out var formatElement))
        {
            format = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(formatElement.GetString() ?? string.Empty);
        }

        return new BookResponse(
          title,
          subtitle,
          authors,
          numberOfPages,
          publishers,
          publishDate,
          subjects,
          format
        );
    }

    private static string NormalizeSubjectKey(Subject subject)
    {
        // Prefer the name; fall back to URL segment if name is empty
        var source = string.IsNullOrWhiteSpace(subject.Name)
            ? (subject.Url ?? string.Empty)
            : subject.Name;

        if (string.IsNullOrEmpty(source))
        {
            return string.Empty;
        }

        // Lowercase and decompose to strip diacritics
        var normalized = source.Normalize(NormalizationForm.FormD).ToLowerInvariant();

        // Build a key with only letters and digits, skipping punctuation, underscores and whitespace
        var sb = new StringBuilder(normalized.Length);
        foreach (var ch in normalized)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(ch);
            if (category == UnicodeCategory.NonSpacingMark)
            {
                // skip diacritic marks
                continue;
            }

            if (char.IsLetterOrDigit(ch))
            {
                sb.Append(ch);
            }
            // else skip punctuation/separators entirely
        }

        return sb.ToString().Normalize(NormalizationForm.FormC);
    }

    private static string NormalizeDisplaySubject(string? name)
    {
        var text = (name ?? string.Empty).Trim();
        if (text.Length == 0)
        {
            return string.Empty;
        }

        // Title-case
        text = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(text.ToLowerInvariant());

        // Replace commas with semicolons to avoid UI splitting issues
        text = text.Replace(',', ';');

        // Normalize spaces around semicolons: "; " (one space after, none before)
        // 1) Remove spaces around semicolons
        var sb = new StringBuilder(text.Length);
        bool lastWasSemicolon = false;
        foreach (var ch in text)
        {
            if (ch == ';')
            {
                if (sb.Length > 0 && sb[^1] == ' ')
                {
                    sb.Length -= 1; // remove space before semicolon
                }
                sb.Append(';');
                lastWasSemicolon = true;
            }
            else
            {
                if (lastWasSemicolon && ch == ' ')
                {
                    // ensure single space after semicolon
                    sb.Append(' ');
                    lastWasSemicolon = false;
                    continue;
                }
                sb.Append(ch);
                lastWasSemicolon = false;
            }
        }

        // Ensure there's exactly one space after each semicolon (if not end of string)
        // The loop above attempts to normalize; final Trim to clean up.
        return sb.ToString().Trim();
    }
}

public record BookResponse(
  string Title,
  string Subtitle,
  List<Author> Authors,
  int NumberOfPages,
  List<Publisher> Publishers,
  string PublishDate,
  List<Subject> Subjects,
  string? Format = null
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
  Dictionary<string, object>? Data,
  Dictionary<string, object>? Details
);

public class OpenLibraryConfiguration
{
    public string BaseUrl { get; set; } = "https://openlibrary.org/";
    public int Timeout { get; set; } = 30;
    public string ContactEmail { get; set; } = "me@paulmfischer.dev";
}
