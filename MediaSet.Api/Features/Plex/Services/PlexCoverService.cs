using MediaSet.Api.Infrastructure.DataAccess;
using MediaSet.Api.Infrastructure.Plex;
using MediaSet.Api.Infrastructure.Storage;
using MediaSet.Api.Shared.Models;
using SkiaSharp;

namespace MediaSet.Api.Features.Plex.Services;

public interface IPlexCoverService
{
    /// <summary>
    /// Gives a movie without a cover the poster of the Plex item it is paired with. Does nothing when the movie already
    /// has a cover, or when Plex has no poster, only a small one, or cannot be read. Never throws (except on
    /// cancellation), so pairing and syncing never fail because of a cover. True when a cover was saved.
    /// </summary>
    Task<bool> ApplyIfMissingAsync(string movieId, string plexRatingKey, CancellationToken cancellationToken);
}

public class PlexCoverService(
    IPlexClient client,
    IImageService imageService,
    IEntityService<Movie> movies,
    ILogger<PlexCoverService> logger) : IPlexCoverService
{
    /// <summary>Narrowest poster saved, in pixels. Plex serves small thumbnails for some items; those are skipped.</summary>
    public const int MinWidth = 400;

    /// <summary>Largest poster read from Plex, in bytes.</summary>
    public const long MaxBytes = 10 * 1024 * 1024;

    public async Task<bool> ApplyIfMissingAsync(string movieId, string plexRatingKey, CancellationToken cancellationToken)
    {
        Image? saved = null;
        try
        {
            var movie = await movies.GetAsync(movieId, cancellationToken);
            if (movie == null || movie.CoverImage != null)
            {
                return false;
            }

            var data = await client.GetPosterAsync(plexRatingKey, MaxBytes, cancellationToken);
            if (data == null)
            {
                logger.LogDebug("Plex has no readable poster for movie {MovieId}", movieId);
                return false;
            }

            using (var encoded = SKData.CreateCopy(data))
            using (var codec = SKCodec.Create(encoded))
            {
                if (codec == null || codec.Info.Width < MinWidth)
                {
                    logger.LogInformation("Plex poster for movie {MovieId} is missing or too small to use as a cover", movieId);
                    return false;
                }
            }

            saved = await imageService.SaveImageBytesAsync(data, nameof(MediaTypes.Movies), movieId, cancellationToken);

            // Re-read right before writing so a cover added meanwhile is never replaced
            var current = await movies.GetAsync(movieId, cancellationToken);
            if (current == null || current.CoverImage != null)
            {
                DeleteQuietly(saved);
                return false;
            }

            current.CoverImage = saved;
            await movies.UpdateAsync(movieId, current, cancellationToken);
            logger.LogInformation("Saved Plex poster as cover for movie {MovieId}", movieId);
            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Type only: never the message, which could hold a Plex address
            logger.LogWarning("Could not use the Plex poster as a cover for movie {MovieId} ({ExceptionType})", movieId, ex.GetType().Name);
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
