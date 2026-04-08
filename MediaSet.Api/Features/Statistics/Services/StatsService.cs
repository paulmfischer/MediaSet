using MediaSet.Api.Features.Statistics.Models;
using MediaSet.Api.Shared.Models;

using MediaSet.Api.Infrastructure.Caching;
using MediaSet.Api.Infrastructure.DataAccess;
using Microsoft.Extensions.Options;
using Serilog;
using SerilogTracing;
using System.Text.RegularExpressions;

namespace MediaSet.Api.Features.Statistics.Services;

public class StatsService : IStatsService
{
    private readonly IEntityService<Book> bookService;
    private readonly IEntityService<Movie> movieService;
    private readonly IEntityService<Game> gameService;
    private readonly IEntityService<Music> musicService;
    private readonly ICacheService cacheService;
    private readonly CacheSettings cacheSettings;
    private readonly ILogger<StatsService> logger;

    public StatsService(
        IEntityService<Book> _bookService,
        IEntityService<Movie> _movieService,
        IEntityService<Game> _gameService,
        IEntityService<Music> _musicService,
        ICacheService _cacheService,
        IOptions<CacheSettings> _cacheSettings,
        ILogger<StatsService> _logger)
    {
        bookService = _bookService;
        movieService = _movieService;
        gameService = _gameService;
        musicService = _musicService;
        cacheService = _cacheService;
        cacheSettings = _cacheSettings.Value;
        logger = _logger;
    }

    private static int? ParseYear(string? dateStr)
    {
        if (string.IsNullOrWhiteSpace(dateStr))
        {
            return null;
        }

        var match = Regex.Match(dateStr, @"\b(19|20)\d{2}\b");
        if (match.Success && int.TryParse(match.Value, out var year))
        {
            return year;
        }

        return null;
    }

    private static IReadOnlyDictionary<string, int> ToDecadeBreakdown(IEnumerable<int> years)
    {
        return years
            .GroupBy(y => y / 10 * 10)
            .OrderBy(g => g.Key)
            .ToDictionary(g => $"{g.Key}s", g => g.Count());
    }

    private static IEnumerable<NameCount> TopN(Dictionary<string, int> breakdown, int n = 10)
    {
        return breakdown
            .OrderByDescending(kv => kv.Value)
            .Take(n)
            .Select(kv => new NameCount(kv.Key, kv.Value));
    }

