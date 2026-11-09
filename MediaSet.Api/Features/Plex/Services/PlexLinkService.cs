using MediaSet.Api.Features.Plex.Models;
using MediaSet.Api.Infrastructure.Plex;

namespace MediaSet.Api.Features.Plex.Services;

public interface IPlexLinkService
{
    /// <summary>
    /// Links an entry to a Plex item by hand and stores that item's data. Replaces any existing link. Without a
    /// scope the item must be a movie. With a scope (a TV series entry) it must be a show, and the Plex seasons the
    /// scope covers are stored; the scope must be readable. Null when Plex has no such item of that type.
    /// </summary>
    Task<PlexItemData?> LinkAsync(string entityType, string movieId, string plexItemId, CancellationToken cancellationToken, PlexScope? scope = null);

    /// <summary>Removes the link and its stored Plex data. False when the movie was not linked.</summary>
    Task<bool> UnlinkAsync(string movieId, CancellationToken cancellationToken);
}

/// <summary>
/// Manual link and unlink. These deliberately do not take the sync lock: they are short, user-driven and
/// touch one document, and blocking them behind a long full sync would make linking unusable while it runs.
/// The sync re-checks for a manual link right before it writes an automatic match, so a link made mid-run
/// is not replaced. An unlink made mid-run may be re-matched automatically by that run. The sync's re-check
/// is still check-then-act (read, then replace), so a link landing in the instant between them can be
/// overwritten once; closing that would need a conditional store write. Two entries may link to the same Plex
/// item: the link does not enforce uniqueness of the Plex item id.
/// </summary>
public class PlexLinkService(IPlexClient client, IPlexItemStore store) : IPlexLinkService
{
    public async Task<PlexItemData?> LinkAsync(string entityType, string movieId, string plexItemId, CancellationToken cancellationToken, PlexScope? scope = null)
    {
        if (PlexEntityTypes.Normalize(entityType) == null || scope is { IsReadable: false })
        {
            return null;
        }

        var item = await client.GetItemAsync(plexItemId, true, cancellationToken);
        if (item == null || !string.Equals(item.Type, PlexEntityTypes.ItemType(scope != null), StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var data = scope == null
            ? PlexMatching.Build(movieId, item, DateTime.UtcNow, PlexLinkSources.Manual)
            : PlexMatching.BuildShow(movieId, item, await client.GetSeasonsAsync(item.RatingKey, cancellationToken), scope, DateTime.UtcNow, PlexLinkSources.Manual);
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
