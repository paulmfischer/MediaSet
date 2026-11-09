using MediaSet.Api.Infrastructure.Plex;

namespace MediaSet.Api.Features.Plex.Services;

public class PlexDataCleanup(PlexEnablement enablement, IPlexItemStore items) : IPlexDataCleanup
{
    public Task DeleteMovieDataAsync(string movieId, CancellationToken cancellationToken) =>
        enablement.IsEnabled(PlexEntityTypes.Movies) ? items.DeleteAsync(movieId, cancellationToken) : Task.CompletedTask;
}
