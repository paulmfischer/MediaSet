using MediaSet.Api.Clients;
using MediaSet.Api.Helpers;
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
      if (!IdentifierTypeExtensions.TryParseIdentifierType(identifierType, out var parsedIdentifierType))
      {
        return TypedResults.BadRequest($"Invalid identifier type: {identifierType}. Valid types are: {IdentifierTypeExtensions.GetValidTypesString()}");
      }

      var result = await openLibraryClient.GetReadableBookAsync(parsedIdentifierType, identifierValue);

      return result switch
      {
        BookResponse bookResponse => TypedResults.Ok(bookResponse),
        _ => TypedResults.NotFound()
      };
    });

    return group;
  }
}
