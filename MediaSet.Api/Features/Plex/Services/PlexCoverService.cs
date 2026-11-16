using MediaSet.Api.Infrastructure.DataAccess;
using MediaSet.Api.Infrastructure.Plex;
using MediaSet.Api.Infrastructure.Storage;
using MediaSet.Api.Shared.Models;
using SkiaSharp;

namespace MediaSet.Api.Features.Plex.Services;

/// <summary>
/// Cover fallback from Plex for any entity type with a cover. TV series entries are Movie entities too
/// (<c>Movie.IsTvSeries</c>), so <c>IPlexCoverService&lt;Movie&gt;</c> covers movies and TV series alike. The entity's own
/// <see cref="IEntity.Type"/> decides which media type the image is saved under.
/// </summary>
public interface IPlexCoverService<TEntity> where TEntity : class, IEntity
{
    /// <summary>
    /// Gives an entity without a cover the image of the Plex item it is paired with. Does nothing when the entity
    /// already has a cover, or when Plex has no image, only a small one, or cannot be read. Never throws (except on
    /// cancellation), so pairing and syncing never fail because of a cover. True when a cover was saved.
    /// </summary>
    Task<bool> ApplyIfMissingAsync(string entityId, string plexRatingKey, CancellationToken cancellationToken);
}

public class PlexCoverService<TEntity>(
    IPlexClient client,
    IImageService imageService,
    IEntityService<TEntity> entities,
    ILogger<PlexCoverService<TEntity>> logger) : IPlexCoverService<TEntity> where TEntity : class, IEntity
{
    /// <summary>Narrowest image saved, in pixels. Plex serves small thumbnails for some items; those are skipped.</summary>
    public const int MinWidth = 400;

    /// <summary>Largest image read from Plex, in bytes.</summary>
    public const long MaxBytes = 10 * 1024 * 1024;

    public async Task<bool> ApplyIfMissingAsync(string entityId, string plexRatingKey, CancellationToken cancellationToken)
    {
        Image? saved = null;
        try
        {
            var entity = await entities.GetAsync(entityId, cancellationToken);
            if (entity == null || entity.CoverImage != null)
            {
                return false;
            }

            var data = await client.GetPosterAsync(plexRatingKey, MaxBytes, cancellationToken);
            if (data == null)
            {
                logger.LogDebug("Plex has no readable image for {EntityType} {EntityId}", typeof(TEntity).Name, entityId);
                return false;
            }

            using (var encoded = SKData.CreateCopy(data))
            using (var codec = SKCodec.Create(encoded))
            {
                if (codec == null || codec.Info.Width < MinWidth)
                {
                    logger.LogInformation("Plex image for {EntityType} {EntityId} is missing or too small to use as a cover", typeof(TEntity).Name, entityId);
                    return false;
                }
            }

            saved = await imageService.SaveImageBytesAsync(data, entity.Type.ToString(), entityId, cancellationToken);

            // Re-read right before writing so a cover added meanwhile is never replaced
            var current = await entities.GetAsync(entityId, cancellationToken);
            if (current == null || current.CoverImage != null)
            {
                DeleteQuietly(saved);
                return false;
            }

            current.CoverImage = saved;
            await entities.UpdateAsync(entityId, current, cancellationToken);
            logger.LogInformation("Saved Plex image as cover for {EntityType} {EntityId}", typeof(TEntity).Name, entityId);
            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Type only: never the message, which could hold a Plex address
            logger.LogWarning("Could not use the Plex image as a cover for {EntityType} {EntityId} ({ExceptionType})", typeof(TEntity).Name, entityId, ex.GetType().Name);
            if (saved != null)
            {
                DeleteQuietly(saved);
            }
            return false;
        }
    }

    private void DeleteQuietly(Image image)
    {
        try
        {
            imageService.DeleteImageAsync(image.FilePath);
        }
        catch (Exception ex)
        {
            logger.LogWarning("Could not remove an unused cover file ({ExceptionType})", ex.GetType().Name);
        }
    }
}
