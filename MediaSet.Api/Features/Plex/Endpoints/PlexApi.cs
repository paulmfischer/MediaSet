using MediaSet.Api.Features.Plex.Models;
using MediaSet.Api.Features.Plex.Services;
using MediaSet.Api.Infrastructure.Plex;

namespace MediaSet.Api.Features.Plex.Endpoints;

internal static class PlexApi
{
    public static RouteGroupBuilder MapPlex(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/plex");
        group.WithTags("Plex");

        // GET /plex/status: one entry per supported entity type
        group.MapGet("/status", (PlexEnablement enablement) =>
            Results.Ok(PlexEntityTypes.Supported
                .Select(t => new PlexEntityStatusDto(t, enablement.IsEnabled(t), null, null))
                .ToList()));

        // Per-entity routes: the filter rejects unknown and disabled entity types
        var entity = group.MapGroup("/{entityType}").AddEndpointFilter<PlexEntityTypeFilter>();

        // GET /plex/{entityType}/status
        entity.MapGet("/status", (HttpContext context) =>
            Results.Ok(new PlexEntityStatusDto(PlexEntityTypeFilter.GetEntityType(context), true, null, null)));

        // GET /plex/{entityType}/search?query=...: title search for manual linking
        entity.MapGet("/search", async (
            HttpContext context,
            string? query,
            IPlexSearchService search,
            CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(query) || query.Trim().Length < PlexSearchService.MinQueryLength)
            {
                return Results.Problem(
                    title: "Query required",
                    detail: $"The 'query' parameter is required and must be at least {PlexSearchService.MinQueryLength} characters.",
                    statusCode: StatusCodes.Status400BadRequest);
            }

            try
            {
                var results = await search.SearchAsync(PlexEntityTypeFilter.GetEntityType(context), query.Trim(), cancellationToken);
                return Results.Ok(results);
            }
            catch (PlexClientException ex)
            {
                // The exception message holds no URL or token
                return Results.Problem(
                    title: "Plex request failed",
                    detail: ex.Message,
                    statusCode: StatusCodes.Status502BadGateway);
            }
        });

        return group;
    }
}
