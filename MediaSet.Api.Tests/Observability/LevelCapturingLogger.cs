using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;

namespace MediaSet.Api.Tests.Observability;

/// <summary>Records each log entry's level so tests can assert on how loudly something was logged.</summary>
public sealed class LevelCapturingLogger<T> : ILogger<T>
{
    public List<(LogLevel Level, string Message)> Entries { get; } = [];

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
        Entries.Add((logLevel, formatter(state, exception)));
}
