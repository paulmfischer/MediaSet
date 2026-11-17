using MediaSet.Api.Infrastructure.DataAccess;
using MediaSet.Api.Infrastructure.Plex;
using MediaSet.Api.Shared.Models;
using MongoDB.Driver;

namespace MediaSet.Api.Features.Entities.Services;

public sealed class EntityWriteService<TEntity>(
    IEntityService<TEntity> entityService,
    EntityImageCoordinator images,
    ILogger<EntityWriteService<TEntity>> logger,
    IPlexDataCleanup? plexCleanup = null) : IEntityWriteService<TEntity>
    where TEntity : IEntity
{
    private static readonly string _entityType = $"{typeof(TEntity).Name}s";

    public async Task<EntityWriteResult<TEntity>> CreateAsync(TEntity entity, ImageInput image, CancellationToken cancellationToken)
    {
        EntityNormalizer.ClearPlex(entity);

        if (!EntityNormalizer.TryNormalizeExternalId(entity))
        {
            logger.LogWarning("Rejected {entityType} with invalid ExternalId", _entityType);
            return new(EntityWriteStatus.InvalidExternalId);
        }

        if (entity.IsEmpty())
        {
            logger.LogError("A {entity} is required and must have some data on it", typeof(TEntity).Name);
            return new(EntityWriteStatus.EmptyEntity);
        }

        logger.LogInformation("Creating new {entityType}", _entityType);
        await entityService.CreateAsync(entity, cancellationToken);
        logger.LogInformation("Created {entityType} with id {id}", _entityType, entity.Id);

        string? imageError = null;

        // The image is stored after the entity so its id can name the file; one more save then records it.
        if (image.HasFile || image.HasUrl)
        {
            var (stored, error) = await images.TryStoreAsync(image, _entityType, entity.Id!, cancellationToken);
            imageError = error;
            if (stored is not null)
            {
                entity.CoverImage = stored;
            }

            // A file takes precedence over any imageUrl on the entity, stored or not.
            if (image.HasFile)
            {
                entity.ImageUrl = null;
            }

            if (stored is not null || image.HasFile)
            {
                await entityService.UpdateAsync(entity.Id!, entity, cancellationToken);
            }
        }

        return new(EntityWriteStatus.Success, entity, imageError);
    }

    public async Task<EntityWriteResult<TEntity>> UpdateAsync(string id, TEntity entity, ImageInput image, CancellationToken cancellationToken)
    {
        EntityNormalizer.ClearPlex(entity);

        if (id != entity.Id)
        {
            logger.LogError("Ids on the entity and the request do not match: {pathId} != {entityId}", id, entity.Id);
            return new(EntityWriteStatus.IdMismatch);
        }

        if (!EntityNormalizer.TryNormalizeExternalId(entity))
        {
            logger.LogWarning("Rejected {entityType}/{id} update with invalid ExternalId", _entityType, id);
            return new(EntityWriteStatus.InvalidExternalId);
        }

        // The existing entity gives the old image to replace or clean up
        var existing = await entityService.GetAsync(id, cancellationToken);
        if (existing is null)
        {
            logger.LogWarning("{entityType} not found for id {id}", _entityType, id);
            return new(EntityWriteStatus.NotFound);
        }

        string? imageError = null;
        Image? replacedImage = null;

        // An entity-level imageUrl is only downloaded when it changed
        var downloadUrl = image.HasUrl && !(image.UrlFromEntity && image.Url == existing.ImageUrl);

        if (image.HasFile || downloadUrl)
        {
            var (stored, error) = await images.TryStoreAsync(image, _entityType, id, cancellationToken);
            imageError = error;
            replacedImage = images.ApplyStoredImage(entity, existing, stored);

            // A file takes precedence over any imageUrl; on failure ApplyStoredImage restored the previous one
            if (image.HasFile && stored is not null)
            {
                entity.ImageUrl = null;
            }
        }
        else if (entity.CoverImage is null && existing.CoverImage is not null)
        {
            logger.LogInformation("Image is being cleared for {entityType}/{id}", _entityType, id);
            replacedImage = existing.CoverImage;
        }

        var result = await entityService.UpdateAsync(id, entity, cancellationToken);
        logger.LogInformation("Updated {entityType} {entityId}: {updated}", _entityType, id, result.ModifiedCount > 0);

        // Delete the replaced file only after the entity references the new image
        images.DeleteFile(replacedImage);
        return new(EntityWriteStatus.Success, entity, imageError);
    }

    public async Task<DeleteResult> DeleteAsync(string id, CancellationToken cancellationToken)
    {
        var entity = await entityService.GetAsync(id, cancellationToken);
        images.DeleteFile(entity?.CoverImage);

        var result = await entityService.RemoveAsync(id, cancellationToken);
        logger.LogInformation("Deleted {entityType} {entityId}: {deleted}", _entityType, id, result.DeletedCount > 0);

        // The entity is deleted first; its Plex data goes after, and a failure there never fails the delete
        await DeletePlexDataAsync(id, cancellationToken);
        return result;
    }

    private async Task DeletePlexDataAsync(string id, CancellationToken cancellationToken)
    {
        // Registered only when the Plex integration is configured and valid; Plex data exists for movies and music only
        if (plexCleanup is null || (typeof(TEntity) != typeof(Movie) && typeof(TEntity) != typeof(Music)))
        {
            return;
        }

        var isMusic = typeof(TEntity) == typeof(Music);
        try
        {
            await (isMusic
                ? plexCleanup.DeleteMusicDataAsync(id, cancellationToken)
                : plexCleanup.DeleteMovieDataAsync(id, cancellationToken));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Failed to delete Plex data for {entityType} {entityId}; the next Plex sync purges it", isMusic ? "music" : "movie", id);
        }
    }

    public async Task<DeleteImageStatus> DeleteImageAsync(string id, CancellationToken cancellationToken)
    {
        logger.LogInformation("Deleting image for {entityType}/{id}", _entityType, id);

        var entity = await entityService.GetAsync(id, cancellationToken);
        if (entity is null)
        {
            logger.LogWarning("{entityType} not found for id {id}", _entityType, id);
            return DeleteImageStatus.EntityNotFound;
        }

        if (entity.CoverImage is null)
        {
            logger.LogWarning("No image found for {entityType}/{id}", _entityType, id);
            return DeleteImageStatus.NoImage;
        }

        images.DeleteFile(entity.CoverImage);

        entity.CoverImage = null;
        await entityService.UpdateAsync(id, entity, cancellationToken);
        logger.LogInformation("Removed image reference from {entityType}/{id}", _entityType, id);
        return DeleteImageStatus.Deleted;
    }
}
