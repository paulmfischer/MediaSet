using MediaSet.Api.Features.Plex.Models;
using MediaSet.Api.Infrastructure.Plex;

namespace MediaSet.Api.Features.Plex.Services;

public class PlexLookup(PlexEnablement enablement, IPlexItemStore items, ILogger<PlexLookup> logger) : IPlexLookup
{
    public async Task<object?> GetMovieDataAsync(string movieId, CancellationToken cancellationToken)
    {
        // Plex documents are keyed by ObjectId; any other id cannot be linked
        if (!enablement.IsEnabled(PlexEntityTypes.Movies) || !MongoDB.Bson.ObjectId.TryParse(movieId, out _))
        {
            return null;
        }

        // Plex data is a bonus on the lookup: a store failure must not fail the movie request
        try
        {
            var data = await items.GetAsync(movieId, cancellationToken);
            return data == null ? null : PlexItemDataDto.From(data).WithoutFilePath();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Could not read the Plex data of movie {MovieId}; returning the movie without it", movieId);
            return null;
        }
    }
}
