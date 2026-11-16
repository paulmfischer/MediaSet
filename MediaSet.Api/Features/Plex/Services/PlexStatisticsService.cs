using MediaSet.Api.Features.Plex.Models;
using MediaSet.Api.Infrastructure.DataAccess;
using MediaSet.Api.Infrastructure.Plex;
using MediaSet.Api.Shared.Models;

namespace MediaSet.Api.Features.Plex.Services;

public interface IPlexStatisticsService
{
    Task<PlexStatisticsDto> GetAsync(CancellationToken cancellationToken);
}

/// <summary>
/// Reads the projected Plex documents and the entry lists of each enabled entity type, and hands them to
/// <see cref="PlexStatisticsCalculator"/>. A section of an entity type that is not enabled is left out.
/// </summary>
public class PlexStatisticsService(
    IPlexItemStore items,
    IEntityService<Movie> movies,
    IEntityService<Music> music,
    PlexEnablement enablement) : IPlexStatisticsService
{
    public async Task<PlexStatisticsDto> GetAsync(CancellationToken cancellationToken)
    {
        PlexEntityStatisticsDto? movieSection = null;
        PlexEntityStatisticsDto? tvSection = null;
        PlexMusicStatisticsDto? musicSection = null;

        if (enablement.IsEnabled(PlexEntityTypes.Movies))
        {
            var entries = (await movies.GetListAsync(cancellationToken)).ToList();
            var docs = await items.ListStatisticsProjectionAsync(cancellationToken);
            var computed = PlexStatisticsCalculator.Compute(entries, docs);
            movieSection = computed.Movies;
            tvSection = computed.Tv;
        }

        if (enablement.IsEnabled(PlexEntityTypes.Music))
        {
            var entries = (await music.GetListAsync(cancellationToken)).ToList();
            var docs = await items.ListMusicStatisticsProjectionAsync(cancellationToken);
            musicSection = PlexStatisticsCalculator.ComputeMusic(entries, docs);
        }

        return new PlexStatisticsDto(movieSection, tvSection, musicSection);
    }
}
