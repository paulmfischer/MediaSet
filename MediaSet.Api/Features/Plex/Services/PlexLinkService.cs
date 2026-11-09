using MediaSet.Api.Features.Plex.Models;
using MediaSet.Api.Infrastructure.Plex;

namespace MediaSet.Api.Features.Plex.Services;

public interface IPlexLinkService
{
    /// <summary>
    /// Links a movie to a Plex item by hand and stores that item's data. Replaces any existing link. Null when
    /// Plex has no such item for the entity type.
    /// </summary>
    Task<PlexItemData?> LinkAsync(string entityType, string movieId, string plexItemId, CancellationToken cancellationToken);

    /// <summary>Removes the link and its stored Plex data. False when the movie was not linked.</summary>
    Task<bool> UnlinkAsync(string movieId, CancellationToken cancellationToken);
}

/// <summary>
/// Manual link and unlink. These deliberately do not take the sync lock: they are short, user-driven and
/// touch one document, and blocking them behind a long full sync would make linking unusable while it runs.
/// The sync re-checks for a manual link right before it writes an automatic match, so a link made mid-run
/// is not replaced. An unlink made mid-run may be re-matched automatically by that run.
/// </summary>
public class PlexLinkService(IPlexClient client, IPlexItemStore store) : IPlexLinkService
{
    public async Task<PlexItemData?> LinkAsync(string entityType, string movieId, string plexItemId, CancellationToken cancellationToken)
    {
        var item = await client.GetItemAsync(plexItemId, true, cancellationToken);
        if (item == null || !string.Equals(item.Type, PlexEntityTypes.LibraryType(entityType), StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var data = PlexMatching.Build(movieId, item, DateTime.UtcNow, PlexLinkSources.Manual);
        await store.UpsertAsync(data, cancellationToken);
        return data;
    }

    public async Task<bool> UnlinkAsync(string movieId, CancellationToken cancellationToken)
    {
        if (await store.GetAsync(movieId, cancellationToken) == null)
        {
            return false;
        }

        await store.DeleteAsync(movieId, cancellationToken);
        return true;
    }
}
