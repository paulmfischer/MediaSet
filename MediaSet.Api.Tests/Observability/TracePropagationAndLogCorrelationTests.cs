using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using OpenTelemetry.Trace;
using Serilog.Events;

namespace MediaSet.Api.Tests.Observability;

/// <summary>
/// Behavior checks for trace propagation, span creation and log correlation. They run the real API
/// host in memory with isolated capture seams so they can guard package upgrades.
/// The in-memory log sink is a process-wide static that only these tests attach (through their own
/// Serilog:WriteTo config), so the fixture must not run in parallel with itself or other host tests
/// that clear or read it; hence [NonParallelizable].
/// </summary>
[TestFixture]
[NonParallelizable]
public class TracePropagationAndLogCorrelationTests
{
    private const string RealEndpoint = "/config/lookup-capabilities";
    private const string IncomingTraceId = "0af7651916cd43dd8448eb211c80319c";
    private const string IncomingSpanId = "b7ad6b7169203331";
    private const string IncomingTraceparent = $"00-{IncomingTraceId}-{IncomingSpanId}-01";

    private InMemoryActivityExporter _exporter = null!;

    [SetUp]
    public void SetUp()
    {
        _exporter = new InMemoryActivityExporter();
        InMemoryLogSink.Clear();
    }

    [Test]
    public async Task Startup_WithExternalLoggingEnabled_ServesRequests()
    {
        using var factory = ObservabilityTestHost.Create(_exporter, externalLoggingEnabled: true);
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(RealEndpoint);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    [Test]
    public async Task Startup_WithExternalLoggingDisabled_ServesRequests()
    {
        using var factory = ObservabilityTestHost.Create(_exporter, externalLoggingEnabled: false);
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(RealEndpoint);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    [Test]
    public async Task IncomingRequest_WithTraceparent_ContinuesTraceInServerSpan()
    {
        using var factory = ObservabilityTestHost.Create(_exporter, externalLoggingEnabled: false);
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, RealEndpoint);
        request.Headers.Add("traceparent", IncomingTraceparent);

        using var response = await client.SendAsync(request);

        var serverSpan = await WaitForServerSpanAsync();
        Assert.That(serverSpan, Is.Not.Null, "expected a SerilogTracing server span event");
        Assert.That(serverSpan!.TraceId?.ToString(), Is.EqualTo(IncomingTraceId));
        Assert.That(Property(serverSpan, "ParentSpanId"), Is.EqualTo(IncomingSpanId));
    }

    [Test]
    public async Task IncomingRequest_WithoutTraceparent_StartsNewRootServerSpan()
    {
        using var factory = ObservabilityTestHost.Create(_exporter, externalLoggingEnabled: false);
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(RealEndpoint);

        var serverSpan = await WaitForServerSpanAsync();
        Assert.That(serverSpan, Is.Not.Null, "expected a SerilogTracing server span event");
        Assert.That(serverSpan!.TraceId?.ToString(), Is.Not.Null.And.Not.EqualTo(IncomingTraceId));
        Assert.That(Property(serverSpan, "ParentSpanId"), Is.Null);
    }

    [Test]
    public async Task OutgoingHttpClientCall_InsideRequest_CreatesClientSpanChildOfServerSpan()
    {
        using var target = new LoopbackHttpTarget();
        using var factory = ObservabilityTestHost.Create(_exporter, false, target.Url);
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/_probe/outgoing");

        var serverSpan = await WaitForServerSpanAsync();
        Assert.That(serverSpan, Is.Not.Null, "expected a SerilogTracing server span event");
        var clientSpan = await WaitForSpanAsync(a => a.Kind == ActivityKind.Client);
        Assert.That(clientSpan, Is.Not.Null, ExportedSpans());
        Assert.That(clientSpan!.TraceId.ToString(), Is.EqualTo(serverSpan!.TraceId?.ToString()));
        Assert.That(clientSpan!.ParentSpanId.ToString(), Is.EqualTo(serverSpan!.SpanId?.ToString()));
    }

    [Test]
    public async Task OutgoingHttpClientCall_InsideRequest_PropagatesTraceparentOfClientSpan()
    {
        using var target = new LoopbackHttpTarget();
        using var factory = ObservabilityTestHost.Create(_exporter, false, target.Url);
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/_probe/outgoing");

        var clientSpan = await WaitForSpanAsync(a => a.Kind == ActivityKind.Client);
        Assert.That(clientSpan, Is.Not.Null, ExportedSpans());
        var traceparent = target.LastTraceparent;
        Assert.That(traceparent, Is.Not.Null);
        var parts = traceparent!.Split('-');
        Assert.That(parts[1], Is.EqualTo(clientSpan!.TraceId.ToString()));
        Assert.That(parts[2], Is.EqualTo(clientSpan!.SpanId.ToString()));
    }

    [Test]
    public async Task Response_ForRequestWithTraceparent_DoesNotEchoTraceHeaders()
    {
        // Recorded current behavior: TraceIdHeaderMiddleware only sets HttpContext.TraceIdentifier,
        // so no traceparent or trace-id response header is emitted.
        using var factory = ObservabilityTestHost.Create(_exporter, externalLoggingEnabled: false);
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, RealEndpoint);
        request.Headers.Add("traceparent", IncomingTraceparent);

        using var response = await client.SendAsync(request);

        var headerNames = response.Headers.Select(h => h.Key).ToList();
        Assert.That(headerNames, Has.None.EqualTo("traceparent").IgnoreCase);
        Assert.That(headerNames, Has.None.Contain("trace").IgnoreCase);
    }

