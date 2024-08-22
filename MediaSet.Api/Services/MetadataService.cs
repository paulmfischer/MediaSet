
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
}