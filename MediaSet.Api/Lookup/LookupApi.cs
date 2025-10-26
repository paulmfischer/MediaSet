using MediaSet.Api.Clients;
using MediaSet.Api.Helpers;
using MediaSet.Api.Services.Lookup;
using Microsoft.AspNetCore.Http.HttpResults;

namespace MediaSet.Api.Lookup;

internal static class LookupApi
{
    public static RouteGroupBuilder MapLookup(this IEndpointRouteBuilder routes)
    {
        var logger = ((WebApplication)routes).Logger;
        var group = routes.MapGroup("/lookup");

        group.WithTags("Lookup");

        group.MapGet("/{entityType}/{identifierType}/{identifierValue}", async Task<Results<Ok<object>, NotFound, BadRequest<string>>> (ILookupService lookupService, string entityType, string identifierType, string identifierValue, CancellationToken cancellationToken) =>
        {
            if (!IdentifierTypeExtensions.TryParseIdentifierType(identifierType, out var parsedIdentifierType))
            {
                logger.LogWarning("Invalid identifier type {identifierType} for value {identifierValue}", identifierType, identifierValue);
                return TypedResults.BadRequest($"Invalid identifier type: {identifierType}. Valid types are: {IdentifierTypeExtensions.GetValidTypesString()}");
            }

            logger.LogInformation("Lookup request: {entityType} / {identifierType} = {identifierValue}", entityType, parsedIdentifierType, identifierValue);
            var result = await lookupService.LookupAsync(entityType, identifierType, identifierValue, cancellationToken);

            if (result != null)
            {
                return TypedResults.Ok(result);
            }

            logger.LogInformation("No result for {entityType} / {identifierType} = {identifierValue}", entityType, parsedIdentifierType, identifierValue);
            return TypedResults.NotFound();
        });

        return group;
    }
}
