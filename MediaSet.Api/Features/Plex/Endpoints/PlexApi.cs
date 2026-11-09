using MediaSet.Api.Features.Plex.Models;
using MediaSet.Api.Features.Plex.Services;
using MediaSet.Api.Infrastructure.DataAccess;
using MediaSet.Api.Infrastructure.Plex;
using MediaSet.Api.Shared.Models;

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

        // GET /plex/{entityType}/items/{movieId}: the Plex data of one linked movie, 404 when unlinked.
        // The only response that carries the file path.
        entity.MapGet("/items/{id}", async (string id, IPlexItemStore items, CancellationToken cancellationToken) =>
        {
            // Plex documents are keyed by ObjectId; any other id cannot be linked
            var data = MongoDB.Bson.ObjectId.TryParse(id, out _) ? await items.GetAsync(id, cancellationToken) : null;
            return data == null
                ? Results.Problem(
                    title: "Not linked to Plex",
                    detail: "This item has no Plex data.",
                    statusCode: StatusCodes.Status404NotFound)
                : Results.Ok(PlexItemDataDto.From(data));
        });

        // PUT /plex/{entityType}/items/{id}/link: links a movie to a Plex item by hand and loads its data now
        entity.MapPut("/items/{id}/link", async (
            HttpContext context,
            string id,
            PlexLinkRequest? body,
            IEntityService<Movie> movies,
            IPlexLinkService links,
            CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(body?.PlexItemId))
            {
                return Results.Problem(
                    title: "Plex item id required",
                    detail: "The request body must contain a 'plexItemId'.",
                    statusCode: StatusCodes.Status400BadRequest);
            }

            if (await MovieMissingAsync(id, movies, cancellationToken))
            {
                return MovieNotFound();
            }

            try
            {
                var data = await links.LinkAsync(PlexEntityTypeFilter.GetEntityType(context), id, body.PlexItemId.Trim(), cancellationToken);
                return data == null ? PlexItemNotFound(body.PlexItemId.Trim()) : Results.Ok(PlexItemDataDto.From(data));
            }
            catch (PlexClientException ex)
            {
                return PlexFailed(ex);
            }
        });

        // DELETE /plex/{entityType}/items/{id}/link: removes the link and its stored Plex data
        entity.MapDelete("/items/{id}/link", async (
            string id,
            IEntityService<Movie> movies,
            IPlexLinkService links,
            CancellationToken cancellationToken) =>
        {
            if (await MovieMissingAsync(id, movies, cancellationToken))
            {
                return MovieNotFound();
            }

            return await links.UnlinkAsync(id, cancellationToken)
                ? Results.NoContent()
                : Results.Problem(
                    title: "Not linked to Plex",
                    detail: "This item has no Plex link to remove.",
                    statusCode: StatusCodes.Status404NotFound);
        });

        // POST /plex/{entityType}/items/{id}/refresh: re-reads one linked movie under the sync lock, 409 while a sync runs
        entity.MapPost("/items/{id}/refresh", async (
            HttpContext context,
            string id,
            IEntityService<Movie> movies,
            IPlexSyncService sync,
            CancellationToken cancellationToken) =>
        {
            if (await MovieMissingAsync(id, movies, cancellationToken))
            {
                return MovieNotFound();
            }

            var entityType = PlexEntityTypeFilter.GetEntityType(context);
            try
            {
                var result = await sync.RefreshItemAsync(entityType, id, cancellationToken);
                return result.Status switch
                {
                    PlexRefreshStatus.Refreshed => Results.Ok(PlexItemDataDto.From(result.Data!)),
                    PlexRefreshStatus.NotLinked => Results.Problem(
                        title: "Not linked to Plex",
                        detail: "This item has no Plex link to refresh. Link it first.",
                        statusCode: StatusCodes.Status400BadRequest),
                    PlexRefreshStatus.PlexItemMissing => PlexItemNotFound(result.Data!.PlexItemId),
                    _ => Results.Problem(
                        title: "Plex sync already in progress",
                        detail: result.Run is { } run
                            ? $"A Plex sync for '{run.EntityType}' is already {run.State}."
                            : "A Plex refresh is already in progress.",
                        statusCode: StatusCodes.Status409Conflict,
                        extensions: new Dictionary<string, object?> { ["run"] = result.Run == null ? null : PlexSyncRunDto.From(result.Run) })
                };
            }
            catch (PlexClientException ex)
            {
                return PlexFailed(ex);
            }
        });

        return group;
    }

    // Movie ids are ObjectIds; any other id cannot belong to a movie
    private static async Task<bool> MovieMissingAsync(string id, IEntityService<Movie> movies, CancellationToken cancellationToken) =>
        !MongoDB.Bson.ObjectId.TryParse(id, out _) || await movies.GetAsync(id, cancellationToken) == null;

    private static IResult MovieNotFound() => Results.Problem(
        title: "Movie not found",
        detail: "No movie exists with this id.",
        statusCode: StatusCodes.Status404NotFound);

    private static IResult PlexItemNotFound(string plexItemId) => Results.Problem(
        title: "Plex item not found",
        detail: $"Plex has no movie with item id '{plexItemId}'.",
        statusCode: StatusCodes.Status404NotFound);

    // The exception message holds no URL or token
    private static IResult PlexFailed(PlexClientException ex) => Results.Problem(
        title: "Plex request failed",
        detail: ex.Message,
        statusCode: StatusCodes.Status502BadGateway);
}

public record PlexLinkRequest(string? PlexItemId);