    public async Task<Stats> GetMediaStatsAsync(CancellationToken cancellationToken = default)
    {
        using var activity = Log.Logger.StartActivity("GetMediaStats");

        const string cacheKey = "stats";

        var cachedStats = await cacheService.GetAsync<Stats>(cacheKey);
        if (cachedStats != null)
        {
            logger.LogDebug("Returning cached statistics");
            return cachedStats;
        }

        logger.LogDebug("Cache miss for statistics, calculating from database");

        var bookTask = bookService.GetListAsync(cancellationToken);
        var movieTask = movieService.GetListAsync(cancellationToken);
        var gameTask = gameService.GetListAsync(cancellationToken);
        var musicTask = musicService.GetListAsync(cancellationToken);
        await Task.WhenAll(bookTask, movieTask, gameTask, musicTask);

        var books = bookTask.Result;
        var movies = movieTask.Result;
        var games = gameTask.Result;
        var musics = musicTask.Result;

        // ── Books ────────────────────────────────────────────────────────────
        var bookFormats = books.Where(b => !string.IsNullOrWhiteSpace(b.Format)).Select(b => b.Format.Trim()).Distinct();
        var bookFormatBreakdown = books
            .Where(b => !string.IsNullOrWhiteSpace(b.Format))
            .GroupBy(b => b.Format.Trim())
            .ToDictionary(g => g.Key, g => g.Count());

        var bookAuthorBreakdown = books
            .Where(b => b.Authors?.Count > 0)
            .SelectMany(b => b.Authors)
            .Where(a => !string.IsNullOrWhiteSpace(a))
            .GroupBy(a => a.Trim())
            .ToDictionary(g => g.Key, g => g.Count());

        var bookGenreBreakdown = books
            .Where(b => b.Genres?.Count > 0)
            .SelectMany(b => b.Genres)
            .Where(g => !string.IsNullOrWhiteSpace(g))
            .GroupBy(g => g.Trim())
            .ToDictionary(g => g.Key, g => g.Count());

        var bookYears = books.Select(b => ParseYear(b.PublicationDate)).Where(y => y.HasValue).Select(y => y!.Value);
        var bookDecadeBreakdown = ToDecadeBreakdown(bookYears);

        var booksWithPages = books.Where(b => b.Pages.HasValue).ToList();
        var pageCountBuckets = new Dictionary<string, int>
        {
            ["0-100"]    = booksWithPages.Count(b => b.Pages <= 100),
            ["101-200"]  = booksWithPages.Count(b => b.Pages > 100 && b.Pages <= 200),
            ["201-300"]  = booksWithPages.Count(b => b.Pages > 200 && b.Pages <= 300),
            ["301-500"]  = booksWithPages.Count(b => b.Pages > 300 && b.Pages <= 500),
            ["501-750"]  = booksWithPages.Count(b => b.Pages > 500 && b.Pages <= 750),
            ["751-1000"] = booksWithPages.Count(b => b.Pages > 750 && b.Pages <= 1000),
            ["1000+"]    = booksWithPages.Count(b => b.Pages > 1000),
        };
        var pageCountBucketsFiltered = pageCountBuckets
            .Where(kv => kv.Value > 0)
            .ToDictionary(kv => kv.Key, kv => kv.Value);

        var avgPages = booksWithPages.Count > 0
            ? (int)Math.Round((double)booksWithPages.Sum(b => b.Pages ?? 0) / booksWithPages.Count)
            : 0;

        var bookStats = new BookStats(
            books.Count(),
            bookFormats.Count(),
            bookFormats,
            books.Where(b => b.Authors?.Count > 0).SelectMany(b => b.Authors).Select(a => a.Trim()).Distinct().Count(),
            books.Where(b => b.Pages.HasValue).Select(b => b.Pages ?? 0).Sum(),
            avgPages,
            bookFormatBreakdown,
            bookDecadeBreakdown,
            pageCountBucketsFiltered,
            TopN(bookAuthorBreakdown),
            TopN(bookGenreBreakdown)
        );

        // ── Movies ───────────────────────────────────────────────────────────
        var movieFormats = movies.Where(m => !string.IsNullOrWhiteSpace(m.Format)).Select(m => m.Format.Trim()).Distinct();
        var movieFormatBreakdown = movies
            .Where(m => !string.IsNullOrWhiteSpace(m.Format))
            .GroupBy(m => m.Format.Trim())
            .ToDictionary(g => g.Key, g => g.Count());

        var movieStudioBreakdown = movies
            .Where(m => m.Studios?.Count > 0)
            .SelectMany(m => m.Studios)
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .GroupBy(s => s.Trim())
            .ToDictionary(g => g.Key, g => g.Count());

        var movieGenreBreakdown = movies
            .Where(m => m.Genres?.Count > 0)
            .SelectMany(m => m.Genres)
            .Where(g => !string.IsNullOrWhiteSpace(g))
            .GroupBy(g => g.Trim())
            .ToDictionary(g => g.Key, g => g.Count());

        var movieYears = movies.Select(m => ParseYear(m.ReleaseDate)).Where(y => y.HasValue).Select(y => y!.Value);
        var movieDecadeBreakdown = ToDecadeBreakdown(movieYears);

        var movieStats = new MovieStats(
            movies.Count(),
            movieFormats.Count(),
            movieFormats,
            movies.Count(m => m.IsTvSeries),
            movieFormatBreakdown,
            movieDecadeBreakdown,
            movieGenreBreakdown,
            TopN(movieStudioBreakdown)
        );

        // ── Games ────────────────────────────────────────────────────────────
        var gameFormats = games.Where(g => !string.IsNullOrWhiteSpace(g.Format)).Select(g => g.Format.Trim()).Distinct();
        var gamePlatforms = games.Where(g => !string.IsNullOrWhiteSpace(g.Platform)).Select(g => g.Platform.Trim()).Distinct();
        var gameFormatBreakdown = games
            .Where(g => !string.IsNullOrWhiteSpace(g.Format))
            .GroupBy(g => g.Format.Trim())
            .ToDictionary(g => g.Key, g => g.Count());
        var gamePlatformBreakdown = games
            .Where(g => !string.IsNullOrWhiteSpace(g.Platform))
            .GroupBy(g => g.Platform.Trim())
            .ToDictionary(g => g.Key, g => g.Count());

        var gamePublisherBreakdown = games
            .Where(g => g.Publishers?.Count > 0)
            .SelectMany(g => g.Publishers)
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .GroupBy(p => p.Trim())
            .ToDictionary(g => g.Key, g => g.Count());

        var gameDeveloperBreakdown = games
            .Where(g => g.Developers?.Count > 0)
            .SelectMany(g => g.Developers)
            .Where(d => !string.IsNullOrWhiteSpace(d))
            .GroupBy(d => d.Trim())
            .ToDictionary(g => g.Key, g => g.Count());

        var gameGenreBreakdown = games
            .Where(g => g.Genres?.Count > 0)
            .SelectMany(g => g.Genres)
            .Where(g => !string.IsNullOrWhiteSpace(g))
            .GroupBy(g => g.Trim())
            .ToDictionary(g => g.Key, g => g.Count());

        var gameYears = games.Select(g => ParseYear(g.ReleaseDate)).Where(y => y.HasValue).Select(y => y!.Value);
        var gameDecadeBreakdown = ToDecadeBreakdown(gameYears);

        var gameStats = new GameStats(
            games.Count(),
            gameFormats.Count(),
            gameFormats,
            gamePlatforms.Count(),
            gamePlatforms,
            gameFormatBreakdown,
            gamePlatformBreakdown,
            gameDecadeBreakdown,
            gameGenreBreakdown,
            TopN(gamePublisherBreakdown),
            TopN(gameDeveloperBreakdown)
        );

        // ── Music ────────────────────────────────────────────────────────────
        var musicFormats = musics.Where(m => !string.IsNullOrWhiteSpace(m.Format)).Select(m => m.Format.Trim()).Distinct();
        var musicFormatBreakdown = musics
            .Where(m => !string.IsNullOrWhiteSpace(m.Format))
            .GroupBy(m => m.Format.Trim())
            .ToDictionary(g => g.Key, g => g.Count());

        var musicArtistBreakdown = musics
            .Where(m => !string.IsNullOrWhiteSpace(m.Artist))
            .GroupBy(m => m.Artist.Trim())
            .ToDictionary(g => g.Key, g => g.Count());

        var musicLabelBreakdown = musics
            .Where(m => !string.IsNullOrWhiteSpace(m.Label))
            .GroupBy(m => m.Label.Trim())
            .ToDictionary(g => g.Key, g => g.Count());

        var musicGenreBreakdown = musics
            .Where(m => m.Genres?.Count > 0)
            .SelectMany(m => m.Genres)
            .Where(g => !string.IsNullOrWhiteSpace(g))
            .GroupBy(g => g.Trim())
            .ToDictionary(g => g.Key, g => g.Count());

        var musicYears = musics.Select(m => ParseYear(m.ReleaseDate)).Where(y => y.HasValue).Select(y => y!.Value);
        var musicDecadeBreakdown = ToDecadeBreakdown(musicYears);

        var musicWithTracks = musics.Where(m => m.Tracks.HasValue).ToList();
        var avgTracks = musicWithTracks.Count > 0
            ? (int)Math.Round((double)musicWithTracks.Sum(m => m.Tracks ?? 0) / musicWithTracks.Count)
            : 0;
        var totalDiscs = musics.Where(m => m.Discs.HasValue).Sum(m => m.Discs ?? 0);

        var musicStats = new MusicStats(
            musics.Count(),
            musicFormats.Count(),
            musicFormats,
            musics.Where(m => !string.IsNullOrWhiteSpace(m.Artist)).Select(m => m.Artist.Trim()).Distinct().Count(),
            musics.Where(m => m.Tracks.HasValue).Select(m => m.Tracks ?? 0).Sum(),
            avgTracks,
            musicLabelBreakdown.Count,
            totalDiscs,
            musicFormatBreakdown,
            musicDecadeBreakdown,
            musicGenreBreakdown,
            TopN(musicArtistBreakdown),
            TopN(musicLabelBreakdown)
        );

        var stats = new Stats(bookStats, movieStats, gameStats, musicStats);

        await cacheService.SetAsync(cacheKey, stats);
        logger.LogInformation("Cached statistics: books={bookCount}, movies={movieCount}, games={gameCount}, music={musicCount}",
            bookStats.Total, movieStats.Total, gameStats.Total, musicStats.Total);

        return stats;
    }
}
