using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;

namespace MediaSet.Api.Tests.Features.Plex;

/// <summary>Collects every log line (message and exception text) so tests can assert on what was logged.</summary>
public sealed class CapturingLoggerProvider : ILoggerProvider
{
    public List<string> Lines { get; } = [];

    public ILogger CreateLogger(string categoryName) => new CapturingLogger(categoryName, Lines);

    public void Dispose() { }

    private sealed class CapturingLogger(string category, List<string> lines) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            lock (lines)
            {
                lines.Add($"{category} {formatter(state, exception)} {exception}");
            }
        }
    }
}
