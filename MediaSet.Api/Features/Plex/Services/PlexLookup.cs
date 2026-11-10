using MediaSet.Api.Features.Plex.Models;
using MediaSet.Api.Infrastructure.Plex;

namespace MediaSet.Api.Features.Plex.Services;

public class PlexLookup(PlexEnablement enablement, IPlexItemStore items) : IPlexLookup
{
    public async Task<object?> GetMovieDataAsync(string movieId, CancellationToken cancellationToken)
    {
        // Plex documents are keyed by ObjectId; any other id cannot be linked
        if (!enablement.IsEnabled(PlexEntityTypes.Movies) || !MongoDB.Bson.ObjectId.TryParse(movieId, out _))
        {
            return null;
        }

        var data = await items.GetAsync(movieId, cancellationToken);
        return data == null ? null : PlexItemDataDto.From(data).WithoutFilePath();
    }
}
