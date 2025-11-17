using MediaSet.Api.Models;
using MediaSet.Api.Services;
using Microsoft.AspNetCore.Http.HttpResults;

namespace MediaSet.Api.Entities;

/// <summary>
/// Image API endpoints for serving and managing image files.
/// Routes: GET /images/{entityType}/{entityId}, DELETE /images/{entityType}/{entityId}
/// </summary>
internal static class ImageApi
{
    public static void MapImages(this WebApplication app)
    {
        var logger = app.Logger;
        var group = app.MapGroup("/images");

        group.WithTags("Images");

        /// <summary>
        /// GET /images/{entityType}/{entityId}
        /// Retrieve and serve image for a media entity.
        /// Returns the image file as binary stream with appropriate HTTP caching headers.
        /// </summary>
        group.MapGet("/{entityType}/{entityId}", async Task<Results<FileStreamHttpResult, NotFound>> (
            [Microsoft.AspNetCore.Mvc.FromServices] IEntityService<Book> bookService,
            [Microsoft.AspNetCore.Mvc.FromServices] IEntityService<Movie> movieService,
            [Microsoft.AspNetCore.Mvc.FromServices] IEntityService<Game> gameService,
            [Microsoft.AspNetCore.Mvc.FromServices] IEntityService<Music> musicService,
            [Microsoft.AspNetCore.Mvc.FromServices] IImageService imageService,
            string entityType,
            string entityId,
            CancellationToken cancellationToken) =>
        {
            try
            {
                logger.LogInformation("Fetching image for {entityType}/{entityId}", entityType, entityId);

                // Get the entity to extract image path
                IEntity? entity = entityType.ToLowerInvariant() switch
                {
                    "books" => await bookService.GetAsync(entityId, cancellationToken),
                    "movies" => await movieService.GetAsync(entityId, cancellationToken),
                    "games" => await gameService.GetAsync(entityId, cancellationToken),
                    "musics" => await musicService.GetAsync(entityId, cancellationToken),
                    _ => null
                };

                if (entity is null)
                {
                    logger.LogWarning("Entity not found: {entityType}/{entityId}", entityType, entityId);
                    return TypedResults.NotFound();
                }

                if (entity.CoverImage is null)
                {
                    logger.LogWarning("Image not found for {entityType}/{entityId}", entityType, entityId);
                    return TypedResults.NotFound();
                }

                var imageStream = await imageService.GetImageStreamAsync(entity.CoverImage.FilePath, cancellationToken);
                if (imageStream is null)
                {
                    logger.LogWarning("Failed to retrieve image stream for {imagePath}", entity.CoverImage.FilePath);
                    return TypedResults.NotFound();
                }

                logger.LogInformation("Serving image: {imagePath}", entity.CoverImage.FilePath);

                // Return file stream with caching headers
                return TypedResults.File(
                    imageStream,
                    entity.CoverImage.ContentType,
                    fileDownloadName: entity.CoverImage.FileName,
                    enableRangeProcessing: true);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error retrieving image for {entityType}/{entityId}", entityType, entityId);
                return TypedResults.NotFound();
            }
        });

        /// <summary>
        /// DELETE /images/{entityType}/{entityId}
        /// Delete image for a media entity while keeping the entity intact.
        /// </summary>
        group.MapDelete("/{entityType}/{entityId}", async Task<Results<NoContent, NotFound>> (
            [Microsoft.AspNetCore.Mvc.FromServices] IEntityService<Book> bookService,
            [Microsoft.AspNetCore.Mvc.FromServices] IEntityService<Movie> movieService,
            [Microsoft.AspNetCore.Mvc.FromServices] IEntityService<Game> gameService,
            [Microsoft.AspNetCore.Mvc.FromServices] IEntityService<Music> musicService,
            [Microsoft.AspNetCore.Mvc.FromServices] IImageService imageService,
            string entityType,
            string entityId,
            CancellationToken cancellationToken) =>
        {
            try
            {
                logger.LogInformation("Deleting image for {entityType}/{entityId}", entityType, entityId);

                // Get the entity
                IEntity? entity = entityType.ToLowerInvariant() switch
                {
                    "books" => await bookService.GetAsync(entityId, cancellationToken),
                    "movies" => await movieService.GetAsync(entityId, cancellationToken),
                    "games" => await gameService.GetAsync(entityId, cancellationToken),
                    "musics" => await musicService.GetAsync(entityId, cancellationToken),
                    _ => null
                };

                if (entity is null)
                {
                    logger.LogWarning("Entity not found: {entityType}/{entityId}", entityType, entityId);
                    return TypedResults.NotFound();
                }

                if (entity.CoverImage is null)
                {
                    logger.LogWarning("No image found for {entityType}/{entityId}", entityType, entityId);
                    return TypedResults.NotFound();
                }

                // Delete the image file
                try
                {
                    imageService.DeleteImageAsync(entity.CoverImage.FilePath);
                    logger.LogInformation("Deleted image file: {imagePath}", entity.CoverImage.FilePath);
                }
                catch (Exception ex)
                {
                    logger.LogWarning("Failed to delete image file: {error}", ex.Message);
                    // Continue - file might already be deleted
                }

                // Remove coverImage from entity
                entity.CoverImage = null;
                
                // Update entity based on type
                var entityTypeKey = entityType.ToLowerInvariant();
                if (entityTypeKey == "books")
                {
                    await bookService.UpdateAsync(entityId, (Book)(object)entity, cancellationToken);
                }
                else if (entityTypeKey == "movies")
                {
                    await movieService.UpdateAsync(entityId, (Movie)(object)entity, cancellationToken);
                }
                else if (entityTypeKey == "games")
                {
                    await gameService.UpdateAsync(entityId, (Game)(object)entity, cancellationToken);
                }
                else if (entityTypeKey == "musics")
                {
                    await musicService.UpdateAsync(entityId, (Music)(object)entity, cancellationToken);
                }
                else
                {
                    throw new InvalidOperationException($"Unknown entity type: {entityType}");
                }

                logger.LogInformation("Removed image reference from {entityType}/{entityId}", entityType, entityId);

                return TypedResults.NoContent();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error deleting image for {entityType}/{entityId}", entityType, entityId);
                return TypedResults.NotFound();
            }
        });

        return;
    }
}
