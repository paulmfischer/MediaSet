using MediaSet.Api.Shared.Models;

namespace MediaSet.Api.Features.Entities.Services;

/// <summary>Rules applied to every entity a client writes, before it is stored.</summary>
internal static class EntityNormalizer
{
    // Plex data is read-only and lives in its own collection: whatever a client sends in `plex` is dropped.
    public static void ClearPlex(IEntity entity)
    {
        if (entity is Movie movie)
        {
            movie.Plex = null;
        }
        else if (entity is Music music)
        {
            music.Plex = null;
        }
    }

    /// <summary>
    /// Validates and normalizes a Movie's or Music entry's ExternalId in place (source lowercased, value untouched).
    /// Returns false when the value is malformed; other entity types are always valid.
    /// </summary>
    public static bool TryNormalizeExternalId(IEntity entity)
    {
        switch (entity)
        {
            case Movie movie:
                if (!ExternalIdFormat.TryNormalize(movie.ExternalId, out var movieId))
                {
                    return false;
                }

                movie.ExternalId = movieId;
                return true;
            case Music music:
                if (!ExternalIdFormat.TryNormalize(music.ExternalId, out var musicId))
                {
                    return false;
                }

                music.ExternalId = musicId;
                return true;
            default:
                return true;
        }
    }
}
