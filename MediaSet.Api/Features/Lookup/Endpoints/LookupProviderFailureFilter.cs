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
    public static TBuilder AddLookupProviderFailureFilter<TBuilder>(this TBuilder builder)
        where TBuilder : IEndpointConventionBuilder
    {
        return builder.AddEndpointFilter(InvokeAsync);
    }

    public static async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
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
    }

    // Only failures that clearly come from a lookup provider are mapped. An HttpClient timeout surfaces as a
    // TaskCanceledException wrapping a TimeoutException; a bare TaskCanceledException or a TimeoutException from
    // elsewhere (e.g. MongoDB) is only a provider failure when a client stamped it. The `when` clause above also
    // leaves real client aborts (RequestAborted cancelled) alone.
    public static bool IsProviderFailure(Exception ex) => ex switch
    {
        HttpRequestException or ProviderLookupException => true,
        TaskCanceledException { InnerException: TimeoutException } => true,
        TaskCanceledException or TimeoutException => ex.Data[LookupFailureScope.ProviderDataKey] is string,
        _ => false
    };

    public static IResult ToProblem(Exception ex, HttpContext httpContext, ILogger logger)
    {
        var provider = ResolveProvider(ex);
        var rateLimited = ex is ProviderLookupException { Outcome: Shared.Models.ImageLookupOutcome.QuotaLimited }
            || ex is HttpRequestException { StatusCode: HttpStatusCode.TooManyRequests };
        var rejectedCredentials = ex is ProviderLookupException { Outcome: Shared.Models.ImageLookupOutcome.Unauthorized }
            || ex is HttpRequestException { StatusCode: HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden };
        var retryAfterSeconds = GetRetryAfterSeconds(ex);

        if (rateLimited)
        {
            logger.LogWarning(ex, "Lookup provider {Provider} rate limited the request; returning 429 (retry after {RetryAfterSeconds}s)",
                provider, retryAfterSeconds);
            SetRetryAfter(httpContext, retryAfterSeconds);

            return TypedResults.Problem(
                title: "Lookup provider rate limit exceeded",
                detail: $"The {provider} lookup provider is rate limiting requests. Please wait a moment and try again.",
                statusCode: StatusCodes.Status429TooManyRequests);
        }

        if (rejectedCredentials)
        {
            // Warning, not Error: the failure is handled and returned to the caller; the message names the
            // provider so a bad API key is easy to find without alerting on every request.
            logger.LogWarning(ex, "Lookup provider {Provider} rejected our credentials; returning 503", provider);
            return TypedResults.Problem(
                title: "Lookup provider rejected credentials",
                detail: $"The {provider} lookup provider rejected our credentials. Its API key may be missing, invalid or expired; check the provider configuration.",
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        logger.LogWarning(ex, "Lookup provider {Provider} request failed or timed out; returning 503", provider);
        SetRetryAfter(httpContext, retryAfterSeconds);
        return TypedResults.Problem(
            title: "Lookup provider unavailable",
            detail: $"The {provider} lookup provider is currently unavailable or busy. Please try again later.",
            statusCode: StatusCodes.Status503ServiceUnavailable);
    }

    private static void SetRetryAfter(HttpContext httpContext, int? seconds)
    {
        if (seconds is { } value)
        {
            httpContext.Response.Headers.RetryAfter = value.ToString(CultureInfo.InvariantCulture);
        }
    }

    private static int? GetRetryAfterSeconds(Exception ex)
    {
        DateTime? retryAt = ex switch
        {
            ProviderLookupException e => e.RetryAt,
            ProviderRateLimitException e => e.RetryAt,
            _ => null
        };
        if (retryAt is null)
        {
            return null;
        }

        var seconds = (int)Math.Ceiling((retryAt.Value - DateTime.UtcNow).TotalSeconds);
        return seconds > 0 ? seconds : null;
    }

    private static string ResolveProvider(Exception ex)
    {
        if (ex is ProviderLookupException provider)
        {
            return provider.Provider;
        }

        // Provider clients stamp their name on exceptions they record before rethrowing.
        return ex.Data[LookupFailureScope.ProviderDataKey] as string ?? LookupFailureScope.UnknownProvider;
    }
}
