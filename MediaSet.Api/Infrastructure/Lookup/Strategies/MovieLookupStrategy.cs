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
            if (searchParams.GetValueOrDefault("confirmedProviderId") is { } collectionProvider
                && collectionProvider.StartsWith("collection:", StringComparison.Ordinal)
                && int.TryParse(collectionProvider[11..], out var collectionId))
            {
                var collection = await _tmdbClient.GetCollectionDetailsAsync(collectionId, cancellationToken);
                var stored = searchParams.GetValueOrDefault("title") ?? string.Empty;
                return collection != null && RetailVideoTitle.IsCollection(stored)
                    && CollectionMatches(collection, stored)
                    ? [MapCollection(collection, stored)] : [];
            }
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

        var identifierValue = searchParams.GetValueOrDefault(identifierType == IdentifierType.Ean ? "ean" : "upc")
            ?? string.Empty;
        var result = await LookupByUpcAsync(identifierValue, cancellationToken, searchParams.GetValueOrDefault("storedTitle"));
        return result != null ? [result] : [];
    }

    private async Task<IReadOnlyList<MovieResponse>> SearchByTitleAsync(
        string title, CancellationToken cancellationToken, bool isTvSeries = false)
    {
        var rawTitle = title;
        title = RetailVideoTitle.Clean(title);
        _logger.LogInformation("Movie title candidate {RawTitle} -> {NormalizedQuery}; rationale {MatchRationale}",
            rawTitle, title, "Known retail noise removed; exact normalized full title required");
        if (string.IsNullOrWhiteSpace(title))
        {
            _logger.LogWarning("Rejected manufacturer-only or empty title {RawTitle}; use stored title or manual artwork", rawTitle);
            return [];
        }
        var video = VideoTitle.Parse(title);
        if (video.Season == null && !isTvSeries && RetailVideoTitle.IsCollection(title))
        {
            var collections = (await SearchCollectionAsync(title, cancellationToken)).ToList();
            // "Complete Collection" can also identify a TV series. Verify both catalogs,
            // but never downgrade a collection release to an individual film.
            if (video.IsSeries) { collections.AddRange(await SearchTvAsync(video, cancellationToken)); }
            return collections;
        }
        var results = new List<MovieResponse>();
        if (!video.IsSeries && !isTvSeries)
        {
            var movies = await _tmdbClient.SearchMovieAsync(title, cancellationToken);
            foreach (var match in movies?.Results.Where(r => SameTitle(r.Title, title)).Take(10) ?? [])
            {
                var details = await _tmdbClient.GetMovieDetailsAsync(match.Id, cancellationToken);
                if (details != null && SameTitle(details.Title, title))
                {
                    _logger.LogInformation("Verified TMDB movie {ProviderId} for {NormalizedQuery}; rationale {MatchRationale}",
                        details.Id, title, "Search and detail titles agree with the full normalized title");
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

    private async Task<IReadOnlyList<MovieResponse>> SearchCollectionAsync(
        string title, CancellationToken cancellationToken)
    {
        var key = RetailVideoTitle.CollectionKey(title);
        var query = System.Text.RegularExpressions.Regex.Replace(title,
            @"\s*[:\-]?\s*(?:the\s+)?(?:complete\s+)?(?:trilogy|quadrilogy|collection)\s*$", "",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase).Trim();
        _logger.LogInformation("Collection query {NormalizedQuery}; rationale {MatchRationale}", query,
            "Collection identity must agree; individual film posters are excluded");
        var matches = await _tmdbClient.SearchCollectionAsync(query, cancellationToken);
        var results = new List<MovieResponse>();
        foreach (var match in matches?.Results.Where(c => RetailVideoTitle.CollectionKey(c.Name) == key).Take(10) ?? [])
        {
            var details = await _tmdbClient.GetCollectionDetailsAsync(match.Id, cancellationToken);
            if (details != null && CollectionMatches(details, title))
            {
                results.Add(MapCollection(details, title));
            }
        }
        _logger.LogInformation("Collection title {Title}: {MatchCount} verified candidates; rationale {MatchRationale}",
            title, results.Count, results.Count == 1 ? "Matched collection artwork; package contents unverified"
                : "Unresolved or ambiguous bundle requires manual artwork; no individual film fallback");
        return results;
    }

    private static MovieResponse MapCollection(TmdbCollectionResponse collection, string title) =>
        new(title, [], [], string.Empty, string.Empty, null, collection.Overview ?? string.Empty, string.Empty,
            string.IsNullOrWhiteSpace(collection.PosterPath) ? null : $"https://image.tmdb.org/t/p/w500{collection.PosterPath}",
            new LookupProvenance("Tmdb", $"collection:{collection.Id}", title));

    private static bool CollectionMatches(TmdbCollectionResponse collection, string title)
    {
        if (RetailVideoTitle.CollectionKey(collection.Name) != RetailVideoTitle.CollectionKey(title)) { return false; }
        var expectedCount = System.Text.RegularExpressions.Regex.IsMatch(title, @"\btrilogy\b",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase) ? 3
            : System.Text.RegularExpressions.Regex.IsMatch(title, @"\bquadrilogy\b",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase) ? 4 : (int?)null;
        return !expectedCount.HasValue || collection.Parts?.Count == expectedCount;
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

    private async Task<MovieResponse?> LookupByUpcAsync(
        string identifierValue, CancellationToken cancellationToken, string? storedTitle = null)
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

        var cleanedTitle = RetailVideoTitle.Clean(firstItem.Title, firstItem.Brand);
        if (VideoTitle.Parse(cleanedTitle).Season == null && RetailVideoTitle.IsCollection(cleanedTitle)
            && !string.IsNullOrWhiteSpace(storedTitle)
            && (!RetailVideoTitle.IsCollection(storedTitle)
                || RetailVideoTitle.CollectionKey(storedTitle) != RetailVideoTitle.CollectionKey(cleanedTitle)))
        {
            throw new RetailBundleException(
                "Retail bundle conflicts with stored title; manual artwork or corrected package metadata required");
        }
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
