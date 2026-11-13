using System.Collections.Generic;
using NUnit.Framework;
using MediaSet.Api.Shared.Extensions;
using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace MediaSet.Api.Tests.Observability;

[TestFixture]
public class LoggingNoiseOverridesTests
{
    private sealed class ListSink : ILogEventSink
    {
        public List<LogEvent> Events { get; } = [];

        public void Emit(LogEvent logEvent) => Events.Add(logEvent);
    }

    private static (Serilog.ILogger Logger, ListSink Sink) CreateLogger()
    {
        var sink = new ListSink();
        var logger = new LoggerConfiguration()
            .MinimumLevel.Verbose()
            .ApplyNoiseOverrides()
            .WriteTo.Sink(sink)
            .CreateLogger();
        return (logger, sink);
    }

    [TestCase("Microsoft.AspNetCore.StaticFiles.StaticFileMiddleware")]
    [TestCase("Microsoft.AspNetCore.HttpLogging.HttpLoggingMiddleware")]
    [TestCase("Microsoft.AspNetCore.Http.Result.OkObjectResult")]
    [TestCase("Experimental.System.Net.Sockets")]
    [TestCase("Experimental.System.Net.Connections")]
    [TestCase("System.Net.Http.HttpClient.Default.LogicalHandler")]
    [TestCase("System.Net.Http.HttpClient.Default.ClientHandler")]
    [TestCase("Microsoft.AspNetCore.Hosting.Diagnostics")]
    [TestCase("Microsoft.AspNetCore.Routing.EndpointMiddleware")]
    public void ApplyNoiseOverrides_NoisyCategoryBelowWarning_IsDropped(string sourceContext)
    {
        var (logger, sink) = CreateLogger();
        var context = logger.ForContext("SourceContext", sourceContext);

        context.Information("noise");
        context.Debug("noise");

        Assert.That(sink.Events, Is.Empty);
    }

    [TestCase("Microsoft.AspNetCore.StaticFiles.StaticFileMiddleware")]
    [TestCase("Experimental.System.Net.Sockets")]
    [TestCase("System.Net.Http.HttpClient.Default.ClientHandler")]
    public void ApplyNoiseOverrides_NoisyCategoryAtWarning_IsKept(string sourceContext)
    {
        var (logger, sink) = CreateLogger();

        logger.ForContext("SourceContext", sourceContext).Warning("problem");

        Assert.That(sink.Events, Has.Count.EqualTo(1));
    }

    [TestCase("MediaSet.Api.Features.Images.Services.BackgroundImageLookupService", "Image run attempt: {@Attempt}")]
    [TestCase("MediaSet.Api.Features.Images.Services.BackgroundImageLookupService", "Image lookup outcome: {MediaType}")]
    [TestCase("SerilogTracing.Instrumentation.AspNetCore.AspNetCoreRequestInstrumentor", "HTTP {RequestMethod} {RequestPath}")]
    [TestCase("SerilogTracing.Instrumentation.HttpClientRequestInstrumentor", "HTTP {RequestMethod} {RequestUri}")]
    public void ApplyNoiseOverrides_ApplicationAndSpanEventsAtInformation_AreKept(string sourceContext, string template)
    {
        var (logger, sink) = CreateLogger();

        logger.ForContext("SourceContext", sourceContext).Information(template, "a", "b");

        Assert.That(sink.Events, Has.Count.EqualTo(1));
        Assert.That(sink.Events[0].MessageTemplate.Text, Is.EqualTo(template));
    }
}
