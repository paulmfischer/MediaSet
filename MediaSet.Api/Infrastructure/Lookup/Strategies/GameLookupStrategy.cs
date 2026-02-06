using MediaSet.Api.Infrastructure.Lookup.Models;
using MediaSet.Api.Features.Entities.Models;
using MediaSet.Api.Infrastructure.Lookup.Clients.GiantBomb;
using MediaSet.Api.Infrastructure.Lookup.Clients.UpcItemDb;
using System.Text.RegularExpressions;
using Serilog;
using SerilogTracing;

namespace MediaSet.Api.Infrastructure.Lookup.Strategies;

public class GameLookupStrategy : ILookupStrategy<GameResponse>
{
    private readonly IUpcItemDbClient _upcItemDbClient;
    private readonly IGiantBombClient _giantBombClient;
    private readonly ILogger<GameLookupStrategy> _logger;

    private static readonly IdentifierType[] _supportedIdentifierTypes =
    [
        IdentifierType.Upc,
        IdentifierType.Ean
    ];

    public GameLookupStrategy(
        IUpcItemDbClient upcItemDbClient,
        IGiantBombClient giantBombClient,
        ILogger<GameLookupStrategy> logger)
    {
        _upcItemDbClient = upcItemDbClient;
        _giantBombClient = giantBombClient;
        _logger = logger;
    }

    public bool CanHandle(MediaTypes entityType, IdentifierType identifierType)
    {
        return entityType == MediaTypes.Games && _supportedIdentifierTypes.Contains(identifierType);
    }

    public async Task<GameResponse?> LookupAsync(
        IdentifierType identifierType,
        string identifierValue,
        CancellationToken cancellationToken)
    {
        using var activity = Log.Logger.StartActivity("GameLookup {IdentifierType}", new { IdentifierType = identifierType, identifierValue });
        
        _logger.LogInformation("Looking up game with {IdentifierType}: {IdentifierValue}", identifierType, identifierValue);

        var upcResult = await _upcItemDbClient.GetItemByCodeAsync(identifierValue, cancellationToken);
        if (upcResult == null || upcResult.Items.Count == 0)
        {
            _logger.LogWarning("No UPC/EAN data found for code: {Code}", identifierValue);
            return null;
        }

        var firstItem = upcResult.Items[0];
        if (string.IsNullOrWhiteSpace(firstItem.Title))
        {
            _logger.LogWarning("UPC/EAN {Code} data does not contain title", identifierValue);
            return null;
        }

        var (cleanedTitle, edition) = CleanGameTitleAndExtractEdition(firstItem.Title);
        var format = ExtractGameFormat(firstItem.Title);
        var platform = ExtractPlatformFromBarcode(firstItem.Title, firstItem.Category, firstItem.Brand, firstItem.Model);
        _logger.LogInformation("Found title '{RawTitle}' from UPC/EAN {Code}, cleaned to '{CleanedTitle}' (edition '{Edition}'), format '{Format}', platform '{Platform}' for GiantBomb search",
            firstItem.Title, identifierValue, cleanedTitle, edition, format, platform);

        var searchResults = await _giantBombClient.SearchGameAsync(cleanedTitle, cancellationToken);
        if (searchResults == null || searchResults.Count == 0)
        {
            _logger.LogWarning("No GiantBomb results found for title: {Title}", cleanedTitle);
            return null;
        }

        // Find the best match from search results instead of blindly taking the first
        var bestMatch = FindBestMatch(searchResults, cleanedTitle);
        if (bestMatch == null)
        {
            _logger.LogWarning("No suitable GiantBomb match found for title: {Title}", cleanedTitle);
            return null;
        }

        _logger.LogInformation("Best match for '{CleanedTitle}' is '{MatchName}' (id: {MatchId})", 
            cleanedTitle, bestMatch.Name, bestMatch.Id);

        var details = await _giantBombClient.GetGameDetailsAsync(bestMatch.ApiDetailUrl, cancellationToken);
        if (details == null)
        {
            _logger.LogWarning("Could not retrieve GiantBomb game details for: {ApiUrl}", bestMatch.ApiDetailUrl);
            return null;
        }

        // If format is still empty, try to derive it from GiantBomb platform info
        if (string.IsNullOrWhiteSpace(format))
        {
            format = DeriveFormatFromPlatforms(details.Platforms, platform);
            _logger.LogInformation("Format derived from GiantBomb platforms: {Format}", format);
        }

        return MapToGameResponse(details, format, platform, edition);
    }

