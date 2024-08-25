using MediaSet.Api.Models;

namespace MediaSet.Api.Helpers;

internal static class BookExtensions
{
  public static bool IsEmpty(this Book book) => 
    string.IsNullOrWhiteSpace(book.Title) &&
    string.IsNullOrWhiteSpace(book.Format) &&
    string.IsNullOrWhiteSpace(book.ISBN) &&
    string.IsNullOrWhiteSpace(book.Plot) &&
    string.IsNullOrWhiteSpace(book.PublicationDate) &&
    string.IsNullOrWhiteSpace(book.Publisher) && 
    !book.Pages.HasValue &&
    book.Authors.Count == 0 &&
    book.Genres.Count == 0;
}