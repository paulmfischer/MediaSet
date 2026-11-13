using System;
using System.Collections.Generic;
using MediaSet.Api.Shared.Extensions;
using Microsoft.Extensions.Logging;

namespace MediaSet.Api.Tests.Observability;

/// <summary>
/// Records each log entry's level (and the Provider scope property in effect, parallel in <see cref="Providers"/>)
/// so tests can assert on how loudly something was logged.
/// </summary>
public sealed class LevelCapturingLogger<T> : ILogger<T>
{
    private readonly Stack<string?> _providers = new();

    public List<(LogLevel Level, string Message)> Entries { get; } = [];

    /// <summary>The Provider scope value active for each entry in <see cref="Entries"/>, or null.</summary>
    public List<string?> Providers { get; } = [];

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull
    {
        var provider = state is IReadOnlyDictionary<string, object?> dict && dict.TryGetValue(LogScopes.ProviderProperty, out var value)
            ? value as string
            : null;
        _providers.Push(provider);
        return new Scope(_providers);
    }

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        Entries.Add((logLevel, formatter(state, exception)));
        Providers.Add(_providers.TryPeek(out var provider) ? provider : null);
    }

    private sealed class Scope(Stack<string?> providers) : IDisposable
    {
        public void Dispose() => providers.TryPop(out _);
    }
}
