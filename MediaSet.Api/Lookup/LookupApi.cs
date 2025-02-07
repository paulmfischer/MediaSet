using MediaSet.Api.Clients;
using Microsoft.AspNetCore.Http.HttpResults;

namespace MediaSet.Api.Lookup;

internal static class LookupApi
{
  public static RouteGroupBuilder MapIsbnLookup(this IEndpointRouteBuilder routes)
  {
    var group = routes.MapGroup("/lookup");
    
    group.WithTags("Lookup");
    
    group.MapGet("/isbn/{isbn}", async Task<Results<Ok<BookResponse>, NotFound>> (OpenLibraryClient openLibraryClient, string isbn) =>
    {
      return await openLibraryClient.GetBookByIsbnAsync(isbn) switch
      {
        BookResponse bookResponse => TypedResults.Ok(bookResponse),
        _ => TypedResults.NotFound(),
      };
    });

    return group;
  }
}