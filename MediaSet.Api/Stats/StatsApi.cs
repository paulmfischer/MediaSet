using MediaSet.Api.Services;

namespace MediaSet.Api.Metadata;

internal static class StatsApi
{
  public static RouteGroupBuilder MapStats(this IEndpointRouteBuilder routes)
  {
    var group = routes.MapGroup("/stats");

    group.WithTags("Stats");

    group.MapGet("/", async (StatsService statsService) => await statsService.GetBookStats());
    // group.MapGet("/", async (BookService booksService) => await booksService.GetAsync());

    return group;
  }
}