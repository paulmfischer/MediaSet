using System.Diagnostics;
using System.Globalization;
using System.Net;
using MediaSet.Api.Infrastructure.Lookup;

namespace MediaSet.Api.Features.Lookup.Endpoints;

/// <summary>
/// Endpoint filter that maps lookup provider HTTP failures to RFC 9457 problem details:
/// 429 from a provider becomes 429 (with Retry-After when known); any other upstream
/// failure (5xx, other statuses, network errors) becomes 503. Logs a Warning with the provider.
/// </summary>
internal static class LookupProviderFailureFilter
{
    private const string UnknownProvider = "Unknown";

    public static TBuilder AddLookupProviderFailureFilter<TBuilder>(this TBuilder builder)
        where TBuilder : IEndpointConventionBuilder
    {
        return builder.AddEndpointFilter(async (context, next) =>
        {
            try
            {
                return await next(context);
            }
            catch (Exception ex) when (IsProviderFailure(ex) && !context.HttpContext.RequestAborted.IsCancellationRequested)
            {
                var logger = context.HttpContext.RequestServices
                    .GetRequiredService<ILoggerFactory>()
                    .CreateLogger(typeof(LookupProviderFailureFilter).FullName!);
                return ToProblem(ex, context.HttpContext, logger);
            }
        });
    }

    public static bool IsProviderFailure(Exception ex) => ex is HttpRequestException or ProviderLookupException;

    public static IResult ToProblem(Exception ex, HttpContext httpContext, ILogger logger)
    {
        var provider = ResolveProvider(ex);
        var rateLimited = ex is ProviderLookupException { Outcome: Shared.Models.ImageLookupOutcome.QuotaLimited }
            || ex is HttpRequestException { StatusCode: HttpStatusCode.TooManyRequests };
        var retryAfterSeconds = GetRetryAfterSeconds(ex);

        if (rateLimited)
        {
            logger.LogWarning(ex, "Lookup provider {Provider} rate limited the request; returning 429 (retry after {RetryAfterSeconds}s)",
                provider, retryAfterSeconds);
            if (retryAfterSeconds is { } seconds)
            {
                httpContext.Response.Headers.RetryAfter = seconds.ToString(CultureInfo.InvariantCulture);
            }

            return TypedResults.Problem(
                title: "Lookup provider rate limit exceeded",
                detail: $"The {provider} lookup provider is rate limiting requests. Please wait a moment and try again.",
                statusCode: StatusCodes.Status429TooManyRequests);
        }

        logger.LogWarning(ex, "Lookup provider {Provider} request failed; returning 503", provider);
        return TypedResults.Problem(
            title: "Lookup provider unavailable",
            detail: $"The {provider} lookup provider is currently unavailable or busy. Please try again later.",
            statusCode: StatusCodes.Status503ServiceUnavailable);
    }

    private static int? GetRetryAfterSeconds(Exception ex)
    {
        if (ex is ProviderLookupException { RetryAt: { } retryAt })
        {
            var seconds = (int)Math.Ceiling((retryAt - DateTime.UtcNow).TotalSeconds);
            return seconds > 0 ? seconds : null;
        }

        return null;
    }

    private static string ResolveProvider(Exception ex)
    {
        if (ex is ProviderLookupException provider)
        {
            return provider.Provider;
        }

        // Provider clients throw plain HttpRequestException; the first lookup client frame names the provider.
        foreach (var frame in new StackTrace(ex).GetFrames())
        {
            var type = frame.GetMethod()?.DeclaringType;
            while (type is { DeclaringType: not null })
            {
                type = type.DeclaringType;
            }

            if (type is { Name: var name } && type.Namespace?.StartsWith("MediaSet.Api.Infrastructure.Lookup.Clients", StringComparison.Ordinal) == true
                && name.EndsWith("Client", StringComparison.Ordinal))
            {
                return name[..^"Client".Length];
            }
        }

        return UnknownProvider;
    }
}
