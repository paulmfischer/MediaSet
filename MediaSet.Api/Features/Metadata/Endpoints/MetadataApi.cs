using MediaSet.Api.Features.Metadata.Services;
using MediaSet.Api.Features.Entities.Models;
using MediaSet.Api.Shared.Constraints;
using Microsoft.AspNetCore.Mvc;

namespace MediaSet.Api.Features.Metadata.Endpoints;

internal static class MetadataApi
{
    public static RouteGroupBuilder MapMetadata(this IEndpointRouteBuilder routes)
    {
        var logger = ((WebApplication)routes).Logger;
        var group = routes.MapGroup("/metadata");

        group.WithTags("Metadata");

        group.MapGet("/{media}/{property}", async (IMetadataService metadataService, [FromRoute] Parameter<MediaTypes> media, string property, CancellationToken cancellationToken) =>
        {
            MediaTypes mediaTypes = media;
            logger.LogInformation("Requesting {property} for {media}", property, mediaTypes);
            return await metadataService.GetMetadata(mediaTypes, property, cancellationToken);
        });

        return group;
    }
}
