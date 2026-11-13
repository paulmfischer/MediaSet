using MediaSet.Api.Features.Plex.Models;
using MediaSet.Api.Features.Plex.Services;
using MediaSet.Api.Infrastructure.DataAccess;
using MediaSet.Api.Infrastructure.Plex;
using MediaSet.Api.Shared.Extensions;
using MediaSet.Api.Shared.Models;

namespace MediaSet.Api.Features.Plex.Endpoints;

internal static class PlexApi
{
    public static RouteGroupBuilder MapPlex(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/plex");
        group.WithTags("Plex");
        group.WithLogComponent(LogComponents.Plex);

        // GET /plex/status: one entry per supported entity type
        group.MapGet("/status", async (PlexEnablement enablement, IPlexSyncRunStore runs, IPlexScheduleInfo schedule, CancellationToken cancellationToken) =>
        {
            var statuses = new List<PlexEntityStatusDto>();
            foreach (var type in PlexEntityTypes.Supported)
            {
                statuses.Add(await BuildStatusAsync(type, enablement, runs, schedule, cancellationToken));
            }

            return Results.Ok(statuses);
        });

        // GET /plex/statistics: breakdowns of the stored Plex data for movies and TV entries (no file paths).
        // Registered with the rest of the Plex routes, so it exists only when Plex is enabled; 400 when movies are not.
        group.MapGet("/statistics", async (PlexEnablement enablement, IPlexStatisticsService statistics, CancellationToken cancellationToken) =>
            enablement.IsEnabled(PlexEntityTypes.Movies)
                ? Results.Ok(await statistics.GetAsync(cancellationToken))
                : Results.Problem(
                    title: "Plex entity type disabled",
                    detail: "Plex is not enabled for entity type 'movies'.",
                    statusCode: StatusCodes.Status400BadRequest));

        // GET /plex/{entityType}/status: rejects unknown entity types only, so a disabled one reports enabled: false.
        // entityType is a bound handler parameter on every route below so OpenAPI declares it as a path parameter.
        group.MapGroup("/{entityType}").AddEndpointFilter(new PlexEntityTypeFilter(requireEnabled: false))
            .MapGet("/status", async (string entityType, HttpContext context, PlexEnablement enablement, IPlexSyncRunStore runs, IPlexScheduleInfo schedule, CancellationToken cancellationToken) =>
            {
                var status = await BuildStatusAsync(PlexEntityTypeFilter.GetEntityType(context), enablement, runs, schedule, cancellationToken);
                return Results.Ok(status);
            });

        // Other per-entity routes: the filter rejects unknown and disabled entity types
        var entity = group.MapGroup("/{entityType}").AddEndpointFilter(new PlexEntityTypeFilter(requireEnabled: true));

        // POST /plex/{entityType}/sync: starts a run in the background (202), or 409 with the current run
        entity.MapPost("/sync", async (string entityType, HttpContext context, IPlexSyncService sync, CancellationToken cancellationToken) =>
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
            string entityType,
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
        entity.MapGet("/items/{id}", async (string entityType, string id, IPlexItemStore items, CancellationToken cancellationToken) =>
        {
            // Plex documents are keyed by ObjectId; any other id cannot be linked
            var data = IsObjectId(id) ? await items.GetAsync(id, cancellationToken) : null;
            return data == null
                ? Results.Problem(
                    title: "Not linked to Plex",
                    detail: "This item has no Plex data.",
                    statusCode: StatusCodes.Status404NotFound)
                : Results.Ok(PlexItemDataDto.From(data));
        });

        // GET /plex/{entityType}/items/{id}/scope: the seasons the TV entry's title says (kind "unreadable" when it does not say),
        // for prefilling the link form. Reads only the entry, so it works whether or not it is linked; 400 for a non-TV entry.
        entity.MapGet("/items/{id}/scope", async (
            string entityType,
            string id,
            IEntityService<Movie> movies,
            CancellationToken cancellationToken) =>
        {
            var movie = IsObjectId(id) ? await movies.GetAsync(id, cancellationToken) : null;
            if (movie == null)
            {
                return MovieNotFound();
            }

            return movie.IsTvSeries
                ? Results.Ok(PlexTvTitleParser.ParseScope(movie.Title))
                : Results.Problem(
                    title: "Not a TV series",
                    detail: "A season scope applies only to TV series entries.",
                    statusCode: StatusCodes.Status400BadRequest);
        });

        // PUT /plex/{entityType}/items/{id}/link: links a movie to a Plex item by hand and loads its data now
        entity.MapPut("/items/{id}/link", async (
            string entityType,
            HttpContext context,
            string id,
            PlexLinkRequest? body,
            IEntityService<Movie> movies,
            IPlexLinkService links,
            IPlexCoverService covers,
            CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(body?.PlexItemId))
            {
                return Results.Problem(
                    title: "Plex item id required",
                    detail: "The request body must contain a 'plexItemId'.",
                    statusCode: StatusCodes.Status400BadRequest);
            }

            var movie = IsObjectId(id) ? await movies.GetAsync(id, cancellationToken) : null;
            if (movie == null)
            {
                return MovieNotFound();
            }

            // A TV series entry links to a show with a scope; any other entry links to a movie and takes none
            PlexScope? scope = null;
            if (movie.IsTvSeries)
            {
                var (parsed, problemTitle, error) = ResolveScope(body.Scope, movie.Title);
                if (error != null)
                {
                    return Results.Problem(title: problemTitle, detail: error, statusCode: StatusCodes.Status400BadRequest);
                }
                scope = parsed;
            }
            else if (body.Scope != null)
            {
                return Results.Problem(
                    title: "Scope not allowed",
                    detail: "A scope applies only to TV series entries.",
                    statusCode: StatusCodes.Status400BadRequest);
            }

            try
            {
                var data = await links.LinkAsync(PlexEntityTypeFilter.GetEntityType(context), id, body.PlexItemId.Trim(), cancellationToken, scope);
                if (data == null)
                {
                    return PlexItemNotFound(body.PlexItemId.Trim(), scope != null);
                }

                // A paired entry without a cover takes the Plex poster (the service checks). Best effort: it never fails the link.
                await covers.ApplyIfMissingAsync(id, data.PlexItemId, cancellationToken);
                return Results.Ok(PlexItemDataDto.From(data));
            }
            catch (PlexClientException ex)
            {
                return PlexFailed(ex);
            }
        });

        // DELETE /plex/{entityType}/items/{id}/link: removes the link and its stored Plex data
        entity.MapDelete("/items/{id}/link", async (
            string entityType,
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
            string entityType,
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

            var type = PlexEntityTypeFilter.GetEntityType(context);
            try
            {
                var result = await sync.RefreshItemAsync(type, id, cancellationToken);
                return result.Status switch
                {
                    PlexRefreshStatus.Refreshed => Results.Ok(PlexItemDataDto.From(result.Data!)),
                    PlexRefreshStatus.NotLinked => Results.Problem(
                        title: "Not linked to Plex",
                        detail: "This item has no Plex link to refresh. Link it first.",
                        statusCode: StatusCodes.Status400BadRequest),
                    PlexRefreshStatus.PlexItemMissing => PlexItemNotFound(result.Data!.PlexItemId, result.Data.Scope != null),
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

    private const int MaxSeason = 99;

    // A disabled type has no last run and no next run
    private static async Task<PlexEntityStatusDto> BuildStatusAsync(
        string type, PlexEnablement enablement, IPlexSyncRunStore runs, IPlexScheduleInfo schedule, CancellationToken cancellationToken)
    {
        var enabled = enablement.IsEnabled(type);
        var last = enabled ? await runs.GetLastAsync(type, cancellationToken) : null;
        return new PlexEntityStatusDto(type, enabled, last == null ? null : PlexSyncRunDto.From(last), enabled ? schedule.GetNextRun(type) : null);
    }

    /// <summary>The scope for a manual TV link: the explicit one, validated, or else the one the title says. An error (with its problem title) when neither is usable.</summary>
    private static (PlexScope? Scope, string? Title, string? Error) ResolveScope(PlexLinkScopeRequest? request, string title)
    {
        if (request == null)
        {
            var parsed = PlexTvTitleParser.ParseScope(title);
            return parsed.IsReadable
                ? (parsed, null, null)
                : (null, "Scope required", "The season scope cannot be read from the title. Send a 'scope' of kind 'season', 'range' or 'all'.");
        }

        static bool Valid(int? n) => n is >= 0 and <= MaxSeason;
        switch (request.Kind?.Trim().ToLowerInvariant())
        {
            case PlexScopeKinds.All:
                return (PlexScope.AllSeasons, null, null);
            case PlexScopeKinds.Season when Valid(request.Season):
                return (PlexScope.Single(request.Season!.Value), null, null);
            case PlexScopeKinds.Season:
                return (null, "Invalid scope", $"Scope kind 'season' needs a 'season' from 0 to {MaxSeason}.");
            case PlexScopeKinds.Range when Valid(request.From) && Valid(request.To) && request.From <= request.To:
                return (PlexScope.Between(request.From!.Value, request.To!.Value), null, null);
            case PlexScopeKinds.Range:
                return (null, "Invalid scope", $"Scope kind 'range' needs 'from' up to 'to' (use kind 'season' for one season), both from 0 to {MaxSeason}.");
            default:
                return (null, "Invalid scope", "Scope 'kind' must be 'season', 'range' or 'all'.");
        }
    }

    // Movie and Plex document ids are ObjectIds; any other id cannot belong to either
    private static bool IsObjectId(string id) => MongoDB.Bson.ObjectId.TryParse(id, out _);

    private static async Task<bool> MovieMissingAsync(string id, IEntityService<Movie> movies, CancellationToken cancellationToken) =>
        !IsObjectId(id) || await movies.GetAsync(id, cancellationToken) == null;

    private static IResult MovieNotFound() => Results.Problem(
        title: "Movie not found",
        detail: "No movie exists with this id.",
        statusCode: StatusCodes.Status404NotFound);

    private static IResult PlexItemNotFound(string plexItemId, bool show = false) => Results.Problem(
        title: "Plex item not found",
        detail: $"Plex has no {(show ? "show" : "movie")} with item id '{plexItemId}'.",
        statusCode: StatusCodes.Status404NotFound);

    // The exception message holds no URL or token
    private static IResult PlexFailed(PlexClientException ex) => Results.Problem(
        title: "Plex request failed",
        detail: ex.Message,
        statusCode: StatusCodes.Status502BadGateway);
}

/// <summary>
/// Body of the manual link call. <c>Scope</c> is for TV series entries only: when omitted it is read from the
/// entry title, and the call fails when the title does not say.
/// </summary>
public record PlexLinkRequest(string? PlexItemId, PlexLinkScopeRequest? Scope = null);

/// <summary>
/// The seasons a TV entry covers. <c>Kind</c> is "season" (with <c>Season</c>), "range" (with <c>From</c> and
/// <c>To</c>, From not above To; equal means one season) or "all". Season numbers are 0 (specials) to 99.
/// </summary>
public record PlexLinkScopeRequest(string? Kind, int? Season = null, int? From = null, int? To = null);