    internal static (string CleanedTitle, string Edition) CleanGameTitleAndExtractEdition(string rawTitle)
    {
        if (string.IsNullOrWhiteSpace(rawTitle))
        {
            return (string.Empty, string.Empty);
        }

        var title = rawTitle.Trim();

        // Extract edition markers to re-append later: Deluxe, GOTY, Definitive, Collector's, Complete, Ultimate
        var edition = string.Empty;
        var editionMatch = Regex.Match(title, @"(?i)(Deluxe|GOTY|Game of the Year|Definitive|Collector'?s Edition|Complete|Ultimate)");
        if (editionMatch.Success)
        {
            edition = editionMatch.Value;
        }

        // Remove platform indicators commonly found in UPC titles
        title = Regex.Replace(title, @"(?i)\b(PS5|PS4|PlayStation 5|PlayStation 4|PlayStation|Xbox Series X\|S|Xbox Series X|Xbox One|Xbox 360|Xbox|Nintendo Switch|Switch|Wii U|Wii|3DS|DS)\b", string.Empty);

        // Remove format markers like (Cartridge), [Disc], - Disc and trailing dashes
        title = Regex.Replace(title, @"\s*\([^)]*(Disc|Cartridge|Digital)[^)]*\)", string.Empty, RegexOptions.IgnoreCase);
        title = Regex.Replace(title, @"\s*\[[^\]]*(Disc|Cartridge|Digital)[^\]]*\]", string.Empty, RegexOptions.IgnoreCase);
        title = Regex.Replace(title, @"\s*-\s*(Disc|Cartridge|Digital).*$", string.Empty, RegexOptions.IgnoreCase);
    
        // Remove common condition/release type suffixes
        title = Regex.Replace(title, @"\s*-\s*(Pre-Played|Pre-Owned|Used|Greatest Hits|Platinum Hits|Player's Choice|Nintendo Selects|Essentials).*$", string.Empty, RegexOptions.IgnoreCase);
        
        // Remove trailing hyphens left by platform removal
        title = Regex.Replace(title, @"\s*-\s*$", string.Empty);

        // Remove trailing SKU-like codes
        title = Regex.Replace(title, @"\b[A-Z0-9]{3,}-[A-Z0-9]{2,}\b", string.Empty);

        // Remove extra edition words from title for search
        if (!string.IsNullOrEmpty(edition))
        {
            // Also remove "Edition" word if it appears after the edition type
            title = Regex.Replace(title, Regex.Escape(edition) + @"\s*Edition", string.Empty, RegexOptions.IgnoreCase);
            title = Regex.Replace(title, Regex.Escape(edition), string.Empty, RegexOptions.IgnoreCase);
        }

        // Remove leftover parentheses/brackets
        title = Regex.Replace(title, @"\s*\([^)]*\)", string.Empty);
        title = Regex.Replace(title, @"\s*\[[^\]]*\]", string.Empty);

        // Collapse spaces
        title = Regex.Replace(title, @"\s+", " ").Trim();

        return (title, edition);
    }

    internal static string ExtractGameFormat(string rawTitle)
    {
        if (string.IsNullOrWhiteSpace(rawTitle))
        {
            return string.Empty;
        }

        if (Regex.IsMatch(rawTitle, @"(?i)Cartridge")) return "Cartridge";
        if (Regex.IsMatch(rawTitle, @"(?i)Disc|Blu-?ray|DVD")) return "Disc";
        if (Regex.IsMatch(rawTitle, @"(?i)Digital")) return "Digital";
        return string.Empty;
    }

