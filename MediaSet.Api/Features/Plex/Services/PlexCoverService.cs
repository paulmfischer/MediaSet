using MediaSet.Api.Infrastructure.DataAccess;
using MediaSet.Api.Infrastructure.Plex;
using MediaSet.Api.Infrastructure.Storage;
using MediaSet.Api.Shared.Models;
using SkiaSharp;

namespace MediaSet.Api.Features.Plex.Services;

/// <summary>
/// Cover fallback from Plex for any entity type with a cover. TV series entries are Movie entities too
/// (<c>Movie.IsTvSeries</c>), so <c>IPlexCoverService&lt;Movie&gt;</c> covers movies and TV series alike. The media type the image is saved
/// under follows from <typeparamref name="TEntity"/>.
/// </summary>
public interface IPlexCoverService<TEntity> where TEntity : class, IEntity
{
    /// <summary>
    /// Like <see cref="ApplyIfMissingAsync"/> but says why no cover was saved, so a caller can avoid repeating a fetch that
    /// is known to be unusable. Never throws (except on cancellation).
    /// </summary>
    Task<PlexCoverOutcome> TryApplyAsync(string entityId, string plexRatingKey, CancellationToken cancellationToken);

    /// <summary>
    /// Gives an entity without a cover the image of the Plex item it is paired with. Does nothing when the entity
    /// already has a cover, or when Plex has no image, only a small one, or cannot be read. Never throws (except on
    /// cancellation), so pairing and syncing never fail because of a cover. True when a cover was saved.
    /// </summary>
    Task<bool> ApplyIfMissingAsync(string entityId, string plexRatingKey, CancellationToken cancellationToken);
}

/// <summary>The result of a cover attempt. <see cref="Failed"/> is the default so an unset mock result never reads as a known dead end.</summary>
public enum PlexCoverOutcome
{
    /// <summary>An error, or a change made meanwhile: worth trying again next time.</summary>
    Failed = 0,

    /// <summary>A cover was saved.</summary>
    Saved,

    /// <summary>The entity has a cover (or is gone), so nothing was read from Plex.</summary>
    NotNeeded,

    /// <summary>Plex has no readable image, or only a small or non-image one: repeating the fetch finds the same until the item changes.</summary>
    Unusable
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

    // From the entity class, not the instance: a deserialized entity can carry a default Type
    private static readonly string _mediaTypeName = (typeof(TEntity) switch
    {
        var t when t == typeof(Movie) => MediaTypes.Movies,
        var t when t == typeof(Music) => MediaTypes.Musics,
        var t when t == typeof(Book) => MediaTypes.Books,
        var t when t == typeof(Game) => MediaTypes.Games,
        _ => throw new NotSupportedException($"No media type for {typeof(TEntity).Name}")
    }).ToString();

    public async Task<bool> ApplyIfMissingAsync(string entityId, string plexRatingKey, CancellationToken cancellationToken) =>
        await TryApplyAsync(entityId, plexRatingKey, cancellationToken) == PlexCoverOutcome.Saved;

    public async Task<PlexCoverOutcome> TryApplyAsync(string entityId, string plexRatingKey, CancellationToken cancellationToken)
    {
        Image? saved = null;
        try
        {
            var entity = await entities.GetAsync(entityId, cancellationToken);
            if (entity == null || entity.CoverImage != null)
            {
                return PlexCoverOutcome.NotNeeded;
            }

            var data = await client.GetThumbAsync(plexRatingKey, MaxBytes, cancellationToken);
            if (data == null)
            {
                logger.LogDebug("Plex has no readable image for {EntityType} {EntityId}", typeof(TEntity).Name, entityId);
                return PlexCoverOutcome.Unusable;
            }

            using (var encoded = SKData.CreateCopy(data))
            using (var codec = SKCodec.Create(encoded))
            {
                if (codec == null || codec.Info.Width < MinWidth)
                {
                    logger.LogInformation("Plex image for {EntityType} {EntityId} is missing or too small to use as a cover", typeof(TEntity).Name, entityId);
                    return PlexCoverOutcome.Unusable;
                }
            }

            saved = await imageService.SaveImageBytesAsync(data, _mediaTypeName, entityId, cancellationToken);

            // Re-read right before writing so a cover added meanwhile is never replaced
            var current = await entities.GetAsync(entityId, cancellationToken);
            if (current == null || current.CoverImage != null)
            {
                DeleteQuietly(saved);
                return PlexCoverOutcome.NotNeeded;
            }

            current.CoverImage = saved;
            await entities.UpdateAsync(entityId, current, cancellationToken);
            logger.LogInformation("Saved Plex image as cover for {EntityType} {EntityId}", typeof(TEntity).Name, entityId);
            return PlexCoverOutcome.Saved;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Type only: never the message, which could hold a Plex address
            logger.LogWarning("Could not use the Plex image as a cover for {EntityType} {EntityId} ({ExceptionType})", typeof(TEntity).Name, entityId, ex.GetType().Name);
            if (saved != null)
            {
                DeleteQuietly(saved);
            }
            return PlexCoverOutcome.Failed;
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
