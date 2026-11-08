using MediaSet.Api.Infrastructure.Plex;

namespace MediaSet.Api.Features.Plex.Endpoints;

/// <summary>
/// Endpoint filter for routes with an {entityType} segment. Rejects unknown (404) and disabled (400)
/// entity types with ProblemDetails, otherwise stores the canonical type for the handler. Later
/// per-entity endpoints reuse it by mapping under the same route group.
/// </summary>
internal sealed class PlexEntityTypeFilter : IEndpointFilter
{
    private const string ItemKey = "plex.entityType";

    public static string GetEntityType(HttpContext context) => (string)context.Items[ItemKey]!;

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var http = context.HttpContext;
        var raw = http.Request.RouteValues["entityType"]?.ToString();
        var entityType = PlexEntityTypes.Normalize(raw);
        if (entityType == null)
        {
            return Results.Problem(
                title: "Unknown Plex entity type",
                detail: $"Plex entity type '{raw}' is not supported. Supported: {string.Join(", ", PlexEntityTypes.Supported)}.",
                statusCode: StatusCodes.Status404NotFound);
        }

        var enablement = http.RequestServices.GetRequiredService<PlexEnablement>();
        if (!enablement.IsEnabled(entityType))
        {
            return Results.Problem(
                title: "Plex entity type disabled",
                detail: $"Plex is not enabled for entity type '{entityType}'.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        http.Items[ItemKey] = entityType;
        return await next(context);
    }
}
