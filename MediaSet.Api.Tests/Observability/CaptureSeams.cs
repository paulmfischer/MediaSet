using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using OpenTelemetry;
using Serilog.Configuration;
using Serilog.Core;
using Serilog.Events;

namespace MediaSet.Api.Tests.Observability;

/// <summary>
/// Process-wide in-memory Serilog sink. It is attached through the Serilog:WriteTo configuration
/// of the test host, so no real sink (and never the shared Seq service) is involved.
/// </summary>
public sealed class InMemoryLogSink : ILogEventSink
{
    private static readonly ConcurrentQueue<LogEvent> _captured = new();

    public static IReadOnlyList<LogEvent> Events => _captured.ToArray();

    public static void Clear() => _captured.Clear();

    public void Emit(LogEvent logEvent) => _captured.Enqueue(logEvent);
}

public static class InMemoryLogSinkExtensions
{
    /// <summary>Resolved by Serilog.Settings.Configuration as the sink named "InMemoryTestSink".</summary>
    public static Serilog.LoggerConfiguration InMemoryTestSink(this LoggerSinkConfiguration sinkConfiguration) =>
        sinkConfiguration.Sink(new InMemoryLogSink());
}

/// <summary>In-memory OpenTelemetry exporter that keeps finished activities.</summary>
public sealed class InMemoryActivityExporter : BaseExporter<Activity>
{
    private readonly ConcurrentQueue<Activity> _activities = new();

    public IReadOnlyList<Activity> Activities => _activities.ToArray();

    public override ExportResult Export(in Batch<Activity> batch)
    {
        foreach (var activity in batch)
        {
            _activities.Enqueue(activity);
        }

        return ExportResult.Success;
    }
}
