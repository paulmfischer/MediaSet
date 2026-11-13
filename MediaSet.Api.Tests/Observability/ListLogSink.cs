using System.Collections.Concurrent;
using System.Collections.Generic;
using Serilog.Core;
using Serilog.Events;

namespace MediaSet.Api.Tests.Observability;

/// <summary>Collects Serilog events (thread-safe) so tests can assert on their properties.</summary>
public sealed class ListLogSink : ILogEventSink
{
    private readonly ConcurrentQueue<LogEvent> _events = new();

    public IEnumerable<LogEvent> Events => _events;

    public void Emit(LogEvent logEvent) => _events.Enqueue(logEvent);

    /// <summary>The scalar value of a property, or null when the event does not have it.</summary>
    public object? Scalar(LogEvent logEvent, string name) =>
        logEvent.Properties.TryGetValue(name, out var value) && value is ScalarValue scalar ? scalar.Value : null;
}
