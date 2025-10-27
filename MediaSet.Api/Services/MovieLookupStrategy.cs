using MediaSet.Api.Clients;
using MediaSet.Api.Models;

namespace MediaSet.Api.Services;

public class MovieLookupStrategy : ILookupStrategy<MovieResponse>
{
    private readonly IUpcItemDbClient _upcItemDbClient;
    private readonly ITmdbClient _tmdbClient;
    private readonly ILogger<MovieLookupStrategy> _logger;

    private static readonly IdentifierType[] _supportedIdentifierTypes = 
    [
        IdentifierType.Upc,
        IdentifierType.Ean
    ];

    public MovieLookupStrategy(
        IUpcItemDbClient upcItemDbClient,
        ITmdbClient tmdbClient,
        ILogger<MovieLookupStrategy> logger)
    {
        _upcItemDbClient = upcItemDbClient;
        _tmdbClient = tmdbClient;
        _logger = logger;
    }

    public bool CanHandle(MediaTypes entityType, IdentifierType identifierType)
    {
        return entityType == MediaTypes.Movies && _supportedIdentifierTypes.Contains(identifierType);
    }

    public async Task<MovieResponse?> LookupAsync(
        IdentifierType identifierType, 
        string identifierValue, 
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Looking up movie with {IdentifierType}: {IdentifierValue}", 
            identifierType, identifierValue);

        var upcResult = await _upcItemDbClient.GetItemByCodeAsync(identifierValue, cancellationToken);
        
        if (upcResult == null || upcResult.Items.Count == 0)
        {
            _logger.LogWarning("No UPC/EAN data found for code: {Code}", identifierValue);
            return null;
        }

        var firstItem = upcResult.Items[0];
        
        if (string.IsNullOrEmpty(firstItem.Title))
        {
            _logger.LogWarning("UPC/EAN {Code} data does not contain title", identifierValue);
            return null;
        }

        var cleanedTitle = CleanMovieTitle(firstItem.Title);
        _logger.LogInformation("Found title '{RawTitle}' from UPC/EAN {Code}, cleaned to '{CleanedTitle}' for TMDB search", 
            firstItem.Title, identifierValue, cleanedTitle);

        var searchResult = await _tmdbClient.SearchMovieAsync(cleanedTitle, cancellationToken);
        
        if (searchResult == null || searchResult.Results.Count == 0)
        {
            _logger.LogWarning("No TMDB results found for title: {Title}", firstItem.Title);
            return null;
        }

        var firstMovie = searchResult.Results[0];
        _logger.LogInformation("Found TMDB movie ID {MovieId} for title: {Title}", 
            firstMovie.Id, firstItem.Title);

        var movieDetails = await _tmdbClient.GetMovieDetailsAsync(firstMovie.Id, cancellationToken);
        
        if (movieDetails == null)
        {
            _logger.LogWarning("Could not retrieve TMDB movie details for ID: {MovieId}", firstMovie.Id);
            return null;
        }

        return MapToMovieResponse(movieDetails);
    }

    private MovieResponse MapToMovieResponse(TmdbMovieResponse tmdbMovie)
    {
        var genres = tmdbMovie.Genres.Select(g => g.Name).ToList();
        var studios = tmdbMovie.ProductionCompanies.Select(c => c.Name).ToList();
        var rating = tmdbMovie.VoteAverage > 0 ? $"{tmdbMovie.VoteAverage:F1}/10" : string.Empty;

        return new MovieResponse(
            Title: tmdbMovie.Title,
            Genres: genres,
            Studios: studios,
            ReleaseDate: tmdbMovie.ReleaseDate ?? string.Empty,
            Rating: rating,
            Runtime: tmdbMovie.Runtime,
            Plot: tmdbMovie.Overview ?? string.Empty
        );
    }

    /// <summary>
    /// Cleans movie title from UPCitemdb by removing common format suffixes and edition details.
    /// Examples:
    /// - "1408 (Two-Disc Collector's Edition)" -> "1408"
    /// - "1408 [2 Discs] [Collector's Edition]" -> "1408"
    /// - "1408 - DVD" -> "1408"
    /// - "The Matrix (DVD)" -> "The Matrix"
    /// </summary>
    private static string CleanMovieTitle(string rawTitle)
    {
        if (string.IsNullOrWhiteSpace(rawTitle))
        {
            return string.Empty;
        }

        var title = rawTitle.Trim();

        // Remove content in parentheses at the end (e.g., "(DVD)", "(Two-Disc Edition)")
        title = System.Text.RegularExpressions.Regex.Replace(title, @"\s*\([^)]*\)\s*$", string.Empty);

        // Remove content in square brackets at the end (e.g., "[2 Discs]", "[Collector's Edition]")
        title = System.Text.RegularExpressions.Regex.Replace(title, @"\s*\[[^\]]*\]\s*$", string.Empty);

        // Remove common suffixes like " - DVD", " - Blu-ray", " - 4K", etc.
        title = System.Text.RegularExpressions.Regex.Replace(title, @"\s*-\s*(DVD|Blu-?ray|4K|BD|UHD|Digital|HD).*$", string.Empty, System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        // Recursively clean in case there are multiple patterns (e.g., "Title [Edition] (DVD)")
        var cleaned = title.Trim();
        if (cleaned != rawTitle && (cleaned.EndsWith(')') || cleaned.EndsWith(']') || cleaned.Contains(" - ")))
        {
            return CleanMovieTitle(cleaned);
        }

        return cleaned;
    }
}