    internal static string ExtractPlatformFromBarcode(string title, string? category, string? brand, string? model)
    {
        // Try title hints
        var titleHints = new (string pattern, string platform)[]
        {
            ("(?i)PS5|PlayStation 5", "PlayStation 5"),
            ("(?i)PS4|PlayStation 4", "PlayStation 4"),
            ("(?i)Xbox Series X\\|S|Series X", "Xbox Series X|S"),
            ("(?i)Xbox One", "Xbox One"),
            ("(?i)Xbox 360", "Xbox 360"),
            ("(?i)Nintendo Switch|Switch", "Nintendo Switch"),
            ("(?i)Wii U", "Wii U"),
            ("(?i)Wii", "Wii"),
            ("(?i)3DS", "Nintendo 3DS"),
            ("(?i)DS", "Nintendo DS"),
        };

        foreach (var (pattern, platform) in titleHints)
        {
            if (Regex.IsMatch(title ?? string.Empty, pattern)) return platform;
        }

        // Try brand/category/model
        var combined = $"{category} {brand} {model}";
        foreach (var (pattern, platform) in titleHints)
        {
            if (Regex.IsMatch(combined, pattern)) return platform;
        }

        return string.Empty;
    }

    internal static string DeriveFormatFromPlatforms(List<GiantBombPlatformRef>? platforms, string detectedPlatform)
    {
        if (platforms == null || platforms.Count == 0)
        {
            return string.Empty;
        }

        // Check if the detected platform (from UPC) matches any of the GiantBomb platforms
        // If so, derive the format based on that platform's typical media format
        var matchingPlatform = platforms.FirstOrDefault(p =>
            p.Name.Contains(detectedPlatform, StringComparison.OrdinalIgnoreCase) ||
            detectedPlatform.Contains(p.Name, StringComparison.OrdinalIgnoreCase));

        if (matchingPlatform == null)
        {
            // No match, try to use the first platform
            matchingPlatform = platforms[0];
        }

        // Map platforms to typical formats
        var platformName = matchingPlatform.Name.ToLowerInvariant();
        
        // GD-ROM (Dreamcast's proprietary format) - check first before CD-ROM
        if (platformName.Contains("dreamcast"))
        {
            return "GD-ROM";
        }
        
        // Cartridge-based platforms
        if (platformName.Contains("nintendo switch") || platformName.Contains("switch") ||
            platformName.Contains("3ds") || platformName.Contains("ds") ||
            platformName.Contains("game boy") || platformName.Contains("gameboy") ||
            platformName.Contains("n64") || platformName.Contains("snes") || platformName.Contains("nes") ||
            platformName.Contains("genesis") || platformName.Contains("game gear"))
        {
            return "Cartridge";
        }

        // Blu-ray Disc platforms (PS3, PS4, PS5, Xbox One, Xbox Series X)
        if (platformName.Contains("playstation 5") || platformName.Contains("ps5") ||
            platformName.Contains("playstation 4") || platformName.Contains("ps4") ||
            platformName.Contains("xbox series") || platformName.Contains("xbox one"))
        {
            return "Blu-ray Disc";
        }

        // DVD platforms (PS2, Xbox, Xbox 360, Wii, Wii U)
        if (platformName.Contains("playstation 3") || platformName.Contains("ps3") ||
            platformName.Contains("playstation 2") || platformName.Contains("ps2") ||
            platformName.Contains("xbox 360") || platformName.Contains("xbox") ||
            platformName.Contains("wii"))
        {
            return "DVD";
        }

        // CD-ROM platforms (PS1, Saturn, older PC games)
        if (platformName.Contains("playstation") && !platformName.Contains("playstation 2") && 
            !platformName.Contains("playstation 3") && !platformName.Contains("playstation 4") && 
            !platformName.Contains("playstation 5") ||
            platformName.Contains("saturn") ||
            platformName.Contains("sega cd"))
        {
            return "CD-ROM";
        }

        // Digital platforms
        if (platformName.Contains("eshop") || platformName.Contains("digital") ||
            platformName.Contains("download"))
        {
            return "Nintendo eShop";
        }

        // PC - default to CD-ROM for older games, but could be DVD or digital
        // This is imperfect without release date, but CD-ROM is most common for physical PC
        if (platformName.Contains("pc") || platformName.Contains("windows") || 
            platformName.Contains("mac") || platformName.Contains("linux"))
        {
            return "CD-ROM";
        }

        // Default to DVD for unknown platforms (most common modern physical format before Blu-ray)
        return "DVD";
    }

