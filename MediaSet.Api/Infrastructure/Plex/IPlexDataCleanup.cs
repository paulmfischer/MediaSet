namespace MediaSet.Api.Infrastructure.Plex;

/// <summary>
/// Removes the stored Plex data of a deleted entity. Registered only when the Plex integration is enabled,
/// so callers resolve it optionally and skip the cleanup when it is absent.
/// </summary>
public interface IPlexDataCleanup
{
    Task DeleteMovieDataAsync(string movieId, CancellationToken cancellationToken);
}
