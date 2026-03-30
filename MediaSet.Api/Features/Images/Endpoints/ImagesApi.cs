using MediaSet.Api.Features.Images.Services;
using MediaSet.Api.Infrastructure.DataAccess;
using MediaSet.Api.Shared.Models;
using Microsoft.AspNetCore.Mvc;

namespace MediaSet.Api.Features.Images.Endpoints;

internal record ResetImageLookupRequest(IEnumerable<string> EntityIds);

internal static class ImagesApi
{
    public static RouteGroupBuilder MapImages(this IEndpointRouteBuilder routes)
    {
        var logger = ((WebApplication)routes).Logger;
        var group = routes.MapGroup("/images");

        group.WithTags("Images");

        group.MapDelete("/orphaned", async (IImageManagementService imageManagementService, CancellationToken cancellationToken) =>
        {
            logger.LogInformation("Deleting orphaned images");
            var count = await imageManagementService.DeleteOrphanedImagesAsync(cancellationToken);
            return Results.Ok(new { deleted = count });
        });

        group.MapDelete("/lookup/{entityType}", async (string entityType, [FromBody] ResetImageLookupRequest request, IImageManagementService imageManagementService, CancellationToken cancellationToken) =>
        {
            logger.LogInformation("Resetting ImageLookup for entity type {EntityType}", entityType);
            try
            {
                var count = await imageManagementService.ResetImageLookupAsync(request.EntityIds, entityType, cancellationToken);
                return Results.Ok(new { reset = count });
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        if (((WebApplication)routes).Environment.IsDevelopment())
        {
            group.MapPost("/dev/lookup/{entityType}/{id}", async (
                string entityType,
                string id,
                IImageLookupService imageLookupService,
                IEntityService<Book> bookService,
                IEntityService<Movie> movieService,
                IEntityService<Game> gameService,
                IEntityService<Music> musicService,
                CancellationToken cancellationToken) =>
            {
                if (!Enum.TryParse<MediaTypes>(entityType, ignoreCase: true, out var mediaType))
                {
                    return Results.BadRequest(new { error = $"Invalid entity type '{entityType}'. Valid types: Books, Movies, Games, Musics" });
                }

                IEntity? entity = mediaType switch
                {
                    MediaTypes.Books => await bookService.GetAsync(id, cancellationToken),
                    MediaTypes.Movies => await movieService.GetAsync(id, cancellationToken),
                    MediaTypes.Games => await gameService.GetAsync(id, cancellationToken),
                    MediaTypes.Musics => await musicService.GetAsync(id, cancellationToken),
                    _ => null
                };

                if (entity is null)
                {
                    return Results.NotFound(new { error = $"{entityType} entity with id '{id}' not found" });
                }

                logger.LogInformation("Dev lookup triggered for {EntityType} entity {EntityId}", entityType, id);

                var result = await imageLookupService.LookupAndSaveImageAsync(entity, mediaType, cancellationToken);
                return Results.Ok(result);
            });
        }

        return group;
    }
}
