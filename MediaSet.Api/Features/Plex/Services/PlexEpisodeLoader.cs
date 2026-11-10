using MediaSet.Api.Features.Plex.Models;
using MediaSet.Api.Infrastructure.Plex;

namespace MediaSet.Api.Features.Plex.Services;

/// <summary>Reads the episodes of the seasons a TV entry's scope covers. Read-only toward Plex.</summary>
public static class PlexEpisodeLoader
{
    /// <summary>
    /// The episodes of every season of the show that the scope covers, by season index. Only seasons Plex has are
    /// requested. The cache (by season rating key) lets several entries of one show share a season read in a run.
    /// A PlexClientException propagates so the caller can fail before it writes anything.
    /// <para>
    /// <paramref name="known"/> are the episodes this entry already stores; the client skips the stream lookup for the
    /// unchanged ones. A cached season may have been read for another entry with its own stored episodes, so a cached
    /// result that skipped lookups is used only when this entry's known episodes cover every skipped episode with the same
    /// updatedAt and size. Otherwise the season is read again with this entry's known episodes, so an entry never gets an
    /// episode without streams it does not have stored. With no known episodes every episode is read in full.
    /// </para>
    /// </summary>
    public static async Task<IReadOnlyDictionary<int, IReadOnlyList<PlexEpisode>>> LoadAsync(
        IPlexClient client,
        IReadOnlyList<PlexSeason> seasons,
        PlexScope scope,
        IDictionary<string, IReadOnlyList<PlexEpisode>>? cache,
        IReadOnlyDictionary<string, PlexKnownMedia>? known,
        CancellationToken cancellationToken)
    {
        var result = new Dictionary<int, IReadOnlyList<PlexEpisode>>();
        foreach (var season in PlexMatching.SeasonsInScope(seasons, scope))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (cache == null || !cache.TryGetValue(season.RatingKey, out var episodes) || !UsableFor(episodes, known))
            {
                // A client that answers nothing counts as no episodes
                episodes = await client.GetEpisodesAsync(season.RatingKey, cancellationToken, known) ?? [];
                cache?[season.RatingKey] = episodes;
            }
            result[season.Index] = episodes;
        }
        return result;
    }

    private static bool UsableFor(IReadOnlyList<PlexEpisode> episodes, IReadOnlyDictionary<string, PlexKnownMedia>? known) =>
        episodes.All(e => !e.StreamsReused
            || (known != null && known.TryGetValue(e.RatingKey, out var k) && k.Matches(e.UpdatedAt, e.Versions)));
}
