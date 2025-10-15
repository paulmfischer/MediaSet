using MediaSet.Api.Services;

namespace MediaSet.Api.Metadata;

internal static class StatsApi
{
  public static RouteGroupBuilder MapStats(this IEndpointRouteBuilder routes)
  {
    var group = routes.MapGroup("/stats");

    group.WithTags("Stats");

    group.MapGet("/", async (IStatsService statsService) => await statsService.GetMediaStatsAsync());

    return group;
  }
}
