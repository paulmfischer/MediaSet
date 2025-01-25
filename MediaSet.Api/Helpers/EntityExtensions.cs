using MediaSet.Api.Models;

namespace MediaSet.Api.Helpers;

internal static class EntityExtensions
{
  public static bool IsEmpty<TEntity>(this TEntity entity) where TEntity : IEntity
  {
    if (entity is Movie movie) {
      return string.IsNullOrWhiteSpace(movie.Title) &&
        string.IsNullOrWhiteSpace(movie.ISBN) &&
        string.IsNullOrWhiteSpace(movie.Format) &&
        string.IsNullOrWhiteSpace(movie.ReleaseDate) &&
        string.IsNullOrWhiteSpace(movie.Rating) &&
        !movie.Runtime.HasValue &&
        movie.Studios.Count == 0 &&
        movie.Genres.Count == 0 &&
        string.IsNullOrWhiteSpace(movie.Plot);
    }
    else if (entity is Book book) {
      return string.IsNullOrWhiteSpace(book.Title) &&
        string.IsNullOrWhiteSpace(book.Format) &&
        string.IsNullOrWhiteSpace(book.ISBN) &&
        string.IsNullOrWhiteSpace(book.Plot) &&
        string.IsNullOrWhiteSpace(book.PublicationDate) &&
        string.IsNullOrWhiteSpace(book.Publisher) && 
        !book.Pages.HasValue &&
        book.Authors.Count == 0 &&
        book.Genres.Count == 0;
    }
    
    return true;
  }
}