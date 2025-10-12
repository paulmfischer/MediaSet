using MediaSet.Api.Models;

namespace MediaSet.Api.Services;

public class MetadataService
{
  private readonly EntityService<Book> booksService;
  private readonly EntityService<Movie> movieService;

  public MetadataService(EntityService<Book> _booksService, EntityService<Movie> _movieService)
  {
    booksService = _booksService;
    movieService = _movieService;
  }

  public async Task<IEnumerable<string>> GetBookFormats()
  {
    var books = await booksService.GetListAsync();

    return books
      .Where(book => !string.IsNullOrWhiteSpace(book.Format))
      .Select(book => book.Format.Trim())
      .Distinct()
      .Order();
  }

  public async Task<IEnumerable<string>> GetBookAuthors()
  {
    var books = await booksService.GetListAsync();

    return books
      .Where(book => book.Authors?.Count > 0)
      .SelectMany(book => book.Authors)
      .Select(author => author.Trim())
      .Distinct()
      .Order();
  }

  public async Task<IEnumerable<string>> GetBookPublishers()
  {
    var books = await booksService.GetListAsync();

    return books
      .Where(book => !string.IsNullOrWhiteSpace(book.Publisher))
      .Select(book => book.Publisher.Trim())
      .Distinct()
      .Order();
  }

  public async Task<IEnumerable<string>> GetBookGenres()
  {
    var books = await booksService.GetListAsync();

    return books
      .Where(book => book.Genres?.Count > 0)
      .SelectMany(book => book.Genres)
      .Select(genre => genre.Trim())
      .Distinct()
      .Order();
  }

  public async Task<IEnumerable<string>> GetMovieFormats()
  {
    var movies = await movieService.GetListAsync();

    return movies
      .Where(movie => !string.IsNullOrWhiteSpace(movie.Format))
      .Select(movie => movie.Format.Trim())
      .Distinct()
      .Order();
  }

  public async Task<IEnumerable<string>> GetMovieStudios()
  {
    var movies = await movieService.GetListAsync();

    return movies
      .Where(movie => movie.Studios?.Count > 0)
      .SelectMany(movie => movie.Studios)
      .Select(studio => studio.Trim())
      .Distinct()
      .Order();
  }

  public async Task<IEnumerable<string>> GetMovieGenres()
  {
    var movies = await movieService.GetListAsync();

    return movies
      .Where(movie => movie.Genres?.Count > 0)
      .SelectMany(movie => movie.Genres)
      .Select(genre => genre.Trim())
      .Distinct()
      .Order();
  }
}
