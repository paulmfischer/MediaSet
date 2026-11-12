using MediaSet.Api.Features.Plex.Models;
using MediaSet.Api.Infrastructure.DataAccess;
using MediaSet.Api.Shared.Models;

namespace MediaSet.Api.Features.Plex.Services;

public interface IPlexStatisticsService
{
    Task<PlexStatisticsDto> GetAsync(CancellationToken cancellationToken);
}

/// <summary>Reads the projected Plex documents and the movie list, and hands them to <see cref="PlexStatisticsCalculator"/>.</summary>
public class PlexStatisticsService(IPlexItemStore items, IEntityService<Movie> movies) : IPlexStatisticsService
{
    public async Task<PlexStatisticsDto> GetAsync(CancellationToken cancellationToken)
    {
        var entries = (await movies.GetListAsync(cancellationToken)).ToList();
        var docs = await items.ListStatisticsProjectionAsync(cancellationToken);
        return PlexStatisticsCalculator.Compute(entries, docs);
    }
}
