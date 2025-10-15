using MediaSet.Api.Clients;
using MediaSet.Api.Helpers;
using Microsoft.AspNetCore.Http.HttpResults;

namespace MediaSet.Api.Lookup;

internal static class LookupApi
{
  public static RouteGroupBuilder MapIsbnLookup(this IEndpointRouteBuilder routes)
  {
    var logger = ((WebApplication)routes).Logger;
    var group = routes.MapGroup("/lookup");

    group.WithTags("Lookup");

    group.MapGet("/{identifierType}/{identifierValue}", async Task<Results<Ok<BookResponse>, NotFound, BadRequest<string>>> (IOpenLibraryClient openLibraryClient, string identifierType, string identifierValue) =>
    {
      if (!IdentifierTypeExtensions.TryParseIdentifierType(identifierType, out var parsedIdentifierType))
      {
        logger.LogWarning("Invalid identifier type {identifierType} for value {identifierValue}", identifierType, identifierValue);
        return TypedResults.BadRequest($"Invalid identifier type: {identifierType}. Valid types are: {IdentifierTypeExtensions.GetValidTypesString()}");
      }

      logger.LogInformation("Lookup request: {identifierType} = {identifierValue}", parsedIdentifierType, identifierValue);
      var result = await openLibraryClient.GetReadableBookAsync(parsedIdentifierType, identifierValue);

      if (result is BookResponse bookResponse)
      {
        return TypedResults.Ok(bookResponse);
      }

      logger.LogInformation("No result for {identifierType} = {identifierValue}", parsedIdentifierType, identifierValue);
      return TypedResults.NotFound();
    });

    return group;
  }
}
