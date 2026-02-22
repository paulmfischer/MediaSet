using MediaSet.Api.Infrastructure.Lookup.Models;
using MediaSet.Api.Shared.Models;
using MediaSet.Api.Infrastructure.Lookup.Strategies;
using MediaSet.Api.Shared.Extensions;
using Microsoft.AspNetCore.Http.HttpResults;

namespace MediaSet.Api.Features.Lookup.Endpoints;

internal static class LookupApi
{
    public static RouteGroupBuilder MapLookup(this IEndpointRouteBuilder routes)
    {
        var logger = ((WebApplication)routes).Logger;
        var group = routes.MapGroup("/lookup");

        group.WithTags("Lookup");

        group.MapGet("/{entityType}/{identifierType}/{identifierValue}", async Task<Results<Ok<IReadOnlyList<BookResponse>>, Ok<IReadOnlyList<MovieResponse>>, Ok<IReadOnlyList<GameResponse>>, Ok<IReadOnlyList<MusicResponse>>, BadRequest<string>>> (
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
                return TypedResults.BadRequest($"Invalid entity type: {entityType}. Valid types are: Books, Movies, Games, Musics");
            }

            logger.LogInformation("Lookup request: {EntityType} with {IdentifierType} = {IdentifierValue}",
                parsedEntityType, parsedIdentifierType, identifierValue);

            try
            {
                switch (parsedEntityType)
                {
                    case MediaTypes.Books:
                        var books = await strategyFactory.GetStrategy<BookResponse>(parsedEntityType, parsedIdentifierType)
                            .LookupAsync(parsedIdentifierType, identifierValue, cancellationToken);
                        return TypedResults.Ok(books);
                    case MediaTypes.Movies:
                        var movies = await strategyFactory.GetStrategy<MovieResponse>(parsedEntityType, parsedIdentifierType)
                            .LookupAsync(parsedIdentifierType, identifierValue, cancellationToken);
                        return TypedResults.Ok(movies);
                    case MediaTypes.Games:
                        var games = await strategyFactory.GetStrategy<GameResponse>(parsedEntityType, parsedIdentifierType)
                            .LookupAsync(parsedIdentifierType, identifierValue, cancellationToken);
                        return TypedResults.Ok(games);
                    case MediaTypes.Musics:
                        var music = await strategyFactory.GetStrategy<MusicResponse>(parsedEntityType, parsedIdentifierType)
                            .LookupAsync(parsedIdentifierType, identifierValue, cancellationToken);
                        return TypedResults.Ok(music);
                    default:
                        return TypedResults.BadRequest($"Invalid entity type: {entityType}. Valid types are: Books, Movies, Games, Musics");
                }
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