    internal static GiantBombSearchResult? FindBestMatch(List<GiantBombSearchResult> searchResults, string cleanedTitle)
    {
        if (searchResults.Count == 0)
        {
            return null;
        }

        if (searchResults.Count == 1)
        {
            return searchResults[0];
        }

        // Score each result based on how well it matches the cleaned title
        var scored = searchResults.Select(result => new
        {
            Result = result,
            Score = CalculateMatchScore(result.Name, cleanedTitle)
        }).OrderByDescending(x => x.Score).ToList();

        // Return the best match if it has a reasonable score (at least 0.5 similarity)
        var best = scored.First();
        return best.Score >= 0.5 ? best.Result : searchResults[0]; // Fall back to first if no good match
    }

    private static double CalculateMatchScore(string resultName, string searchTitle)
    {
        if (string.IsNullOrWhiteSpace(resultName) || string.IsNullOrWhiteSpace(searchTitle))
        {
            return 0.0;
        }

        var result = resultName.ToLowerInvariant();
        var search = searchTitle.ToLowerInvariant();

        // Exact match
        if (result == search)
        {
            return 1.0;
        }

        // Contains the entire search term
        if (result.Contains(search))
        {
            return 0.9;
        }

        // Word overlap scoring
        var resultWords = result.Split(new[] { ' ', '-', ':', '.' }, StringSplitOptions.RemoveEmptyEntries);
        var searchWords = search.Split(new[] { ' ', '-', ':', '.' }, StringSplitOptions.RemoveEmptyEntries);

        var matchingWords = searchWords.Count(sw => resultWords.Any(rw => rw.Contains(sw) || sw.Contains(rw)));
        var wordScore = searchWords.Length > 0 ? (double)matchingWords / searchWords.Length : 0.0;

        return wordScore;
    }

    private static GameResponse MapToGameResponse(GiantBombGameDetails details, string format, string platform, string edition)
    {
        var genres = details.Genres?.Select(g => g.Name).ToList() ?? new List<string>();
        var developers = details.Developers?.Select(d => d.Name).ToList() ?? new List<string>();
        var publishers = details.Publishers?.Select(p => p.Name).ToList() ?? new List<string>();

        var rating = string.Empty;
        if (details.Ratings != null && details.Ratings.Count > 0)
        {
            // Prefer ESRB if present
            var esrb = details.Ratings.FirstOrDefault(r => r.Name.Contains("ESRB", StringComparison.OrdinalIgnoreCase));
            rating = esrb?.Name ?? details.Ratings[0].Name;
        }

        var releaseDate = details.OriginalReleaseDate ?? string.Empty;
        var description = details.Deck ?? string.Empty;
        var imageUrl = details.Image?.SuperUrl ?? details.Image?.MediumUrl ?? details.Image?.SmallUrl;

        var title = details.Name;
        if (!string.IsNullOrEmpty(edition))
        {
            title = $"{title} ({edition})";
        }

        return new GameResponse(
            Title: title,
            Platform: platform,
            Genres: genres,
            Developers: developers,
            Publishers: publishers,
            ReleaseDate: releaseDate,
            Rating: rating,
            Description: description,
            Format: format,
            ImageUrl: imageUrl
        );
    }
}
