using MediaSet.Api.Clients;

namespace MediaSet.Api.Services;

public class LookupService(
  ILogger<LookupService> logger,
  OpenLibraryClient openLibraryClient,
  UpcItemdbClient upcItemClient,
  TmdbClient tmdbClient
)
{
  public async Task<MovieResponse?> SearchByUpcAsync(string upc)
  {
    var upcLookupData = await upcItemClient.SearchByUpc(upc);
    logger.LogInformation("Looking up movie with upc {upc} - found: {upcResponse}", upc, upcLookupData);
    if (upcLookupData != null && upcLookupData.Items.Count() > 0)
    {
      var tmdbMovieData = await tmdbClient.SearchMovieAsync(upcLookupData.Items[0].Title);
      logger.LogInformation("Searching Tmdb for movie with title '{title}' - found: {movieResponse}", upcLookupData.Items[0].Title, tmdbMovieData);
      return tmdbMovieData?.TotalResults > 0 ? tmdbMovieData : null;
    }

    return null;
  }
  
  public async Task<BookResponse?> SearchByIsbnAsync(string isbn)
  {
    var openLibraryData = await openLibraryClient.SearchByIsbnAsync(isbn);
    logger.LogInformation("Looking up book with isbn {isbn} -  found: {book}", isbn, openLibraryData);
    return openLibraryData;
  }
}