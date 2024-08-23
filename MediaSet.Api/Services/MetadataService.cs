
namespace MediaSet.Api.Services;

public class MetadataService
{
  private readonly BookService _booksService;

  public MetadataService(BookService booksService)
  {
    _booksService = booksService;
  }

  public async Task<IEnumerable<string>> GetBookFormats()
  {
    var books = await _booksService.GetAsync();

    return books
      .Where(book => !string.IsNullOrWhiteSpace(book.Format))
      .Select(book => book.Format)
      .Distinct()
      .Order();
  }

  public async Task<IEnumerable<string>> GetBookAuthors()
  {
    var books = await _booksService.GetAsync();

    return books
      .Where(book => book.Author?.Count > 0)
      .SelectMany(book => book.Author)
      .Select(author => author.Trim())
      .Distinct()
      .Order();
  }

  public async Task<IEnumerable<string>> GetBookPublishers()
  {
    var books = await _booksService.GetAsync();

    return books
      .Where(book => book.Publisher?.Count > 0)
      .SelectMany(book => book.Publisher)
      .Select(publisher => publisher.Trim())
      .Distinct()
      .Order();
  }

  public async Task<IEnumerable<string>> GetBookGenres()
  {
    var books = await _booksService.GetAsync();

    return books
      .Where(book => book.Genre?.Count > 0)
      .SelectMany(book => book.Genre)
      .Select(genre => genre.Trim())
      .Distinct()
      .Order();
  }
}