using System.Net;
using MediaSet.Api.Shared.Extensions;

namespace MediaSet.Api.Infrastructure.Lookup;

/// <summary>Logging for lookup provider client failures that are rethrown to the endpoint.</summary>
public static class ProviderFailureLogging
{
    /// <summary>
    /// True for a 429 or an upstream 5xx: transient failures the lookup endpoint maps to 429/503 and logs as a
    /// Warning (<c>LookupProviderFailureFilter</c>), so the client logs them at Warning too. The filter returns 503
    /// for every other failure as well, but 401/403 and other statuses deliberately still log Error in the client.
    /// </summary>
    public static bool IsTransientProviderStatus(HttpStatusCode? status) =>
        status is HttpStatusCode.TooManyRequests or >= HttpStatusCode.InternalServerError;

    /// <summary>
    /// Logs a rethrown <see cref="HttpRequestException"/> inside a Provider scope: Warning for rate limits and 5xx
    /// (the endpoint logs and maps them), Error for anything else.
    /// </summary>
    public static void LogProviderHttpFailure(this ILogger logger, HttpRequestException ex, string provider,
        string message, params object?[] args)
    {
        var level = IsTransientProviderStatus(ex.StatusCode) ? LogLevel.Warning : LogLevel.Error;
        using (logger.BeginProviderScope(provider))
        {
            logger.Log(level, ex, message, args);
        }
    }
}
