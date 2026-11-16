using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Text.Json.Serialization;
using MediaSet.Api.Shared.Extensions;

namespace MediaSet.Api.Infrastructure.Lookup.Clients.GoogleBooks;

/// <summary>
/// Keyless Google Books volumes API client (unauthenticated requests share a per-IP quota).
/// Only resolves a cover URL by ISBN; book metadata still comes from Open Library.
/// </summary>
public class GoogleBooksClient(HttpClient httpClient, ILogger<GoogleBooksClient> logger) : IGoogleBooksClient
{
    // Largest first; the volumes search usually returns only thumbnail and smallThumbnail.
    private static readonly string[] _imageSizes = ["extraLarge", "large", "medium", "small", "thumbnail", "smallThumbnail"];

    public async Task<string?> GetCoverUrlByIsbnAsync(string isbn, CancellationToken cancellationToken = default)
    {
        var normalized = NormalizeIsbn(isbn);
        if (normalized == null)
        {
            return null;
        }

        var volumes = await SearchVolumesAsync($"isbn:{normalized}", $"ISBN {normalized}", cancellationToken);

        // A query can return other editions; only accept a volume that lists this exact ISBN.
        var volume = volumes?.Items?.FirstOrDefault(v => v.VolumeInfo?.IndustryIdentifiers?
            .Any(i => i.Type is "ISBN_10" or "ISBN_13" && NormalizeIsbn(i.Identifier) == normalized) == true);
        var url = FirstCoverUrl(volume?.VolumeInfo?.ImageLinks);
        if (url == null)
        {
            logger.LogDebug("No Google Books cover for ISBN {Isbn}", normalized);
            return null;
        }
        return NormalizeCoverUrl(url);
    }

    public async Task<string?> GetCoverUrlByTitleAsync(string title, string? subtitle, IReadOnlyList<string> authors,
        CancellationToken cancellationToken = default)
    {
        var wanted = Normalize(title);
        if (wanted.Length == 0)
        {
            return null;
        }
        var query = $"intitle:\"{title}\"" + (authors.FirstOrDefault(a => !string.IsNullOrWhiteSpace(a)) is { } author
            ? $" inauthor:\"{author}\"" : string.Empty);
        var volumes = await SearchVolumesAsync(query, $"title {title}", cancellationToken);
        var volume = volumes?.Items?.FirstOrDefault(v => v.VolumeInfo is { } info
            && TitleMatches(title, subtitle, info) && AuthorsMatch(authors, info));
        var url = FirstCoverUrl(volume?.VolumeInfo?.ImageLinks);
        if (url == null)
        {
            logger.LogDebug("No Google Books cover for title {Title}", title);
            return null;
        }
        return NormalizeCoverUrl(url);
    }

    // Exact title (with or without the subtitle) only. A stored volume number ("Preacher Vol. 1") also accepts
    // the same volume with its own subtitle ("Preacher Vol. 1: Gone to Texas").
    private static bool TitleMatches(string title, string? subtitle, VolumeInfo info)
    {
        var stored = new[] { Normalize(title), Normalize($"{title} {subtitle}") }.Where(t => t.Length > 0).ToHashSet();
        var found = Normalize(info.Title);
        if (stored.Contains(found) || stored.Contains(Normalize($"{info.Title} {info.Subtitle}")))
        {
            return true;
        }
        return Regex.IsMatch(Normalize(title), @"\b(?:vol|volume|book|v) ?\d+$")
            && Normalize($"{info.Title} {info.Subtitle}").StartsWith(Normalize(title) + " ", StringComparison.Ordinal);
    }

    private static bool AuthorsMatch(IReadOnlyList<string> authors, VolumeInfo info)
    {
        var stored = authors.Where(a => !string.IsNullOrWhiteSpace(a)).Select(Normalize).ToList();
        return stored.Count == 0 || info.Authors?.Any(a => stored.Contains(Normalize(a))) == true;
    }

    private static string Normalize(string? value) =>
        Regex.Replace((value ?? string.Empty).ToLowerInvariant(), @"[^\p{L}\p{N}]+", " ").Trim();

    private static string? FirstCoverUrl(Dictionary<string, string>? links) => links == null ? null
        : _imageSizes.Select(size => links.GetValueOrDefault(size)).FirstOrDefault(u => !string.IsNullOrWhiteSpace(u));

    private async Task<VolumesResponse?> SearchVolumesAsync(string query, string description, CancellationToken cancellationToken)
    {
        using var response = await httpClient.GetProviderAsync(
            $"volumes?q={Uri.EscapeDataString(query).Replace("%3A", ":")}&maxResults=10&printType=books", cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
        if (response.StatusCode == HttpStatusCode.Forbidden)
        {
            // Keyless requests get 403 for an exhausted shared daily quota or an unsupported location. Retrying
            // would not help and the cover is only a fallback, so treat it as no cover instead of a retryable failure.
            logger.LogWarning("Google Books refused the cover lookup for {Description} (403 Forbidden)", description);
            return null;
        }
        if (!response.IsSuccessStatusCode)
        {
            throw new ProviderLookupException(LogProviders.GoogleBooks, LookupFailureScope.Classify(response.StatusCode),
                $"Google Books cover lookup failed with status {(int)response.StatusCode}", LookupFailureScope.RetryAt(response));
        }
        try
        {
            return await response.Content.ReadFromJsonAsync<VolumesResponse>(cancellationToken);
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Unreadable Google Books response for {Description}", description);
            return null;
        }
    }

    /// <summary>ISBN-10 or ISBN-13 without separators, upper-case X check digit; null when it is neither.</summary>
    internal static string? NormalizeIsbn(string? isbn)
    {
        var value = string.Concat((isbn ?? string.Empty).Where(c => c is not ('-' or ' '))).ToUpperInvariant();
        return value.Length switch
        {
            13 when value.All(char.IsAsciiDigit) => value,
            10 when value[..9].All(char.IsAsciiDigit) && (char.IsAsciiDigit(value[9]) || value[9] == 'X') => value,
            _ => null
        };
    }

    /// <summary>HTTPS, and without the page-curl effect Google adds to thumbnails.</summary>
    internal static string? NormalizeCoverUrl(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https"))
        {
            return null;
        }
        var query = string.Join('&', uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Where(p => !p.StartsWith("edge=", StringComparison.OrdinalIgnoreCase)));
        return new UriBuilder(uri) { Scheme = Uri.UriSchemeHttps, Port = -1, Query = query }.Uri.AbsoluteUri;
    }

    private sealed record VolumesResponse([property: JsonPropertyName("items")] List<Volume>? Items);

    private sealed record Volume([property: JsonPropertyName("volumeInfo")] VolumeInfo? VolumeInfo);

    private sealed record VolumeInfo(
        [property: JsonPropertyName("title")] string? Title,
        [property: JsonPropertyName("subtitle")] string? Subtitle,
        [property: JsonPropertyName("authors")] List<string>? Authors,
        [property: JsonPropertyName("industryIdentifiers")] List<IndustryIdentifier>? IndustryIdentifiers,
        [property: JsonPropertyName("imageLinks")] Dictionary<string, string>? ImageLinks);

    private sealed record IndustryIdentifier(
        [property: JsonPropertyName("type")] string? Type,
        [property: JsonPropertyName("identifier")] string? Identifier);
}
