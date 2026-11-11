using MediaSet.Api.Shared.Models;

namespace MediaSet.Api.Infrastructure.Plex;

/// <summary>
/// Reads the stored Plex data of a movie for the entity lookup response. Registered only when the Plex integration
/// is enabled, so callers resolve it optionally and add nothing when it is absent.
/// </summary>
public interface IPlexLookup
{
    /// <summary>
    /// The Plex data of the movie without any file path, or null when Plex is not enabled for movies, the id is not
    /// an ObjectId or the movie is not linked.
    /// </summary>
    Task<object?> GetMovieDataAsync(string movieId, CancellationToken cancellationToken);

    /// <summary>
    /// The small Plex summaries (no file path) of the linked movies among the ids, keyed by movie id, read in one query.
    /// Empty when Plex is not enabled for movies or a read fails.
    /// </summary>
    Task<IReadOnlyDictionary<string, object>> GetMovieSummariesAsync(IReadOnlyCollection<string> movieIds, CancellationToken cancellationToken);

    /// <summary>
    /// The id restriction for a list filter (onPlex, notOnPlex, unwatched or broken, case-insensitive), or null when Plex is
    /// not enabled for movies. Throws <see cref="ArgumentException"/> for an unknown filter value.
    /// </summary>
    Task<IdFilter?> GetMovieFilterAsync(string filter, CancellationToken cancellationToken);
}
