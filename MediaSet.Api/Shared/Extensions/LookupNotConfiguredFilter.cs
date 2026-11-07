using MediaSet.Api.Infrastructure.Lookup.Strategies;

namespace MediaSet.Api.Shared.Extensions;

/// <summary>
/// Endpoint filter that turns <see cref="LookupNotConfiguredException"/> into an RFC 9457
/// problem details response with status 503.
/// </summary>
internal static class LookupNotConfiguredFilter
{
    public static IResult ToProblem() => TypedResults.Problem(
        title: "Lookup provider not configured",
        detail: LookupNotConfiguredException.DefaultMessage,
        statusCode: StatusCodes.Status503ServiceUnavailable);

    public static TBuilder AddLookupNotConfiguredFilter<TBuilder>(this TBuilder builder)
        where TBuilder : IEndpointConventionBuilder
    {
        return builder.AddEndpointFilter(async (context, next) =>
        {
            try
            {
                return await next(context);
            }
            catch (LookupNotConfiguredException)
            {
                return ToProblem();
            }
        });
    }
}
