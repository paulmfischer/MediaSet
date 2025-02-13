using MediaSet.Api.Clients;
using MediaSet.Api.Services;
using Microsoft.AspNetCore.Http.HttpResults;

namespace MediaSet.Api.Lookup;

internal static class LookupApi
{
  public static RouteGroupBuilder MapLookupsApi(this IEndpointRouteBuilder routes, bool isBookLookupEnabled, bool isMovieLookupEnabled)
  {
    var group = routes.MapGroup("/lookup");
    
    group.WithTags("Lookup");
    
    if (isBookLookupEnabled)
    {
      group.MapGet("/isbn/{isbn}", async Task<Results<Ok<BookResponse>, NotFound>> (LookupService lookupService, string isbn) =>
      {
        return await lookupService.SearchByIsbnAsync(isbn) switch
        {
          BookResponse bookResponse => TypedResults.Ok(bookResponse),
          _ => TypedResults.NotFound(),
        };
      });
    }
    
    if (isMovieLookupEnabled)
    {
      group.MapGet("/upc/{upc}", async Task<Results<Ok<MovieResponse>, NotFound>> (LookupService lookupService, string upc) =>
      {
        return await lookupService.SearchByUpcAsync(upc) switch
        {
          MovieResponse movieResponse => TypedResults.Ok(movieResponse),
          _ => TypedResults.NotFound(),
        };
      });
    }

    return group;
  }
}