using MediaSet.Api.Features.Statistics.Services;
using MediaSet.Api.Features.Statistics.Models;
using MediaSet.Api.Features.Entities.Models;
using MediaSet.Api.Services;

namespace MediaSet.Api.Features.Statistics.Endpoints;

internal static class StatsApi
{
    public static RouteGroupBuilder MapStats(this IEndpointRouteBuilder routes)
    {
        var logger = ((WebApplication)routes).Logger;
        var group = routes.MapGroup("/stats");

        group.WithTags("Stats");

        group.MapGet("/", async (IStatsService statsService, CancellationToken cancellationToken) =>
        {
            logger.LogInformation("Requesting media stats");
            var stats = await statsService.GetMediaStatsAsync(cancellationToken);
            if (stats is not null)
            {
                logger.LogInformation(
              "Stats: books total={bookTotal}, formats={bookFormats}, pages={bookPages}; movies total={movieTotal}, formats={movieFormats}, tvSeries={tvSeries}; games total={gameTotal}, formats={gameFormats}, platforms={gamePlatforms}; music total={musicTotal}, formats={musicFormats}, tracks={musicTracks}",
              stats.BookStats.Total,
              stats.BookStats.TotalFormats,
              stats.BookStats.TotalPages,
              stats.MovieStats.Total,
              stats.MovieStats.TotalFormats,
              stats.MovieStats.TotalTvSeries,
              stats.GameStats.Total,
              stats.GameStats.TotalFormats,
              stats.GameStats.TotalPlatforms,
              stats.MusicStats.Total,
              stats.MusicStats.TotalFormats,
              stats.MusicStats.TotalTracks
            );
            }
            return stats;
        });

        return group;
    }
}
