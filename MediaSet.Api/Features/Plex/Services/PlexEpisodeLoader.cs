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
    /// </summary>
    public static async Task<IReadOnlyDictionary<int, IReadOnlyList<PlexEpisode>>> LoadAsync(
        IPlexClient client,
        IReadOnlyList<PlexSeason> seasons,
        PlexScope scope,
        IDictionary<string, IReadOnlyList<PlexEpisode>>? cache,
        CancellationToken cancellationToken)
    {
        var result = new Dictionary<int, IReadOnlyList<PlexEpisode>>();
        foreach (var season in PlexMatching.SeasonsInScope(seasons, scope))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (cache == null || !cache.TryGetValue(season.RatingKey, out var episodes))
            {
                // A client that answers nothing counts as no episodes
                episodes = await client.GetEpisodesAsync(season.RatingKey, cancellationToken) ?? [];
                cache?[season.RatingKey] = episodes;
            }
            result[season.Index] = episodes;
        }
        return result;
    }
}
