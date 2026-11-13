using System.Reflection;
using Microsoft.AspNetCore.HttpLogging;
using MediaSet.Api.Shared.Models;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.Enrichers.Span;
using Serilog.Events;
using SerilogTracing;

namespace MediaSet.Api.Shared.Extensions;

/// <summary>
/// Extension methods for configuring logging and tracing in the application.
/// </summary>
public static class LoggingExtensions
{
    /// <summary>
    /// Configures the bootstrap logger for very early configuration.
    /// This logger is used before the full Serilog configuration is in place.
    /// Suppresses console output when running in the Testing environment.
    /// </summary>
    public static void ConfigureBootstrapLogger()
    {
        var assemblyName = Assembly.GetEntryAssembly()?.GetName().Name ?? "MediaSet.Api";
        var envName = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "NotSet";

        var loggerConfig = new LoggerConfiguration()
            .Enrich.WithProperty("Application", assemblyName)
            .Enrich.WithProperty("Environment", envName);

        // Only write to console if not in Testing environment
        if (envName != "Testing")
        {
            loggerConfig.WriteTo.Console();
        }

        Log.Logger = loggerConfig.CreateBootstrapLogger();
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
                .GetSection(nameof(HttpLoggingFilterOptions))
                .Get<HttpLoggingFilterOptions>() ?? new();

            cfg.ReadFrom.Configuration(context.Configuration)
               .Enrich.FromLogContext()
               .Enrich.WithSpan()
               .Enrich.WithProperty("Application", assemblyNameFromContext)
               .Enrich.WithProperty("Environment", envNameFromContext)
               .ApplyNoiseOverrides();

            // Drop per-command Mongo spans, HTTP client request logs and connection spans (a Plex sync makes about a thousand
            // requests) for every sink, so the console and Seq show the same thing. Client call spans stay for tracing.
            cfg.Filter.ByExcluding(IsLogNoise);

            // Conditionally add Seq sink with filtering for excluded paths
            if (externalLoggingEnabled)
            {
                var seqUrl = context.Configuration.GetValue<string>("ExternalLogging:SeqUrl")
                    ?? "http://localhost:5341";

                // Use nested logger to apply filter only to Seq sink
                cfg.WriteTo.Logger(lc =>
                    lc.Filter.ByExcluding(logEvent =>
                    {
                        // Filter out ALL logs (HTTP logging and infrastructure logs) from excluded paths
                        // The RequestPath is enriched by HttpLoggingFilterMiddleware for all logs in a request
                        if (logEvent.Properties.TryGetValue("RequestPath", out var pathValue) &&
                            pathValue is ScalarValue scalarValue &&
                            scalarValue.Value is string path)
                        {
                            return httpLoggingOptions.IsPathExcluded(path);
                        }
                        return false;
                    })
                    .WriteTo.Seq(seqUrl));
            }
        });

        return builder;
    }

    /// <summary>
    /// Raises framework and HTTP client categories to Warning. The request and client spans (SerilogTracing)
    /// and application events stay at Information.
    /// </summary>
    internal static LoggerConfiguration ApplyNoiseOverrides(this LoggerConfiguration cfg)
    {
        foreach (var category in _noisyCategories)
        {
            cfg.MinimumLevel.Override(category, LogEventLevel.Warning);
        }

        return cfg;
    }

    private static readonly string[] _noisyCategories =
    [
        // Duplicate ASP.NET Core request logs: the request span already covers them
        "Microsoft.AspNetCore.Hosting.Diagnostics",
        "Microsoft.AspNetCore.Routing",
        // "Sending file. Request path ..." for every static image
        "Microsoft.AspNetCore.StaticFiles",
        // Multi-line {HttpLog} request text that duplicates the request span
        "Microsoft.AspNetCore.HttpLogging",
        // "Setting HTTP status code" / "Writing value of type" from result executors
        "Microsoft.AspNetCore.Http.Result",
        // Socket and connection events, including the Seq and MongoDB sinks' own connections
        "Experimental.System.Net",
        // Per-call handler events (Start/Sending/Received/End); the client span has the same data
        "System.Net.Http.HttpClient",
        "Microsoft.Extensions.Http"
    ];

    private static readonly string[] _noisySourcePrefixes = ["MongoDB.Driver", "System.Net.Http.HttpClient", "Microsoft.Extensions.Http"];

    // Connection-level spans of the HTTP client: they carry no source context, so they are matched by their message
    private static readonly string[] _noisySpanPrefixes = ["DNS lookup", "socket connect", "TLS client handshake", "HTTP connection_setup", "HTTP wait_for_connection"];

    /// <summary>Events below Warning from the Mongo driver and the HTTP client stack.</summary>
    internal static bool IsLogNoise(LogEvent logEvent) =>
        logEvent.Level < LogEventLevel.Warning
        && ((logEvent.Properties.TryGetValue("SourceContext", out var source)
                && source is ScalarValue { Value: string context }
                && _noisySourcePrefixes.Any(p => context.StartsWith(p, StringComparison.Ordinal)))
            || _noisySpanPrefixes.Any(p => logEvent.MessageTemplate.Text.StartsWith(p, StringComparison.Ordinal)));

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
        builder.Services.Configure<HttpLoggingFilterOptions>(
            builder.Configuration.GetSection(nameof(HttpLoggingFilterOptions)));

        // Register the HTTP logging interceptor to respect path exclusions
        builder.Services.AddSingleton<IHttpLoggingInterceptor, ExcludePathHttpLoggingInterceptor>();

        builder.Services.AddHttpLogging(logging =>
        {
            logging.LoggingFields =
                HttpLoggingFields.RequestMethod |
                HttpLoggingFields.RequestPath;
            logging.RequestHeaders.Add("User-Agent");
            logging.RequestHeaders.Add("traceparent");
            logging.ResponseHeaders.Add("traceparent");
        });

        return builder;
    }

    /// <summary>
    /// Configures OpenTelemetry for distributed tracing across the application.
    /// Instruments ASP.NET Core and HTTP client requests. Spans are written through SerilogTracing, so no console exporter is added (it printed a multi-line dump per outgoing HTTP request).
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
                    .AddHttpClientInstrumentation();
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
    /// based on the HttpLoggingFilterOptions configuration.
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
}
