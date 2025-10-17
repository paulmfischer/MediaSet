using MediaSet.Api.Models;

namespace MediaSet.Api.Services;

public class StatsService(IEntityService<Book> bookService, IEntityService<Movie> movieService, IEntityService<Game> gameService) : IStatsService
{
    public async Task<Stats> GetMediaStatsAsync()
    {
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
        var gamePlatforms = games.Where(game => game.Platforms?.Count > 0).SelectMany(game => game.Platforms).Select(platform => platform.Trim()).Distinct();
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

        return new Stats(bookStats, movieStats, gameStats);
    }
}