    [Test]
    public async Task LogEventsInRequest_WithTraceparent_CarryActiveTraceAndSpanIds()
    {
        using var factory = ObservabilityTestHost.Create(_exporter, externalLoggingEnabled: false);
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, RealEndpoint);
        request.Headers.Add("traceparent", IncomingTraceparent);

        using var response = await client.SendAsync(request);

        var serverSpan = await WaitForServerSpanAsync();
        Assert.That(serverSpan, Is.Not.Null, "expected a SerilogTracing server span event");
        var requestLogs = InMemoryLogSink.Events
            .Where(e => Property(e, "RequestPath") == RealEndpoint && e.Properties.ContainsKey("TraceId") && !e.Properties.ContainsKey("SpanStartTimestamp"))
            .ToList();
        Assert.That(requestLogs, Is.Not.Empty, "expected at least one log event correlated to the request");
        Assert.That(requestLogs.Select(e => Property(e, "TraceId")), Has.All.EqualTo(IncomingTraceId));
        Assert.That(requestLogs.Select(e => Property(e, "SpanId")), Has.All.EqualTo(serverSpan!.SpanId.ToString()));
    }

    [Test]
    public async Task SerilogTracing_ForIncomingRequest_CapturesServerSpanInSink()
    {
        using var factory = ObservabilityTestHost.Create(_exporter, externalLoggingEnabled: false);
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, RealEndpoint);
        request.Headers.Add("traceparent", IncomingTraceparent);

        using var response = await client.SendAsync(request);

        var spans = await WaitForSerilogSpansAsync(e =>
            e.TraceId?.ToString() == IncomingTraceId && Property(e, "SpanKind") == "Server");
        Assert.That(spans, Is.Not.Empty, "expected a SerilogTracing server span event in the in-memory sink");
    }

    [Test]
    public async Task SerilogTracing_ForOutgoingHttpClientCall_CapturesClientSpanInSink()
    {
        using var target = new LoopbackHttpTarget();
        using var factory = ObservabilityTestHost.Create(_exporter, false, target.Url);
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/_probe/outgoing");

        var clientSpan = await WaitForSpanAsync(a => a.Kind == ActivityKind.Client);
        Assert.That(clientSpan, Is.Not.Null, ExportedSpans());
        var spans = await WaitForSerilogSpansAsync(e =>
            e.SpanId?.ToString() == clientSpan!.SpanId.ToString() && Property(e, "SpanKind") == "Client");
        Assert.That(spans, Is.Not.Empty, "expected the outgoing call as a SerilogTracing span event");
    }

    [Test]
    public async Task IncomingRequest_WithSerilogTracingActive_ExportsNoServerSpanToOpenTelemetry()
    {
        // Recorded current behavior: SerilogTracing owns the ASP.NET Core server span, and the
        // built-in Microsoft.AspNetCore activity is not recorded, so OpenTelemetry sees no server span.
        // The outgoing call acts as a positive control: its client span proves the OpenTelemetry
        // pipeline is live, and ForceFlush plus the SerilogTracing server span event (emitted when the
        // request ends) remove any dependence on sleeping.
        using var target = new LoopbackHttpTarget();
        using var factory = ObservabilityTestHost.Create(_exporter, false, target.Url);
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/_probe/outgoing");

        var serverSpan = await WaitForServerSpanAsync();
        Assert.That(serverSpan, Is.Not.Null, "expected a SerilogTracing server span event");
        var clientSpan = await WaitForSpanAsync(a => a.Kind == ActivityKind.Client);
        Assert.That(clientSpan, Is.Not.Null, ExportedSpans());
        factory.Services.GetRequiredService<TracerProvider>().ForceFlush();
        Assert.That(_exporter.Activities.Where(a => a.Kind == ActivityKind.Server), Is.Empty);
    }

    private static string? Property(LogEvent logEvent, string name) =>
        logEvent.Properties.TryGetValue(name, out var value) && value is ScalarValue scalar
            ? scalar.Value?.ToString()
            : null;

    private string ExportedSpans() =>
        "Expected span was not exported. Exported: " +
        string.Join(", ", _exporter.Activities.Select(a => $"{a.Kind}:{a.OperationName}"));

    private async Task<Activity?> WaitForSpanAsync(Func<Activity, bool> predicate)
    {
        for (var i = 0; i < 50; i++)
        {
            var match = _exporter.Activities.FirstOrDefault(predicate);
            if (match != null)
            {
                return match;
            }

            await Task.Delay(100);
        }

        return null;
    }

    private static async Task<LogEvent?> WaitForServerSpanAsync()
    {
        var spans = await WaitForSerilogSpansAsync(e => Property(e, "SpanKind") == "Server");
        return spans.Count > 0 ? spans[0] : null;
    }

    private static async Task<IReadOnlyList<LogEvent>> WaitForSerilogSpansAsync(Func<LogEvent, bool> predicate)
    {
        // SerilogTracing span events carry the SpanStartTimestamp property.
        for (var i = 0; i < 50; i++)
        {
            var spans = InMemoryLogSink.Events
                .Where(e => e.Properties.ContainsKey("SpanStartTimestamp") && predicate(e))
                .ToList();
            if (spans.Count > 0)
            {
                return spans;
            }

            await Task.Delay(100);
        }

        return Array.Empty<LogEvent>();
    }
}
