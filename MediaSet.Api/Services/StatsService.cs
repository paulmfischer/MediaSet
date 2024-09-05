using MediaSet.Api.Models;

namespace MediaSet.Api.Services;

public class StatsService
{
  private readonly BookService bookService;

  public StatsService(BookService _bookService)
  {
    bookService = _bookService;
  }

  public async Task<Stats> GetBookStats()
  {
    var books = await bookService.GetAsync();

    return new Stats
    {
      TotalBooks = books.Count,
      TotalFormats = books.Where(book => !string.IsNullOrWhiteSpace(book.Format)).Select(book => book.Format.Trim()).Distinct().Count(),
      TotalPages = books.Where(book => book.Pages.HasValue).Select(book => book.Pages ?? 0).Sum(),
      UniqueAuthors = books.Where(book => book.Authors?.Count > 0).SelectMany(book => book.Authors).Select(author => author.Trim()).Distinct().Count(),
    };
  }
}