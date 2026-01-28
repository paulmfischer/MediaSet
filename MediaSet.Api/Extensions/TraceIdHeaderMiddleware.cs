namespace MediaSet.Api.Extensions;

/// <summary>
/// Middleware that reads TraceId from request headers and uses it for the current request.
/// This allows clients to correlate UI logs with API logs by sending a trace ID header.
/// If no trace ID is provided, ASP.NET will use its default request ID.
/// </summary>
public class TraceIdHeaderMiddleware : IMiddleware
{
    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        // Check if client provided a TraceId header
        if (context.Request.Headers.TryGetValue("X-Trace-Id", out var traceIdValue) && 
            !string.IsNullOrEmpty(traceIdValue))
        {
            // Use the client-provided trace ID
            context.TraceIdentifier = traceIdValue.ToString();
        }

        await next(context);
    }
}
