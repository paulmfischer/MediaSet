using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Trace;

namespace MediaSet.Api.Tests.Observability;

/// <summary>
/// Builds the real API host in memory with isolated observability seams: an in-memory Serilog
/// sink, an in-memory OpenTelemetry exporter, and test-only endpoints that run inside the
/// real request pipeline (ASP.NET Core instrumentation, TraceIdHeaderMiddleware, enrichers).
/// </summary>
public static class ObservabilityTestHost
{
    /// <summary>
    /// Creates the factory. Seq, when enabled, points at an unreachable local address.
    /// </summary>
    public static WebApplicationFactory<Program> Create(
        InMemoryActivityExporter exporter,
        bool externalLoggingEnabled,
        string? outgoingTargetUrl = null)
    {
        var contentRoot = Path.GetDirectoryName(typeof(ObservabilityTestHost).Assembly.Location)
            ?? throw new InvalidOperationException("Cannot determine test assembly directory");

        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseContentRoot(contentRoot);
            builder.UseEnvironment("Testing");

            builder.ConfigureAppConfiguration((_, configuration) =>
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Serilog:Using:0"] = "MediaSet.Api.Tests",
                    ["Serilog:MinimumLevel"] = "Verbose",
                    ["Serilog:WriteTo:0:Name"] = "InMemoryTestSink",
                    ["ExternalLogging:Enabled"] = externalLoggingEnabled ? "true" : "false",
                    // Port 1 on loopback is unreachable: the shared Seq service is never contacted.
                    ["ExternalLogging:SeqUrl"] = "http://127.0.0.1:1",
                    ["MediaSetDatabaseSettings:ConnectionString"] = "mongodb://127.0.0.1:1",
                    ["ImageConfiguration:StoragePath"] = Path.Combine(Path.GetTempPath(), "mediaset-observability-tests"),
                    ["BackgroundImageLookupConfiguration:Enabled"] = "false"
                }));

            builder.ConfigureServices(services =>
            {
                services.ConfigureOpenTelemetryTracerProvider((_, tracing) =>
                    tracing.AddProcessor(new OpenTelemetry.SimpleActivityExportProcessor(exporter)));
                services.AddTransient<IStartupFilter>(_ => new ProbeEndpointsStartupFilter(outgoingTargetUrl));
            });
        });
    }

    private sealed class ProbeEndpointsStartupFilter(string? outgoingTargetUrl) : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.Map("/_probe/outgoing", probe => probe.Run(async context =>
            {
                var client = context.RequestServices.GetRequiredService<IHttpClientFactory>().CreateClient();
                using var response = await client.GetAsync(outgoingTargetUrl);
                context.Response.StatusCode = (int)response.StatusCode;
            }));

            next(app);
        };
    }
}
