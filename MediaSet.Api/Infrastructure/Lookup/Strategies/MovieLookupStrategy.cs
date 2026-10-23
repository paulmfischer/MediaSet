using MediaSet.Api.Infrastructure.Lookup.Models;
using MediaSet.Api.Shared.Models;
using MediaSet.Api.Infrastructure.Lookup.Clients.Tmdb;
using MediaSet.Api.Infrastructure.Lookup.Clients.UpcItemDb;
using Serilog;
using SerilogTracing;

namespace MediaSet.Api.Infrastructure.Lookup.Strategies;

public class MovieLookupStrategy : LookupStrategyBase<Movie, MovieResponse>
{
    private readonly IUpcItemDbClient _upcItemDbClient;
    private readonly ITmdbClient _tmdbClient;
    private readonly ILogger<MovieLookupStrategy> _logger;

    private static readonly IdentifierType[] _supportedIdentifierTypes =
    [
        IdentifierType.Upc,
        IdentifierType.Ean,
        IdentifierType.Entity
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

    public override bool CanHandle(MediaTypes entityType, IdentifierType identifierType)
    {
        return entityType == MediaTypes.Movies && _supportedIdentifierTypes.Contains(identifierType);
    }

    public override async Task<IReadOnlyList<MovieResponse>> LookupAsync(
        IdentifierType identifierType,
        IReadOnlyDictionary<string, string> searchParams,
        CancellationToken cancellationToken)
    {
        using var activity = Log.Logger.StartActivity("MovieLookup {IdentifierType}", new { IdentifierType = identifierType });

        _logger.LogInformation("Looking up movie with {IdentifierType}", identifierType);

        if (identifierType == IdentifierType.Entity)
        {
            if (searchParams.TryGetValue("confirmedProviderId", out var providerId) && int.TryParse(providerId, out var id))
            {
                var details = await _tmdbClient.GetMovieDetailsAsync(id, cancellationToken);
                return details == null ? [] : [MapToMovieResponse(details)];
            }
            if (searchParams.TryGetValue("confirmedProviderId", out providerId)
                && providerId.StartsWith("tv:", StringComparison.Ordinal))
            {
                return await GetConfirmedTvAsync(providerId, searchParams.GetValueOrDefault("title"), cancellationToken);
            }
            searchParams.TryGetValue("title", out var title);
            return await SearchByTitleAsync(title ?? string.Empty, cancellationToken,
                searchParams.GetValueOrDefault("isTvSeries") == "true");
        }

        var identifierValue = searchParams.Values.FirstOrDefault() ?? string.Empty;
        var result = await LookupByUpcAsync(identifierValue, cancellationToken);
        return result != null ? [result] : [];
    }

    private async Task<IReadOnlyList<MovieResponse>> SearchByTitleAsync(
        string title, CancellationToken cancellationToken, bool isTvSeries = false)
    {
        var video = VideoTitle.Parse(title);
        var results = new List<MovieResponse>();
        if (!video.IsSeries && !isTvSeries)
        {
            var movies = await _tmdbClient.SearchMovieAsync(title, cancellationToken);
            foreach (var match in movies?.Results.Where(r => SameTitle(r.Title, title)).Take(10) ?? [])
            {
                var details = await _tmdbClient.GetMovieDetailsAsync(match.Id, cancellationToken);
                if (details != null && SameTitle(details.Title, title))
                {
                    results.Add(MapToMovieResponse(details));
                }
            }
        }
        // Punctuation may belong to a series name or separate a named special.
        // Verify both interpretations instead of assuming that the prefix is the series.
        if (video.Special != null)
        {
            results.AddRange(await SearchTvAsync(new VideoTitle(title, title), cancellationToken));
        }
        results.AddRange(await SearchTvAsync(video, cancellationToken));
        return results;
    }

    private async Task<IReadOnlyList<MovieResponse>> SearchTvAsync(
        VideoTitle video, CancellationToken cancellationToken)
    {
        var results = new List<MovieResponse>();
        var shows = await _tmdbClient.SearchTvAsync(video.SeriesTitle, cancellationToken);
        foreach (var match in shows?.Results.Where(r => SameTitle(r.Name, video.SeriesTitle)).Take(10) ?? [])
        {
            var series = await _tmdbClient.GetTvDetailsAsync(match.Id, cancellationToken);
            if (series == null || !SameTitle(series.Name, video.SeriesTitle)) { continue; }
            var seasonNumber = video.Special != null ? 0 : video.Season;
            var season = seasonNumber.HasValue
                ? await _tmdbClient.GetSeasonDetailsAsync(series.Id, seasonNumber.Value, cancellationToken) : null;
            // A missing season is not evidence that this is the requested release.
            if (seasonNumber.HasValue && season?.SeasonNumber != seasonNumber.Value) { continue; }
            if (video.Special != null)
            {
                foreach (var episode in season?.Episodes?.Where(e => SameTitle(e.Name, video.Special)) ?? [])
                {
                    results.Add(MapTvResponse(series, video.Title, season, episode));
                }
            }
            else { results.Add(MapTvResponse(series, video.Title, season, null)); }
        }
        return results;
    }

    private static bool SameTitle(string first, string second) =>
        VideoTitle.Normalize(first) == VideoTitle.Normalize(second);

    private async Task<IReadOnlyList<MovieResponse>> GetConfirmedTvAsync(
        string providerId, string? storedTitle, CancellationToken cancellationToken)
    {
        var parts = providerId.Split(':');
        if (parts.Length is < 2 or > 4 || !int.TryParse(parts[1], out var seriesId)) { return []; }
        var series = await _tmdbClient.GetTvDetailsAsync(seriesId, cancellationToken);
        if (series == null) { return []; }
        TmdbSeasonResponse? season = null;
        TmdbEpisode? episode = null;
        if (parts.Length >= 3)
        {
            if (!int.TryParse(parts[2], out var number)) { return []; }
            season = await _tmdbClient.GetSeasonDetailsAsync(seriesId, number, cancellationToken);
            if (season?.SeasonNumber != number) { return []; }
        }
        if (parts.Length == 4)
        {
            if (!int.TryParse(parts[3], out var number)) { return []; }
            episode = season?.Episodes?.SingleOrDefault(e => e.EpisodeNumber == number);
            if (episode == null) { return []; }
        }
        var title = episode != null ? $"{series.Name}: {episode.Name}"
            : season != null ? $"{series.Name}: Season {season.SeasonNumber}" : series.Name;
        if (!string.IsNullOrWhiteSpace(storedTitle))
        {
            var video = VideoTitle.Parse(storedTitle);
            if (season == null && SameTitle(storedTitle, series.Name)) { title = storedTitle; }
            else if (!SameTitle(video.SeriesTitle, series.Name)
                || (episode != null ? !SameTitle(video.Special ?? string.Empty, episode.Name)
                    : video.Season != season?.SeasonNumber)) { return []; }
            title = storedTitle;
        }
        return [MapTvResponse(series, title, season, episode)];
    }

    private static MovieResponse MapTvResponse(
        TmdbTvResponse series, string title, TmdbSeasonResponse? season, TmdbEpisode? episode)
    {
        // Season art wins; a verified season/special without art explicitly falls back to series art.
        var poster = !string.IsNullOrWhiteSpace(season?.PosterPath) ? season.PosterPath : series.PosterPath;
        var providerId = $"tv:{series.Id}" + (season != null ? $":{season.SeasonNumber}" : string.Empty)
            + (episode != null ? $":{episode.EpisodeNumber}" : string.Empty);
        return new(title, series.Genres?.Select(g => g.Name).ToList() ?? [],
            series.ProductionCompanies?.Select(c => c.Name).ToList() ?? [],
            episode?.AirDate ?? season?.AirDate ?? series.FirstAirDate ?? string.Empty,
            string.Empty, null, series.Overview ?? string.Empty, string.Empty,
            string.IsNullOrWhiteSpace(poster) ? null : $"https://image.tmdb.org/t/p/w500{poster}",
            new LookupProvenance("Tmdb", providerId, title, MatchYears:
                new[] { series.FirstAirDate, season?.AirDate, episode?.AirDate }
                    .Where(d => !string.IsNullOrWhiteSpace(d) && d.Length >= 4)
                    .Select(d => d![..4]).Distinct().ToList()));
    }

    private async Task<MovieResponse?> LookupByUpcAsync(string identifierValue, CancellationToken cancellationToken)
    {
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

        var candidates = await SearchByTitleAsync(cleanedTitle, cancellationToken);
        if (candidates.Count != 1)
        {
            _logger.LogWarning("No unambiguous TMDB match found for title: {Title}", cleanedTitle);
            return null;
        }
        return candidates[0] with { Format = format };
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
            ImageUrl: imageUrl,
            Provenance: new LookupProvenance("Tmdb", tmdbMovie.Id.ToString(), tmdbMovie.Title)
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
