using System.Text.RegularExpressions;
using MediaSet.Api.Clients;
using MediaSet.Api.Models;

namespace MediaSet.Api.Services.Lookup;

public partial class MovieLookupStrategy : ILookupStrategy<MovieLookupResponse>
{
    private readonly IProductLookupClient _productLookupClient;
    private readonly IMovieMetadataClient _movieMetadataClient;
    private readonly ILogger<MovieLookupStrategy> _logger;

    [GeneratedRegex(@"\(?(blu-?ray|dvd|4k|uhd|digital|combo|pack|edition|widescreen|full\s+screen)[^\)]*\)?", RegexOptions.IgnoreCase)]
    private static partial Regex FormatMarkerRegex();

    [GeneratedRegex(@"\((\d{4})\)")]
    private static partial Regex YearExtractionRegex();

    public string EntityType => "movies";

    public MovieLookupStrategy(
        IProductLookupClient productLookupClient,
        IMovieMetadataClient movieMetadataClient,
        ILogger<MovieLookupStrategy> logger)
    {
        _productLookupClient = productLookupClient;
        _movieMetadataClient = movieMetadataClient;
        _logger = logger;
    }

    public bool SupportsIdentifierType(string identifierType)
    {
        return identifierType.ToLowerInvariant() switch
        {
            "upc" => true,
            "ean" => true,
            _ => false
        };
    }

    public async Task<MovieLookupResponse?> LookupAsync(string identifierType, string identifierValue, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("MovieLookupStrategy: Looking up {identifierType} {identifierValue}", identifierType, identifierValue);

        // Step 1: Lookup product by barcode
        var productResult = await _productLookupClient.LookupBarcodeAsync(identifierValue, cancellationToken);
        if (productResult == null)
        {
            _logger.LogInformation("MovieLookupStrategy: No product found for barcode {barcode}", identifierValue);
            return null;
        }

        _logger.LogInformation("MovieLookupStrategy: Found product {title}", productResult.Title);

        // Step 2: Normalize title and extract year
        var normalizedTitle = NormalizeTitle(productResult.Title);
        var year = ExtractYear(productResult.Title);

        _logger.LogInformation("MovieLookupStrategy: Normalized title: {title}, year: {year}", normalizedTitle, year);

        // Step 3: Infer format from product info
        var format = InferFormat(productResult.Title, productResult.Category);

        // Step 4: Search TMDb for movie metadata
        var movieMetadata = await _movieMetadataClient.SearchAndGetDetailsAsync(normalizedTitle, year, cancellationToken);
        if (movieMetadata == null)
        {
            _logger.LogWarning("MovieLookupStrategy: No movie metadata found for {title}", normalizedTitle);
            return null;
        }

        _logger.LogInformation("MovieLookupStrategy: Found movie metadata for {title}", movieMetadata.Title);

        // Step 5: Map to MovieLookupResponse
        var response = new MovieLookupResponse(
            Title: movieMetadata.Title,
            Studios: movieMetadata.ProductionCompanies,
            Genres: movieMetadata.Genres,
            ReleaseDate: movieMetadata.ReleaseDate,
            Rating: movieMetadata.Certification ?? string.Empty,
            Runtime: movieMetadata.Runtime,
            Plot: movieMetadata.Overview,
            Format: format,
            IsTvSeries: movieMetadata.IsTvSeries
        );

        return response;
    }

    private static string NormalizeTitle(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return string.Empty;
        }

        // Remove format markers
        var normalized = FormatMarkerRegex().Replace(title, string.Empty);

        // Remove year in parentheses
        normalized = YearExtractionRegex().Replace(normalized, string.Empty);

        // Clean up multiple spaces and trim
        normalized = Regex.Replace(normalized, @"\s+", " ").Trim();

        // Remove trailing dashes or colons
        normalized = normalized.TrimEnd('-', ':', ',').Trim();

        return normalized;
    }

    private static int? ExtractYear(string title)
    {
        var match = YearExtractionRegex().Match(title);
        if (match.Success && int.TryParse(match.Groups[1].Value, out var year))
        {
            return year;
        }
        return null;
    }

    private static string? InferFormat(string title, string? category)
    {
        var lowerTitle = title.ToLowerInvariant();
        var lowerCategory = category?.ToLowerInvariant() ?? string.Empty;

        if (lowerTitle.Contains("4k") || lowerTitle.Contains("uhd") || lowerCategory.Contains("4k"))
        {
            return "4K UHD";
        }

        if (lowerTitle.Contains("blu-ray") || lowerTitle.Contains("bluray") || lowerCategory.Contains("blu-ray"))
        {
            return "Blu-ray";
        }

        if (lowerTitle.Contains("dvd") || lowerCategory.Contains("dvd"))
        {
            return "DVD";
        }

        return null;
    }
}
