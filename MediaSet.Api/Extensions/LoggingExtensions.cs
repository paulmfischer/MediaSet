using System.Reflection;
using Microsoft.AspNetCore.HttpLogging;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.Enrichers.Span;
using Serilog.Events;
using SerilogTracing;

namespace MediaSet.Api.Extensions;

/// <summary>
/// Extension methods for configuring logging and tracing in the application.
/// </summary>
public static class LoggingExtensions
{
    /// <summary>
    /// Configures the bootstrap logger for very early configuration.
    /// This logger is used before the full Serilog configuration is in place.
    /// </summary>
    public static void ConfigureBootstrapLogger()
    {
        var assemblyName = Assembly.GetEntryAssembly()?.GetName().Name ?? "MediaSet.Api";
        var envName = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "NotSet";

        Log.Logger = new LoggerConfiguration()
            .Enrich.WithProperty("Application", assemblyName)
            .Enrich.WithProperty("Environment", envName)
            .WriteTo.Console()
            .CreateBootstrapLogger();
    }

    /// <summary>
    /// Gets the bootstrap logger instance for use in configuration.
    /// </summary>
    public static Serilog.ILogger GetBootstrapLogger() =>
        Log.Logger.ForContext("BootstrapPhase", true);

    /// <summary>
    /// Configures Serilog as the logging provider with conditional Seq sink support.
    /// </summary>
    public static WebApplicationBuilder UseSerilogConfiguration(this WebApplicationBuilder builder)
    {
        builder.Host.UseSerilog((context, services, cfg) =>
        {
            var assemblyNameFromContext = Assembly.GetEntryAssembly()?.GetName().Name ?? "MediaSet.Api";
            var envNameFromContext = context.HostingEnvironment.EnvironmentName;
            var externalLoggingEnabled = context.Configuration.GetValue<bool>("ExternalLogging:Enabled");
            var httpLoggingOptions = context.Configuration
                .GetSection(nameof(Models.HttpLoggingOptions))
                .Get<Models.HttpLoggingOptions>() ?? new();

            cfg.ReadFrom.Configuration(context.Configuration)
               .Enrich.FromLogContext()
               .Enrich.WithSpan()
               .Enrich.WithProperty("Application", assemblyNameFromContext)
               .Enrich.WithProperty("Environment", envNameFromContext)
               // Suppress duplicate ASP.NET Core request logs
               .MinimumLevel.Override("Microsoft.AspNetCore.Hosting.Diagnostics", LogEventLevel.Warning)
               .MinimumLevel.Override("Microsoft.AspNetCore.Routing", LogEventLevel.Warning);

            // Conditionally add Seq sink with filtering for excluded paths (console still logs everything)
            if (externalLoggingEnabled)
            {
                var seqUrl = context.Configuration.GetValue<string>("ExternalLogging:SeqUrl") 
                    ?? "http://localhost:5341";
                
                // Use nested logger to apply filter only to Seq sink
                cfg.WriteTo.Logger(lc => 
                    lc.Filter.ByExcluding(logEvent =>
                    {
                        // Only filter out HTTP logging from excluded paths, not application logs
                        // HTTP logs come from Microsoft.AspNetCore.HttpLogging
                        var isHttpLog = logEvent.Properties.ContainsKey("RequestPath") &&
                                       logEvent.Properties.ContainsKey("RequestMethod");
                        
                        if (isHttpLog)
                        {
                            if (logEvent.Properties.TryGetValue("RequestPath", out var pathValue) && 
                                pathValue is ScalarValue scalarValue &&
                                scalarValue.Value is string path)
                            {
                                return httpLoggingOptions.IsPathExcluded(path);
                            }
                        }
                        return false;
                    })
                    .WriteTo.Seq(seqUrl));
            }
        });

        return builder;
    }

    /// <summary>
    /// Configures activity tracking for trace propagation across distributed systems.
    /// </summary>
    public static WebApplicationBuilder ConfigureActivityTracking(this WebApplicationBuilder builder)
    {
        builder.Logging.Configure(options =>
            options.ActivityTrackingOptions = ActivityTrackingOptions.SpanId |
                                              ActivityTrackingOptions.TraceId |
                                              ActivityTrackingOptions.ParentId);

        return builder;
    }

