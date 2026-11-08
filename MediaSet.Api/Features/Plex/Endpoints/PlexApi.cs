using MediaSet.Api.Features.Plex.Models;
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

        return group;
    }
}
