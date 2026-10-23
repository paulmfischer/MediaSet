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
            var response = await _httpClient.GetProviderFromJsonAsync<Dictionary<string, BookResponse>>($"api/books?bibkeys=ISBN:{isbn}&format=json&jscmd=data", new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
            }, cancellationToken);
            _logger.LogInformation("Open Library ISBN lookup completed");

            var key = $"ISBN:{isbn}";
            return response?.ContainsKey(key) == true ? response[key] : null;
        }
        catch (HttpRequestException ex)
        {
            MediaSet.Api.Infrastructure.Lookup.LookupFailureScope.Record(ex);
            _logger.LogWarning(ex, "HTTP error while looking up book by ISBN: {Isbn}", isbn);
            return null;
        }
        catch (JsonException ex)
        {
            MediaSet.Api.Infrastructure.Lookup.LookupFailureScope.Record(ex);
            _logger.LogWarning(ex, "JSON parsing error while looking up book by ISBN: {Isbn}", isbn);
            return null;
        }
        catch (Exception ex)
        {
            MediaSet.Api.Infrastructure.Lookup.LookupFailureScope.Record(ex);
            _logger.LogError(ex, "Error looking up book by ISBN: {Isbn}", isbn);
            return null;
        }
    }

    public async Task<BookResponse?> GetReadableBookAsync(string identifierType, string identifierValue, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetProviderFromJsonAsync<ReadApiResponse>($"api/volumes/brief/{identifierType}/{identifierValue}.json", new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
            }, cancellationToken);
            _logger.LogInformation("Open Library readable lookup completed for {IdentifierType}", identifierType);

            var book = MapReadApiResponseToBookResponse(response);
            if (book == null) { return null; }
            var artwork = ExtractArtwork(response!, identifierType, identifierValue);
            return book with { ImageUrl = artwork.FirstOrDefault()?.Url, Artwork = artwork };
        }
        catch (HttpRequestException ex)
        {
            MediaSet.Api.Infrastructure.Lookup.LookupFailureScope.Record(ex);
            _logger.LogWarning(ex, "HTTP error while getting readable book for {identifierType}:{identifierValue}", identifierType, identifierValue);
            return null;
        }
        catch (JsonException ex)
        {
            MediaSet.Api.Infrastructure.Lookup.LookupFailureScope.Record(ex);
            _logger.LogWarning(ex, "JSON parsing error while getting readable book for {identifierType}:{identifierValue}", identifierType, identifierValue);
            return null;
        }
        catch (Exception ex)
        {
            MediaSet.Api.Infrastructure.Lookup.LookupFailureScope.Record(ex);
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

    public async Task<IReadOnlyList<BookResponse>> SearchByEntityPropertiesAsync(IReadOnlyDictionary<string, string> searchParams, CancellationToken cancellationToken = default)
    {
        searchParams.TryGetValue("title", out var title);
        searchParams.TryGetValue("author", out var author);

        _logger.LogInformation("Searching OpenLibrary by entity properties: title={Title}, author={Author}", title, author);

        try
        {
            string url;
            if (!string.IsNullOrWhiteSpace(title) && !string.IsNullOrWhiteSpace(author))
            {
                url = $"search.json?title={Uri.EscapeDataString(title)}&author={Uri.EscapeDataString(author)}&limit=10";
            }
            else if (!string.IsNullOrWhiteSpace(title))
            {
                url = $"search.json?q={Uri.EscapeDataString(title)}&limit=10";
            }
            else
            {
                _logger.LogWarning("No title provided for OpenLibrary entity search");
                return [];
            }

            url += "&fields=key,title,author_name,first_publish_year,isbn,publisher,subject,number_of_pages_median,cover_i,cover_edition_key";
            var response = await _httpClient.GetProviderFromJsonAsync<OpenLibrarySearchResponse>(url, cancellationToken);

            if (response == null || response.Docs.Count == 0)
            {
                _logger.LogInformation("No OpenLibrary search results for entity search: title={Title}", title);
                return [];
            }

            var docs = response.Docs.Where(d => !string.IsNullOrWhiteSpace(d.Title)).ToList();
            _logger.LogInformation("OpenLibrary search found {Count} results for entity search: title={Title}", docs.Count, title);

            var enrichmentTasks = docs.Select(doc => EnrichSearchDocAsync(doc, cancellationToken));
            return await Task.WhenAll(enrichmentTasks);
        }
        catch (HttpRequestException ex)
        {
            MediaSet.Api.Infrastructure.Lookup.LookupFailureScope.Record(ex);
            _logger.LogWarning(ex, "HTTP error while searching OpenLibrary by entity properties: title={Title}", title);
            return [];
        }
        catch (JsonException ex)
        {
            MediaSet.Api.Infrastructure.Lookup.LookupFailureScope.Record(ex);
            _logger.LogWarning(ex, "JSON parsing error while searching OpenLibrary by entity properties: title={Title}", title);
            return [];
        }
        catch (Exception ex)
        {
            MediaSet.Api.Infrastructure.Lookup.LookupFailureScope.Record(ex);
            _logger.LogError(ex, "Error searching OpenLibrary by entity properties: title={Title}", title);
            return [];
        }
    }

    private async Task<BookResponse> EnrichSearchDocAsync(OpenLibrarySearchDoc doc, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(doc.CoverEditionKey))
        {
            try
            {
                var enriched = await GetReadableBookByOlidAsync(doc.CoverEditionKey, cancellationToken);
                if (enriched != null)
                {
                    _logger.LogDebug("Enriched search result '{Title}' using OLID {Olid}", doc.Title, doc.CoverEditionKey);
                    var search = MapSearchDocToBookResponse(doc);
                    // Search covers describe a work/another edition even when enrichment
                    // happens to provide metadata for a particular edition.
                    var artwork = (enriched.Artwork ?? []).Concat(search.Artwork ?? []).DistinctBy(a => a.Url).ToList();
                    return enriched with { ImageUrl = search.ImageUrl ?? enriched.ImageUrl, Artwork = artwork,
                        Provenance = search.Provenance ?? enriched.Provenance };
                }
            }
            catch (Exception ex)
            {
                MediaSet.Api.Infrastructure.Lookup.LookupFailureScope.Record(ex);
                _logger.LogWarning(ex, "Failed to enrich '{Title}' with OLID {Olid}, falling back to search data",
                    doc.Title, doc.CoverEditionKey);
            }
        }

        return MapSearchDocToBookResponse(doc);
    }

    private static BookResponse MapSearchDocToBookResponse(OpenLibrarySearchDoc doc)
    {
        var authors = doc.AuthorName?.Select(a => new Author(a, string.Empty)).ToList() ?? [];
        var publishers = doc.Publisher?.Take(3).Select(p => new Publisher(p)).ToList() ?? [];
        var subjects = doc.Subject?.Take(5).Select(s => new Subject(s, string.Empty)).ToList() ?? [];
        var publishDate = doc.FirstPublishYear.HasValue ? doc.FirstPublishYear.Value.ToString() : string.Empty;
        var imageUrl = doc.CoverId is > 0 ? CoverUrl("id", doc.CoverId.Value.ToString()) : null;

        return new BookResponse(
            Title: doc.Title ?? string.Empty,
            Subtitle: string.Empty,
            Authors: authors,
            NumberOfPages: doc.NumberOfPagesMedian ?? 0,
            Publishers: publishers,
            PublishDate: publishDate,
            Subjects: subjects,
            Format: null,
            ImageUrl: imageUrl,
            Provenance: string.IsNullOrWhiteSpace(doc.Key ?? doc.CoverEditionKey) ? null
                : new LookupProvenance("OpenLibrary", doc.Key ?? doc.CoverEditionKey!, doc.Title ?? string.Empty,
                    ArtworkScope: "Work or alternate-edition artwork; exact edition unverified"),
            Artwork: imageUrl == null ? [] : [new BookArtwork(imageUrl,
                "Work or alternate-edition artwork; exact edition unverified", doc.CoverEditionKey)]
        );
    }

    private static BookResponse? MapReadApiResponseToBookResponse(ReadApiResponse? readApiResponse)
    {
        if (readApiResponse == null || !readApiResponse.Records.Any())
        {
            return null;
        }

        // Get the first record (usually there's only one)
        var firstRecord = readApiResponse.Records.Values.FirstOrDefault(r => r.Data != null);
        if (firstRecord == null) { return null; }
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
          imageUrl,
          firstRecord.Olids?.FirstOrDefault() is { } olid
            ? new LookupProvenance("OpenLibrary", olid, title) : null
        );
    }

    internal static string CoverUrl(string key, string value) =>
        $"https://covers.openlibrary.org/b/{key}/{Uri.EscapeDataString(value)}-L.jpg?default=false";

    private static List<BookArtwork> ExtractArtwork(ReadApiResponse response, string identifierType, string identifierValue)
    {
        var artwork = new List<BookArtwork>();
        var primary = response.Records.Values.FirstOrDefault(r => r.Data != null);
        foreach (var record in response.Records.Values)
        {
            // A read response can contain several records. Never borrow artwork
            // across conflicting titles/authors merely because it was returned nearby.
            if (record.Data == null || primary?.Data == null) { continue; }
            if (!ReferenceEquals(record, primary)
                && (NormalizeSubjectKey(new Subject(record.Data.ExtractStringFromData("title"), ""))
                    != NormalizeSubjectKey(new Subject(primary.Data.ExtractStringFromData("title"), ""))
                || !record.Data.ExtractAuthorsFromData().Select(a => a.Name).Order()
                    .SequenceEqual(primary.Data.ExtractAuthorsFromData().Select(a => a.Name).Order()))) { continue; }
            var exact = identifierType == "isbn" && (record.Isbns?.Contains(identifierValue) ?? false)
                || identifierType == "olid" && (record.Olids?.Contains(identifierValue) ?? false);
            var scope = exact ? "Exact-edition artwork" : "Related edition artwork; exact edition unverified";
            var edition = record.Olids?.FirstOrDefault();
            void Add(string? url)
            {
                if (Uri.TryCreate(url, UriKind.Absolute, out var uri)
                    && uri.Host.Equals("covers.openlibrary.org", StringComparison.OrdinalIgnoreCase))
                {
                    var builder = new UriBuilder(uri) { Scheme = "https", Port = -1,
                        Query = "default=false" };
                    artwork.Add(new(builder.Uri.AbsoluteUri, scope, edition));
                }
            }
            void Inspect(JsonElement element)
            {
                if (element.ValueKind != JsonValueKind.Object) { return; }
                if (element.TryGetProperty("cover", out var cover) && cover.ValueKind == JsonValueKind.Object)
                {
                    foreach (var size in new[] { "large", "medium", "small" })
                    {
                        if (cover.TryGetProperty(size, out var url) && url.ValueKind == JsonValueKind.String) { Add(url.GetString()); }
                    }
                }
                if (element.TryGetProperty("covers", out var covers) && covers.ValueKind == JsonValueKind.Array)
                {
                    foreach (var id in covers.EnumerateArray())
                    {
                        if (id.ValueKind == JsonValueKind.Number && id.TryGetInt64(out var value) && value > 0) { Add(CoverUrl("id", value.ToString())); }
                    }
                }
                if (element.TryGetProperty("details", out var details)) { Inspect(details); }
            }
            if (record.Data != null) { Inspect(JsonSerializer.SerializeToElement(record.Data)); }
            if (record.Details != null) { Inspect(JsonSerializer.SerializeToElement(record.Details)); }
            foreach (var item in (response.Items ?? []).Where(i => i.FromRecord == record.RecordUrl
                || !string.IsNullOrEmpty(edition) && i.OlEditionId == edition))
            {
                Add(item.Cover?.Large); Add(item.Cover?.Medium); Add(item.Cover?.Small);
            }
            foreach (var olid in record.Olids ?? []) { Add(CoverUrl("olid", olid)); }
        }
        if (identifierType is "isbn" or "olid")
        {
            artwork.Add(new(CoverUrl(identifierType, identifierValue), "Exact-edition artwork", identifierType == "olid" ? identifierValue : null));
        }
        return artwork.DistinctBy(a => a.Url).ToList();
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
