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
        group.MapGet("/status", async (PlexEnablement enablement, IPlexSyncRunStore runs, CancellationToken cancellationToken) =>
        {
            var statuses = new List<PlexEntityStatusDto>();
            foreach (var type in PlexEntityTypes.Supported)
            {
                var enabled = enablement.IsEnabled(type);
                var last = enabled ? await runs.GetLastAsync(type, cancellationToken) : null;
                statuses.Add(new PlexEntityStatusDto(type, enabled, last == null ? null : PlexSyncRunDto.From(last), null));
            }

            return Results.Ok(statuses);
        });

        // Per-entity routes: the filter rejects unknown and disabled entity types
        var entity = group.MapGroup("/{entityType}").AddEndpointFilter<PlexEntityTypeFilter>();

        // GET /plex/{entityType}/status
        entity.MapGet("/status", async (HttpContext context, IPlexSyncRunStore runs, CancellationToken cancellationToken) =>
        {
            var entityType = PlexEntityTypeFilter.GetEntityType(context);
            var last = await runs.GetLastAsync(entityType, cancellationToken);
            return Results.Ok(new PlexEntityStatusDto(entityType, true, last == null ? null : PlexSyncRunDto.From(last), null));
        });

        // POST /plex/{entityType}/sync: starts a run in the background (202), or 409 with the current run
        entity.MapPost("/sync", async (HttpContext context, IPlexSyncService sync, CancellationToken cancellationToken) =>
        {
            var result = await sync.StartAsync(PlexEntityTypeFilter.GetEntityType(context), PlexSyncTriggers.Manual, cancellationToken);
            var run = PlexSyncRunDto.From(result.Run);
            if (!result.Started)
            {
                return Results.Problem(
                    title: "Plex sync already in progress",
                    detail: $"A Plex sync for '{run.EntityType}' is already {run.State}.",
                    statusCode: StatusCodes.Status409Conflict,
                    extensions: new Dictionary<string, object?> { ["run"] = run });
            }

            return Results.Accepted((string?)null, run);
        });

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