    /// <summary>
    /// Configures built-in HTTP request/response logging with structured logging fields.
    /// Also configures path exclusion options to prevent logging of specific endpoints.
    /// </summary>
    public static WebApplicationBuilder ConfigureHttpLogging(this WebApplicationBuilder builder)
    {
        // Configure HTTP logging options with path exclusions
        builder.Services.Configure<MediaSet.Api.Models.HttpLoggingOptions>(
            builder.Configuration.GetSection(nameof(MediaSet.Api.Models.HttpLoggingOptions)));

        // Register the HTTP logging interceptor to respect path exclusions
        builder.Services.AddSingleton<IHttpLoggingInterceptor, ExcludePathHttpLoggingInterceptor>();

        builder.Services.AddHttpLogging(logging =>
        {
            logging.LoggingFields =
                HttpLoggingFields.RequestMethod |
                HttpLoggingFields.RequestPath |
                HttpLoggingFields.RequestBody |
                HttpLoggingFields.ResponseStatusCode |
                HttpLoggingFields.ResponseBody |
                HttpLoggingFields.Duration;
            logging.RequestHeaders.Add("User-Agent");
            logging.RequestHeaders.Add("X-Request-ID");
            logging.RequestHeaders.Add("X-Correlation-ID");
            logging.ResponseHeaders.Add("X-Request-ID");
            logging.ResponseHeaders.Add("X-Correlation-ID");
        });

        return builder;
    }

    /// <summary>
    /// Configures OpenTelemetry for distributed tracing across the application.
    /// Instruments ASP.NET Core and HTTP client requests, exporting to console.
    /// </summary>
    public static WebApplicationBuilder ConfigureOpenTelemetry(this WebApplicationBuilder builder)
    {
        var serviceName = Assembly.GetEntryAssembly()?.GetName().Name ?? "MediaSet.Api";

        builder.Services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(serviceName))
            .WithTracing(tracing =>
            {
                tracing
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddConsoleExporter();
            });

        return builder;
    }

    /// <summary>
    /// Configures SerilogTracing to capture spans and write them to Serilog/Seq.
    /// This enables distributed tracing across the application.
    /// </summary>
    public static IDisposable ConfigureSerilogTracing()
    {
        return new ActivityListenerConfiguration()
            .Instrument.AspNetCoreRequests()  // Captures HTTP requests as spans
            .Instrument.HttpClientRequests()  // Captures outgoing HTTP calls as spans
            .TraceToSharedLogger();            // Writes to Serilog (which goes to Seq)
    }

    /// <summary>
    /// Adds HTTP logging filter middleware to the request pipeline.
    /// This middleware checks if a path should be excluded from HTTP logging
    /// based on the HttpLoggingOptions configuration.
    /// Must be called before UseHttpLogging() and after UseRouting().
    /// </summary>
    public static WebApplication UseHttpLoggingFilterMiddleware(this WebApplication app)
    {
        app.UseMiddleware<HttpLoggingFilterMiddleware>();
        return app;
    }

    /// <summary>
    /// Adds HTTP logging middleware to the request pipeline.
    /// Must be called after UseRouting() and before endpoint mapping.
    /// </summary>
    public static WebApplication UseHttpLoggingMiddleware(this WebApplication app)
    {
        app.UseHttpLogging();
        return app;
    }

    /// <summary>
    /// Adds correlation ID middleware for request tracking.
    /// Generates or extracts correlation ID from request headers.
    /// </summary>
    public static WebApplication UseCorrelationIdMiddleware(this WebApplication app)
    {
        app.Use(async (context, next) =>
        {
            // Get or generate correlation ID
            var correlationId = context.Request.Headers["X-Request-ID"].FirstOrDefault()
                ?? context.Request.Headers["X-Correlation-ID"].FirstOrDefault()
                ?? Guid.NewGuid().ToString();

            // Add correlation ID to response headers
            context.Response.Headers["X-Request-ID"] = correlationId;
            context.Response.Headers["X-Correlation-ID"] = correlationId;

            await next();
        });

        return app;
    }
}
