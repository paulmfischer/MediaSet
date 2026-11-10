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
}
