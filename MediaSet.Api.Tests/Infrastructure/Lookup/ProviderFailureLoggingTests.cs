using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using MediaSet.Api.Infrastructure.Lookup;
using MediaSet.Api.Shared.Extensions;
using Microsoft.Extensions.Logging;
using NUnit.Framework;

namespace MediaSet.Api.Tests.Infrastructure.Lookup;

[TestFixture]
public class ProviderFailureLoggingTests
{
    [TestCase(HttpStatusCode.TooManyRequests)]
    [TestCase(HttpStatusCode.InternalServerError)]
    [TestCase(HttpStatusCode.BadGateway)]
    [TestCase(HttpStatusCode.ServiceUnavailable)]
    public void LogProviderHttpFailure_RateLimitOr5xx_LogsWarningWithProvider(HttpStatusCode status)
    {
        var logger = new ScopeCapturingLogger();

        logger.LogProviderHttpFailure(new HttpRequestException("boom", null, status), LogProviders.Tmdb, "HTTP error for {Title}", "x");

        Assert.That(logger.Entries, Has.Count.EqualTo(1));
        Assert.That(logger.Entries[0].Level, Is.EqualTo(LogLevel.Warning));
        Assert.That(logger.Entries[0].Provider, Is.EqualTo("TMDB"));
    }

    [TestCase(HttpStatusCode.Unauthorized)]
    [TestCase(HttpStatusCode.BadRequest)]
    [TestCase(null)]
    public void LogProviderHttpFailure_OtherFailures_LogsErrorWithProvider(HttpStatusCode? status)
    {
        var logger = new ScopeCapturingLogger();

        logger.LogProviderHttpFailure(new HttpRequestException("boom", null, status), LogProviders.Igdb, "HTTP error for {Title}", "x");

        Assert.That(logger.Entries, Has.Count.EqualTo(1));
        Assert.That(logger.Entries[0].Level, Is.EqualTo(LogLevel.Error));
        Assert.That(logger.Entries[0].Provider, Is.EqualTo("IGDB"));
    }

    private sealed class ScopeCapturingLogger : ILogger
    {
        private string? _provider;

        public List<(LogLevel Level, string? Provider)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull
        {
            if (state is IReadOnlyDictionary<string, object?> dict && dict.TryGetValue(LogScopes.ProviderProperty, out var value))
            {
                _provider = value as string;
            }
            return new Reset(this);
        }

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Entries.Add((logLevel, _provider));

        private sealed class Reset(ScopeCapturingLogger owner) : IDisposable
        {
            public void Dispose() => owner._provider = null;
        }
    }
}
