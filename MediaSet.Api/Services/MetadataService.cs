using MediaSet.Api.Models;

namespace MediaSet.Api.Services;

public class MetadataService
{
  private readonly EntityService<Book> booksService;

  public MetadataService(EntityService<Book> _booksService)
  {
    booksService = _booksService;
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
}