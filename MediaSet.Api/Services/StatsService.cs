using MediaSet.Api.Models;

namespace MediaSet.Api.Services;

public class StatsService(EntityService<Book> bookService, EntityService<Movie> movieService)
{
  public async Task<Stats> GetMediaStatsAsync()
  {
    var bookTask = bookService.GetListAsync();
    var movieTask = movieService.GetListAsync();
    await Task.WhenAll(bookTask, movieTask);

    var books = bookTask.Result;
    var movies = movieTask.Result;
    var bookFormats = books.Where(book => !string.IsNullOrWhiteSpace(book.Format)).Select(book => book.Format.Trim()).Distinct();
    var movieFormats = movies.Where(movie => !string.IsNullOrWhiteSpace(movie.Format)).Select(movie => movie.Format.Trim()).Distinct();
    var bookStats = new BookStats
    (
      books.Count(),
      bookFormats.Count(),
      bookFormats,
      books.Where(book => book.Pages.HasValue).Select(book => book.Pages ?? 0).Sum(),
      books.Where(book => book.Authors?.Count > 0).SelectMany(book => book.Authors).Select(author => author.Trim()).Distinct().Count()
    );
    var movieStats = new MovieStats
    (
      movies.Count(),
      movieFormats.Count(),
      movieFormats,
      movies.Where(movie => movie.IsTvSeries).Count()
    );

    return new Stats(bookStats, movieStats);
  }
}
