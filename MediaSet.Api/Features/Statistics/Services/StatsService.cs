using MediaSet.Api.Features.Statistics.Models;
using MediaSet.Api.Shared.Models;
using MediaSet.Api.Features.Entities.Models;
using MediaSet.Api.Infrastructure.Caching;
using MediaSet.Api.Infrastructure.DataAccess;
using Microsoft.Extensions.Options;
using Serilog;
using SerilogTracing;

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

    public async Task<Stats> GetMediaStatsAsync(CancellationToken cancellationToken = default)
    {
        using var activity = Log.Logger.StartActivity("GetMediaStats");
        
        const string cacheKey = "stats";
        
        // Try to get from cache
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
        var bookFormats = books.Where(book => !string.IsNullOrWhiteSpace(book.Format)).Select(book => book.Format.Trim()).Distinct();
        var movieFormats = movies.Where(movie => !string.IsNullOrWhiteSpace(movie.Format)).Select(movie => movie.Format.Trim()).Distinct();
        var gameFormats = games.Where(game => !string.IsNullOrWhiteSpace(game.Format)).Select(game => game.Format.Trim()).Distinct();
        var gamePlatforms = games.Where(game => !string.IsNullOrWhiteSpace(game.Platform)).Select(game => game.Platform.Trim()).Distinct();
        var musicFormats = musics.Where(music => !string.IsNullOrWhiteSpace(music.Format)).Select(music => music.Format.Trim()).Distinct();
        var bookStats = new BookStats
        (
          books.Count(),
          bookFormats.Count(),
          bookFormats,
          books.Where(book => book.Authors?.Count > 0).SelectMany(book => book.Authors).Select(author => author.Trim()).Distinct().Count(),
          books.Where(book => book.Pages.HasValue).Select(book => book.Pages ?? 0).Sum()
        );
        var movieStats = new MovieStats
        (
          movies.Count(),
          movieFormats.Count(),
          movieFormats,
          movies.Where(movie => movie.IsTvSeries).Count()
        );
        var gameStats = new GameStats
        (
          games.Count(),
          gameFormats.Count(),
          gameFormats,
          gamePlatforms.Count(),
          gamePlatforms
        );
        var musicStats = new MusicStats
        (
          musics.Count(),
          musicFormats.Count(),
          musicFormats,
          musics.Where(music => !string.IsNullOrWhiteSpace(music.Artist)).Select(music => music.Artist.Trim()).Distinct().Count(),
          musics.Where(music => music.Tracks.HasValue).Select(music => music.Tracks ?? 0).Sum()
        );

        var stats = new Stats(bookStats, movieStats, gameStats, musicStats);
        
        // Cache the results
        await cacheService.SetAsync(cacheKey, stats);
        logger.LogInformation("Cached statistics: books={bookCount}, movies={movieCount}, games={gameCount}, music={musicCount}", 
            bookStats.Total, movieStats.Total, gameStats.Total, musicStats.Total);

        return stats;
    }
}
