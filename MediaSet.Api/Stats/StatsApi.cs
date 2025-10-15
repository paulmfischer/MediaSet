using MediaSet.Api.Services;

namespace MediaSet.Api.Metadata;

internal static class StatsApi
{
  public static RouteGroupBuilder MapStats(this IEndpointRouteBuilder routes)
  {
    var logger = ((WebApplication)routes).Logger;
    var group = routes.MapGroup("/stats");

    group.WithTags("Stats");

    group.MapGet("/", async (IStatsService statsService) =>
    {
      logger.LogInformation("Requesting media stats");
      var stats = await statsService.GetMediaStatsAsync();
      if (stats is not null)
      {
        logger.LogInformation(
          "Stats: books total={bookTotal}, formats={bookFormats}, pages={bookPages}; movies total={movieTotal}, formats={movieFormats}, tvSeries={tvSeries}",
          stats.BookStats.Total,
          stats.BookStats.TotalFormats,
          stats.BookStats.TotalPages,
          stats.MovieStats.Total,
          stats.MovieStats.TotalFormats,
          stats.MovieStats.TotalTvSeries
        );
      }
      return stats;
    });

    return group;
  }
}
