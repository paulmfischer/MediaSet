using MediaSet.Api.Models;
using Microsoft.Extensions.Options;

namespace MediaSet.Api.Services;

public class StatsService : IStatsService
{
    private readonly IEntityService<Book> bookService;
    private readonly IEntityService<Movie> movieService;
    private readonly IEntityService<Game> gameService;
    private readonly ICacheService cacheService;
    private readonly CacheSettings cacheSettings;
    private readonly ILogger<StatsService> logger;

    public StatsService(
        IEntityService<Book> _bookService,
        IEntityService<Movie> _movieService,
        IEntityService<Game> _gameService,
        ICacheService _cacheService,
        IOptions<CacheSettings> _cacheSettings,
        ILogger<StatsService> _logger)
    {
        bookService = _bookService;
        movieService = _movieService;
        gameService = _gameService;
        cacheService = _cacheService;
        cacheSettings = _cacheSettings.Value;
        logger = _logger;
    }
    public async Task<Stats> GetMediaStatsAsync()
    {
        const string cacheKey = "stats";
        
        // Try to get from cache
        var cachedStats = await cacheService.GetAsync<Stats>(cacheKey);
        if (cachedStats != null)
        {
            logger.LogDebug("Returning cached statistics");
            return cachedStats;
        }

        logger.LogDebug("Cache miss for statistics, calculating from database");

        var bookTask = bookService.GetListAsync();
        var movieTask = movieService.GetListAsync();
        var gameTask = gameService.GetListAsync();
        await Task.WhenAll(bookTask, movieTask, gameTask);

        var books = bookTask.Result;
        var movies = movieTask.Result;
        var games = gameTask.Result;
        var bookFormats = books.Where(book => !string.IsNullOrWhiteSpace(book.Format)).Select(book => book.Format.Trim()).Distinct();
        var movieFormats = movies.Where(movie => !string.IsNullOrWhiteSpace(movie.Format)).Select(movie => movie.Format.Trim()).Distinct();
        var gameFormats = games.Where(game => !string.IsNullOrWhiteSpace(game.Format)).Select(game => game.Format.Trim()).Distinct();
        var gamePlatforms = games.Where(game => !string.IsNullOrWhiteSpace(game.Platform)).Select(game => game.Platform.Trim()).Distinct();
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

        var stats = new Stats(bookStats, movieStats, gameStats);
        
        // Cache the results
        await cacheService.SetAsync(cacheKey, stats);
        logger.LogInformation("Cached statistics: books={bookCount}, movies={movieCount}, games={gameCount}", 
            bookStats.Total, movieStats.Total, gameStats.Total);

        return stats;
    }
}
