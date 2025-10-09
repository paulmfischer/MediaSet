using MediaSet.Api.Clients;
using Microsoft.AspNetCore.Http.HttpResults;

namespace MediaSet.Api.Lookup;

internal static class LookupApi
{
  public static RouteGroupBuilder MapIsbnLookup(this IEndpointRouteBuilder routes)
  {
    var group = routes.MapGroup("/lookup");
    
    group.WithTags("Lookup");
    
    group.MapGet("/{identifierType}/{identifierValue}", async Task<Results<Ok<BookResponse>, NotFound, BadRequest<string>>> (OpenLibraryClient openLibraryClient, string identifierType, string identifierValue) =>
    {
      var result = identifierType.ToLowerInvariant() switch
      {
        "isbn" => await openLibraryClient.GetReadableBookByIsbnAsync(identifierValue),
        "lccn" => await openLibraryClient.GetReadableBookByLccnAsync(identifierValue),
        "oclc" => await openLibraryClient.GetReadableBookByOclcAsync(identifierValue),
        "olid" => await openLibraryClient.GetReadableBookByOlidAsync(identifierValue),
        _ => null
      };

      return result switch
      {
        BookResponse bookResponse => TypedResults.Ok(bookResponse),
        null when !IsValidIdentifierType(identifierType) => TypedResults.BadRequest($"Invalid identifier type: {identifierType}. Valid types are: isbn, lccn, oclc, olid"),
        _ => TypedResults.NotFound()
      };
    });

    return group;
  }

  private static bool IsValidIdentifierType(string identifierType)
  {
    return identifierType.ToLowerInvariant() switch
    {
      "isbn" or "lccn" or "oclc" or "olid" => true,
      _ => false
    };
  }
}