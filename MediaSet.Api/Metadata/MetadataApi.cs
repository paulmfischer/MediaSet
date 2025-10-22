using MediaSet.Api.Bindings;
using MediaSet.Api.Models;
using MediaSet.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace MediaSet.Api.Metadata;

internal static class MetadataApi
{
    public static RouteGroupBuilder MapMetadata(this IEndpointRouteBuilder routes)
    {
        var logger = ((WebApplication)routes).Logger;
        var group = routes.MapGroup("/metadata");

        group.WithTags("Metadata");

        group.MapGet("/formats/{media}", async (IMetadataService metadataService, [FromRoute] Parameter<MediaTypes> media) =>
        {
            MediaTypes mediaTypes = media;
            logger.LogInformation("Requesting formats for {media}", mediaTypes);
            return await metadataService.GetFormats(mediaTypes);
        });

        group.MapGet("/genres/{media}", async (IMetadataService metadataService, [FromRoute] Parameter<MediaTypes> media) =>
        {
            MediaTypes mediaTypes = media;
            logger.LogInformation("Requesting genres for {media}", mediaTypes);
            return await metadataService.GetGenres(mediaTypes);
        });

        group.MapGet("/{media}/{property}", async (IMetadataService metadataService, [FromRoute] Parameter<MediaTypes> media, string property) =>
        {
            MediaTypes mediaTypes = media;
            logger.LogInformation("Requesting {property} for {media}", property, mediaTypes);
            return await metadataService.GetMetadata(mediaTypes, property);
        });

        return group;
    }
}
