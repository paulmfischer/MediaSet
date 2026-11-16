using MediaSet.Api.Infrastructure.Plex;

namespace MediaSet.Api.Features.Plex.Services;

public class PlexDataCleanup(PlexEnablement enablement, IPlexItemStore items) : IPlexDataCleanup
{
    public Task DeleteMovieDataAsync(string movieId, CancellationToken cancellationToken) =>
        enablement.IsEnabled(PlexEntityTypes.Movies) ? items.DeleteAsync(movieId, cancellationToken) : Task.CompletedTask;

    public Task DeleteMusicDataAsync(string musicId, CancellationToken cancellationToken) =>
        enablement.IsEnabled(PlexEntityTypes.Music) ? items.DeleteAsync(musicId, cancellationToken) : Task.CompletedTask;
}
