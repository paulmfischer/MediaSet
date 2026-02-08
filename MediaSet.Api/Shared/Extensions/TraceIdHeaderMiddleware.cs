namespace MediaSet.Api.Shared.Extensions;

/// <summary>
/// Middleware that extracts the W3C traceparent header from requests for distributed tracing.
/// This integrates with OpenTelemetry to maintain trace context across service boundaries.
/// The traceparent header format: 00-{traceId}-{spanId}-{traceFlags}
/// If no traceparent header is provided, OpenTelemetry will generate a new trace.
/// </summary>
public class TraceIdHeaderMiddleware : IMiddleware
{
    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        // OpenTelemetry's AspNetCoreInstrumentation automatically extracts the W3C traceparent header
        // and propagates it through Activity.Current. This middleware ensures the trace ID is also
        // available via context.TraceIdentifier for compatibility with Serilog enrichment.
        if (context.Request.Headers.TryGetValue("traceparent", out var traceparentValue) &&
            !string.IsNullOrEmpty(traceparentValue))
        {
            // traceparent format: 00-{traceId}-{spanId}-{traceFlags}
            // Extract just the traceId portion (32 hex characters after the first dash)
            var parts = traceparentValue.ToString().Split('-');
            if (parts.Length >= 2 && parts[1].Length >= 32)
            {
                context.TraceIdentifier = parts[1][..32]; // First 32 chars are the trace ID
            }
        }

        await next(context);
    }
}
