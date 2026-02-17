using MediaSet.Api.Infrastructure.Lookup.Models;
using MediaSet.Api.Shared.Models;
using MediaSet.Api.Infrastructure.Lookup.Clients.Tmdb;
using MediaSet.Api.Infrastructure.Lookup.Clients.UpcItemDb;
using Serilog;
using SerilogTracing;

namespace MediaSet.Api.Infrastructure.Lookup.Strategies;

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
        using var activity = Log.Logger.StartActivity("MovieLookup {IdentifierType}", new { IdentifierType = identifierType, identifierValue });
        
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

        var cleanedTitle = CleanMovieTitle(firstItem.Title, firstItem.Brand);
        var format = ExtractMovieFormat(firstItem.Title);
        _logger.LogInformation("Found title '{RawTitle}' from UPC/EAN {Code}, cleaned to '{CleanedTitle}' with format '{Format}' for TMDB search", 
            firstItem.Title, identifierValue, cleanedTitle, format);

        var searchResult = await _tmdbClient.SearchMovieAsync(cleanedTitle, cancellationToken);
        
        if (searchResult == null || searchResult.Results.Count == 0)
        {
            _logger.LogWarning("No TMDB results found for title: {Title}", cleanedTitle);
            return null;
        }

        // Find the best match by comparing titles, prioritizing exact matches
        var bestMatch = searchResult.Results.FirstOrDefault(r => 
            r.Title.Equals(cleanedTitle, StringComparison.OrdinalIgnoreCase)) 
            ?? searchResult.Results[0];
        
        _logger.LogInformation("Found TMDB movie ID {MovieId} for title: {Title}", 
            bestMatch.Id, cleanedTitle);

        var movieDetails = await _tmdbClient.GetMovieDetailsAsync(bestMatch.Id, cancellationToken);
        
        if (movieDetails == null)
        {
            _logger.LogWarning("Could not retrieve TMDB movie details for ID: {MovieId}", bestMatch.Id);
            return null;
        }

        return MapToMovieResponse(movieDetails, format);
    }

    private MovieResponse MapToMovieResponse(TmdbMovieResponse tmdbMovie, string format = "")
    {
        var genres = tmdbMovie.Genres.Select(g => g.Name).ToList();
        var studios = tmdbMovie.ProductionCompanies.Select(c => c.Name).ToList();
        var rating = tmdbMovie.VoteAverage > 0 ? $"{tmdbMovie.VoteAverage:F1}/10" : string.Empty;
        var imageUrl = !string.IsNullOrEmpty(tmdbMovie.PosterPath) 
            ? $"https://image.tmdb.org/t/p/w500{tmdbMovie.PosterPath}"
            : null;

        return new MovieResponse(
            Title: tmdbMovie.Title,
            Genres: genres,
            Studios: studios,
            ReleaseDate: tmdbMovie.ReleaseDate ?? string.Empty,
            Rating: rating,
            Runtime: tmdbMovie.Runtime,
            Plot: tmdbMovie.Overview ?? string.Empty,
            Format: format,
            ImageUrl: imageUrl
        );
    }

    /// <summary>
    /// Cleans movie title from UPCitemdb by removing brand prefixes, format suffixes,
    /// edition details, and trailing junk text.
    /// Examples:
    /// - "1408 (Two-Disc Collector's Edition)" -> "1408"
    /// - "Paramount - Beverly Hills Cop [DIGITAL VIDEO DISC]" (brand: Paramount) -> "Beverly Hills Cop"
    /// - "Beverly Hills Ninja (DVD) Sony Pictures Comedy" -> "Beverly Hills Ninja"
    /// - "300 (BD) [Blu-ray] Feature Action Science Fiction" -> "300"
    /// </summary>
    private static string CleanMovieTitle(string rawTitle, string? brand = null)
    {
        if (string.IsNullOrWhiteSpace(rawTitle))
        {
            return string.Empty;
        }

        var title = rawTitle.Trim();

        // Remove brand as leading prefix (e.g., "Paramount - Beverly Hills Cop" or "Lions Gate : Title")
        if (!string.IsNullOrWhiteSpace(brand))
        {
            title = System.Text.RegularExpressions.Regex.Replace(title, @"^" + System.Text.RegularExpressions.Regex.Escape(brand) + @"\s*[-:]\s*", string.Empty, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        }

        // Remove common trailing words like NEW, USED, SEALED, etc.
        title = System.Text.RegularExpressions.Regex.Replace(title, @"\s+(NEW|USED|SEALED|MINT|OPENED|UNOPENED|LIKE NEW)\s*$", string.Empty, System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        // Truncate at first ( or [ if there's content before it
        var bracketIndex = title.IndexOfAny(['(', '[']);
        if (bracketIndex > 0)
        {
            title = title[..bracketIndex];
        }

        // Truncate at comma followed by disc-count keywords (e.g., ", Two Disc Deluxe Edition, Hologram Cover")
        title = System.Text.RegularExpressions.Regex.Replace(title, @",\s+(?:\d+|Two|Three|Four|Five|Six)\s+Discs?\b.*$", string.Empty, System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        // Remove format combinations with + (e.g., "Blue-Ray + DVD", "Blu-ray + Digital")
        title = System.Text.RegularExpressions.Regex.Replace(title, @"\s*(Blu-?ray|Blue-?ray|DVD|4K|BD|UHD|Digital|HD)\s*\+\s*(Blu-?ray|Blue-?ray|DVD|4K|BD|UHD|Digital|HD).*$", string.Empty, System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        // Remove trailing brand text
        if (!string.IsNullOrWhiteSpace(brand))
        {
            title = System.Text.RegularExpressions.Regex.Replace(title, @"\s+" + System.Text.RegularExpressions.Regex.Escape(brand) + @"\s*$", string.Empty, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        }

        // Remove common suffixes like " - DVD", " - Blu-ray", " - 4K", etc.
        title = System.Text.RegularExpressions.Regex.Replace(title, @"\s*-\s*(DVD|Blu-?ray|4K|BD|UHD|Digital|HD|DIGITAL VIDEO DISC).*$", string.Empty, System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        // Remove format keywords at end (e.g., 'DVD', 'Blu-ray', etc.)
        title = System.Text.RegularExpressions.Regex.Replace(title, @"\s*(DVD|Blu-?ray|Blue-?ray|4K|BD|UHD|Digital|HD)\s*$", string.Empty, System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        // Remove trailing disc-count edition phrases (e.g., "Two-Disc Diamond Edition")
        title = System.Text.RegularExpressions.Regex.Replace(title, @"\s+(?:\d+-?Discs?|Two-?Discs?|Three-?Discs?|Four-?Discs?|Five-?Discs?|Six-?Discs?)\b.*$", string.Empty, System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        // Remove trailing "Complete Collection"
        title = System.Text.RegularExpressions.Regex.Replace(title, @"\s+Complete\s+Collection\s*$", string.Empty, System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        // Clean up multiple spaces and trim
        title = System.Text.RegularExpressions.Regex.Replace(title, @"\s+", " ").Trim();

        // Move articles from end to beginning
        // Examples: "Scanner Darkly A" -> "A Scanner Darkly", "Matrix The" -> "The Matrix"
        var articleMatch = System.Text.RegularExpressions.Regex.Match(title, @"^(.+?)\s+(A|The)$", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (articleMatch.Success)
        {
            var mainTitle = articleMatch.Groups[1].Value.Trim();
            var article = articleMatch.Groups[2].Value;
            title = $"{article} {mainTitle}";
        }

        return title;
    }

    /// <summary>
    /// Extracts the movie format (DVD, Blu-ray, 4K, etc.) from the UPCitemdb title.
    /// Examples:
    /// - "1408 (Two-Disc Collector's Edition)" -> ""
    /// - "1408 - DVD" -> "DVD"
    /// - "The Matrix (Blu-ray)" -> "Blu-ray"
    /// - "Akira [4K UHD]" -> "4K UHD"
    /// - "Movie Title - Blu-ray 4K" -> "Blu-ray 4K"
    /// </summary>
    private static string ExtractMovieFormat(string rawTitle)
    {
        if (string.IsNullOrWhiteSpace(rawTitle))
        {
            return string.Empty;
        }

        var formats = new List<string>();

        // Check for format in parentheses
        var parenMatch = System.Text.RegularExpressions.Regex.Match(rawTitle, @"\((.*?(DVD|Blu-?ray|4K|BD|UHD|Digital|HD).*?)\)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (parenMatch.Success)
        {
            formats.Add(parenMatch.Groups[1].Value.Trim());
        }

        // Check for format in square brackets
        var bracketMatch = System.Text.RegularExpressions.Regex.Match(rawTitle, @"\[(.*?(DVD|Blu-?ray|4K|BD|UHD|Digital|HD).*?)\]", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (bracketMatch.Success)
        {
            formats.Add(bracketMatch.Groups[1].Value.Trim());
        }

        // Check for format after dash
        var dashMatch = System.Text.RegularExpressions.Regex.Match(rawTitle, @"-\s*(DVD|Blu-?ray|4K|BD|UHD|Digital|HD).*$", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (dashMatch.Success)
        {
            formats.Add(dashMatch.Groups[0].Value.TrimStart('-').Trim());
        }

        // Check for format at the end of the title (e.g., '30 Days of Night DVD')
        var endMatch = System.Text.RegularExpressions.Regex.Match(rawTitle, @"\b(DVD|Blu-?ray|4K|BD|UHD|Digital|HD)\b\s*$", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (endMatch.Success)
        {
            formats.Add(endMatch.Groups[1].Value.Trim());
        }

        // Return the first found format, normalized
        if (formats.Count > 0)
        {
            var format = formats[0];
            // Normalize common variations
            format = System.Text.RegularExpressions.Regex.Replace(format, @"\bBlu-?ray\b", "Blu-ray", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            format = System.Text.RegularExpressions.Regex.Replace(format, @"\bBD\b", "Blu-ray", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            format = System.Text.RegularExpressions.Regex.Replace(format, @"\bUHD\b", "4K UHD", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            return format;
        }

        return string.Empty;
    }
}
