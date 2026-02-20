using MediaSet.Api.Infrastructure.Lookup.Models;
using MediaSet.Api.Shared.Models;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace MediaSet.Api.Infrastructure.Lookup.Clients.OpenLibrary;

public class OpenLibraryClient : IOpenLibraryClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<OpenLibraryClient> _logger;

    public OpenLibraryClient(HttpClient httpClient, ILogger<OpenLibraryClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<BookResponse?> GetBookByIsbnAsync(string isbn, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetFromJsonAsync<Dictionary<string, BookResponse>>($"api/books?bibkeys=ISBN:{isbn}&format=json&jscmd=data", new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
            }, cancellationToken);
            _logger.LogInformation("books lookup by isbn: {response}", JsonSerializer.Serialize(response));

            var key = $"ISBN:{isbn}";
            return response?.ContainsKey(key) == true ? response[key] : null;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "HTTP error while looking up book by ISBN: {Isbn}", isbn);
            return null;
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "JSON parsing error while looking up book by ISBN: {Isbn}", isbn);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error looking up book by ISBN: {Isbn}", isbn);
            return null;
        }
    }

    public async Task<BookResponse?> GetReadableBookAsync(string identifierType, string identifierValue, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetFromJsonAsync<ReadApiResponse>($"api/volumes/brief/{identifierType}/{identifierValue}.json", new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
            }, cancellationToken);
            _logger.LogInformation("readable book lookup by {identifierType}:{identifierValue}: {response}", identifierType, identifierValue, JsonSerializer.Serialize(response));

            return MapReadApiResponseToBookResponse(response);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "HTTP error while getting readable book for {identifierType}:{identifierValue}", identifierType, identifierValue);
            return null;
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "JSON parsing error while getting readable book for {identifierType}:{identifierValue}", identifierType, identifierValue);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting readable book for {identifierType}:{identifierValue}", identifierType, identifierValue);
            return null;
        }
    }

    public async Task<BookResponse?> GetReadableBookByIsbnAsync(string isbn, CancellationToken cancellationToken = default)
    {
        return await GetReadableBookAsync("isbn", isbn, cancellationToken);
    }

    public async Task<BookResponse?> GetReadableBookByLccnAsync(string lccn, CancellationToken cancellationToken = default)
    {
        return await GetReadableBookAsync("lccn", lccn, cancellationToken);
    }

    public async Task<BookResponse?> GetReadableBookByOclcAsync(string oclc, CancellationToken cancellationToken = default)
    {
        return await GetReadableBookAsync("oclc", oclc, cancellationToken);
    }

    public async Task<BookResponse?> GetReadableBookByOlidAsync(string olid, CancellationToken cancellationToken = default)
    {
        return await GetReadableBookAsync("olid", olid, cancellationToken);
    }

    public async Task<BookResponse?> GetReadableBookAsync(IdentifierType identifierType, string identifierValue, CancellationToken cancellationToken = default)
    {
        return identifierType switch
        {
            IdentifierType.Isbn => await GetReadableBookByIsbnAsync(identifierValue, cancellationToken),
            IdentifierType.Lccn => await GetReadableBookByLccnAsync(identifierValue, cancellationToken),
            IdentifierType.Oclc => await GetReadableBookByOclcAsync(identifierValue, cancellationToken),
            IdentifierType.Olid => await GetReadableBookByOlidAsync(identifierValue, cancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(identifierType), identifierType, null)
        };
    }

    public async Task<IReadOnlyList<BookResponse>> SearchByTitleAsync(string title, CancellationToken cancellationToken = default)
    {
        try
        {
            var encodedTitle = Uri.EscapeDataString(title);
            var response = await _httpClient.GetFromJsonAsync<OpenLibrarySearchResponse>(
                $"search.json?title={encodedTitle}&limit=10",
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true },
                cancellationToken);

            if (response == null || response.Docs.Count == 0)
            {
                _logger.LogInformation("No OpenLibrary search results for title: {Title}", title);
                return [];
            }

            _logger.LogInformation("OpenLibrary search found {Count} results for title: {Title}", response.Docs.Count, title);

            return response.Docs
                .Where(d => !string.IsNullOrWhiteSpace(d.Title))
                .Select(MapSearchDocToBookResponse)
                .ToList();
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "HTTP error while searching OpenLibrary by title: {Title}", title);
            return [];
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "JSON parsing error while searching OpenLibrary by title: {Title}", title);
            return [];
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching OpenLibrary by title: {Title}", title);
            return [];
        }
    }

    private static BookResponse MapSearchDocToBookResponse(OpenLibrarySearchDoc doc)
    {
        var authors = doc.AuthorName?.Select(a => new Author(a, string.Empty)).ToList() ?? [];
        var publishers = doc.Publisher?.Take(3).Select(p => new Publisher(p)).ToList() ?? [];
        var subjects = doc.Subject?.Take(5).Select(s => new Subject(s, string.Empty)).ToList() ?? [];
        var publishDate = doc.FirstPublishYear.HasValue ? doc.FirstPublishYear.Value.ToString() : string.Empty;
        var imageUrl = doc.CoverId.HasValue ? $"https://covers.openlibrary.org/b/id/{doc.CoverId.Value}-L.jpg" : null;

        return new BookResponse(
            Title: doc.Title ?? string.Empty,
            Subtitle: string.Empty,
            Authors: authors,
            NumberOfPages: doc.NumberOfPagesMedian ?? 0,
            Publishers: publishers,
            PublishDate: publishDate,
            Subjects: subjects,
            Format: null,
            ImageUrl: imageUrl
        );
    }

    private static BookResponse? MapReadApiResponseToBookResponse(ReadApiResponse? readApiResponse)
    {
        if (readApiResponse == null || !readApiResponse.Records.Any())
        {
            return null;
        }

        // Get the first record (usually there's only one)
        var firstRecord = readApiResponse.Records.Values.First();
        var data = firstRecord.Data;

        if (data == null)
        {
            return null;
        }

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
        string? imageUrl = null;
        if (firstRecord.Details?.TryGetValue("details", out var detailsObj) == true &&
            detailsObj is JsonElement detailsElement &&
            detailsElement.ValueKind == JsonValueKind.Object)
        {
            if (detailsElement.TryGetProperty("physical_format", out var formatElement))
            {
                format = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(formatElement.GetString() ?? string.Empty);
            }

            if (detailsElement.TryGetProperty("covers", out var coversElement) &&
                coversElement.ValueKind == JsonValueKind.Array &&
                coversElement.GetArrayLength() > 0)
            {
                var firstCoverId = coversElement[0].GetInt64();
                imageUrl = $"https://covers.openlibrary.org/b/id/{firstCoverId}-L.jpg";
            }
        }

        return new BookResponse(
          title,
          subtitle,
          authors,
          numberOfPages,
          publishers,
          publishDate,
          subjects,
          format,
          imageUrl
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
