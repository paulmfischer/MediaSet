using Microsoft.AspNetCore.HttpLogging;
using Microsoft.Extensions.Options;

namespace MediaSet.Api.Shared.Extensions;

/// <summary>
/// Middleware that conditionally enables HTTP logging based on path configuration.
/// This prevents logging of specific endpoints (like /api/logs) that would
/// create circular/redundant logging entries.
/// </summary>
public class HttpLoggingFilterMiddleware : IMiddleware
{
    private readonly ILogger<HttpLoggingFilterMiddleware> _logger;
    private readonly Features.Logs.Models.HttpLoggingOptions _options;

    public HttpLoggingFilterMiddleware(
        ILogger<HttpLoggingFilterMiddleware> logger,
        IOptions<Features.Logs.Models.HttpLoggingOptions> options)
    {
        _logger = logger;
        _options = options.Value;
    }

    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        var path = context.Request.Path.Value ?? string.Empty;

        // If path should be excluded from logging, disable HTTP logging for this request
        if (_options.IsPathExcluded(path))
        {
            // Set a flag that the HTTP logging interceptor can check
            context.Items["DisableHttpLogging"] = true;
        }

        await next(context);
    }
}

/// <summary>
/// HTTP logging interceptor that respects the DisableHttpLogging flag
/// to exclude specific paths from being logged.
/// </summary>
public class ExcludePathHttpLoggingInterceptor : IHttpLoggingInterceptor
{
    public ValueTask OnRequestAsync(HttpLoggingInterceptorContext context)
    {
        // Check if this request should be excluded from logging
        var disableLogging = context.HttpContext.Items.TryGetValue("DisableHttpLogging", out var value) 
            && value is true;

        if (disableLogging)
        {
            // Clear all logging fields to prevent logging
            context.LoggingFields = Microsoft.AspNetCore.HttpLogging.HttpLoggingFields.None;
        }

        return ValueTask.CompletedTask;
    }

    public ValueTask OnResponseAsync(HttpLoggingInterceptorContext context)
    {
        // Check if this request should be excluded from logging
        var disableLogging = context.HttpContext.Items.TryGetValue("DisableHttpLogging", out var value)
            && value is true;

        if (disableLogging)
        {
            // Clear all logging fields to prevent logging
            context.LoggingFields = Microsoft.AspNetCore.HttpLogging.HttpLoggingFields.None;
        }

        return ValueTask.CompletedTask;
    }
}
