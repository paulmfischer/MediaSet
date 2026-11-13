using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;

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

        using var response = await httpClient.GetProviderAsync(
            $"volumes?q=isbn:{normalized}&maxResults=5&printType=books", cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
        if (!response.IsSuccessStatusCode)
        {
            throw new ProviderLookupException("GoogleBooks", LookupFailureScope.Classify(response.StatusCode),
                $"Google Books cover lookup failed with status {(int)response.StatusCode}", LookupFailureScope.RetryAt(response));
        }

        VolumesResponse? volumes;
        try
        {
            volumes = await response.Content.ReadFromJsonAsync<VolumesResponse>(cancellationToken);
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Unreadable Google Books response for ISBN {Isbn}", normalized);
            return null;
        }

        // A query can return other editions; only accept a volume that lists this exact ISBN.
        var volume = volumes?.Items?.FirstOrDefault(v => v.VolumeInfo?.IndustryIdentifiers?
            .Any(i => i.Type is "ISBN_10" or "ISBN_13" && NormalizeIsbn(i.Identifier) == normalized) == true);
        var links = volume?.VolumeInfo?.ImageLinks;
        var url = links == null ? null : _imageSizes.Select(size => links.GetValueOrDefault(size)).FirstOrDefault(u => !string.IsNullOrWhiteSpace(u));
        if (url == null)
        {
            logger.LogDebug("No Google Books cover for ISBN {Isbn}", normalized);
            return null;
        }
        return NormalizeCoverUrl(url);
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
        [property: JsonPropertyName("industryIdentifiers")] List<IndustryIdentifier>? IndustryIdentifiers,
        [property: JsonPropertyName("imageLinks")] Dictionary<string, string>? ImageLinks);

    private sealed record IndustryIdentifier(
        [property: JsonPropertyName("type")] string? Type,
        [property: JsonPropertyName("identifier")] string? Identifier);
}
