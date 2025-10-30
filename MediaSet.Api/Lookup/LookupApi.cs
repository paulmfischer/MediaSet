using MediaSet.Api.Clients;
using MediaSet.Api.Helpers;
using MediaSet.Api.Models;
using MediaSet.Api.Services;
using Microsoft.AspNetCore.Http.HttpResults;

namespace MediaSet.Api.Lookup;

internal static class LookupApi
{
    public static RouteGroupBuilder MapLookup(this IEndpointRouteBuilder routes)
    {
        var logger = ((WebApplication)routes).Logger;
        var group = routes.MapGroup("/lookup");

        group.WithTags("Lookup");

        group.MapGet("/{entityType}/{identifierType}/{identifierValue}", async Task<Results<Ok<BookResponse>, Ok<MovieResponse>, Ok<GameResponse>, NotFound, BadRequest<string>>> (
            LookupStrategyFactory strategyFactory,
            string entityType,
            string identifierType,
            string identifierValue,
            CancellationToken cancellationToken) =>
        {
            if (!IdentifierTypeExtensions.TryParseIdentifierType(identifierType, out var parsedIdentifierType))
            {
                logger.LogWarning("Invalid identifier type {IdentifierType} for value {IdentifierValue}", identifierType, identifierValue);
                return TypedResults.BadRequest($"Invalid identifier type: {identifierType}. Valid types are: {IdentifierTypeExtensions.GetValidTypesString()}");
            }

            if (!Enum.TryParse<MediaTypes>(entityType, true, out var parsedEntityType))
            {
                logger.LogWarning("Invalid entity type {EntityType}", entityType);
                return TypedResults.BadRequest($"Invalid entity type: {entityType}. Valid types are: Books, Movies, Games");
            }

            logger.LogInformation("Lookup request: {EntityType} with {IdentifierType} = {IdentifierValue}", 
                parsedEntityType, parsedIdentifierType, identifierValue);

            try
            {
                object? result = parsedEntityType switch
                {
                    MediaTypes.Books => await strategyFactory.GetStrategy<BookResponse>(parsedEntityType, parsedIdentifierType)
                        .LookupAsync(parsedIdentifierType, identifierValue, cancellationToken),
                    MediaTypes.Movies => await strategyFactory.GetStrategy<MovieResponse>(parsedEntityType, parsedIdentifierType)
                        .LookupAsync(parsedIdentifierType, identifierValue, cancellationToken),
                    MediaTypes.Games => await strategyFactory.GetStrategy<GameResponse>(parsedEntityType, parsedIdentifierType)
                        .LookupAsync(parsedIdentifierType, identifierValue, cancellationToken),
                    _ => null
                };

                if (result != null)
                {
                    return result switch
                    {
                        BookResponse book => TypedResults.Ok(book),
                        MovieResponse movie => TypedResults.Ok(movie),
                        GameResponse game => TypedResults.Ok(game),
                        _ => TypedResults.NotFound()
                    };
                }

                logger.LogInformation("No result for {EntityType} with {IdentifierType} = {IdentifierValue}", 
                    parsedEntityType, parsedIdentifierType, identifierValue);
                return TypedResults.NotFound();
            }
            catch (NotSupportedException ex)
            {
                logger.LogWarning(ex, "Unsupported combination: {EntityType} with {IdentifierType}", 
                    parsedEntityType, parsedIdentifierType);
                return TypedResults.BadRequest(ex.Message);
            }
        });

        return group;
    }
}
