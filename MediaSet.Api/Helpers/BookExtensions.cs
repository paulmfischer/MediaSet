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
    !book.Pages.HasValue &&
    book.Author.Count == 0 &&
    book.Genre.Count == 0 &&
    book.Publisher.Count == 0;
}