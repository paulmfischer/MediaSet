using MediaSet.Api.Features.Plex.Models;
using MediaSet.Api.Infrastructure.DataAccess;
using MediaSet.Api.Infrastructure.Plex;
using MediaSet.Api.Shared.Models;

namespace MediaSet.Api.Features.Plex.Services;

public interface IPlexStatisticsService
{
    /// <summary>
    /// The statistics of every enabled entity type, or only of <paramref name="entityTypes"/> (canonical names) when given.
    /// A section that is not requested is not computed, so a caller that needs only music does not read the movies.
    /// </summary>
    Task<PlexStatisticsDto> GetAsync(CancellationToken cancellationToken, IReadOnlyCollection<string>? entityTypes = null);
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
    public async Task<PlexStatisticsDto> GetAsync(CancellationToken cancellationToken, IReadOnlyCollection<string>? entityTypes = null)
    {
        bool Wanted(string type) => enablement.IsEnabled(type) && (entityTypes == null || entityTypes.Contains(type));

        PlexEntityStatisticsDto? movieSection = null;
        PlexEntityStatisticsDto? tvSection = null;
        PlexMusicStatisticsDto? musicSection = null;

        if (Wanted(PlexEntityTypes.Movies))
        {
            var entries = (await movies.GetListAsync(cancellationToken)).ToList();
            var docs = await items.ListStatisticsProjectionAsync(cancellationToken);
            var computed = PlexStatisticsCalculator.Compute(entries, docs);
            movieSection = computed.Movies;
            tvSection = computed.Tv;
        }

        if (Wanted(PlexEntityTypes.Music))
        {
            var entries = (await music.GetListAsync(cancellationToken)).ToList();
            var docs = await items.ListMusicStatisticsProjectionAsync(cancellationToken);
            musicSection = PlexStatisticsCalculator.ComputeMusic(entries, docs);
        }

        return new PlexStatisticsDto(movieSection, tvSection, musicSection);
    }
}
