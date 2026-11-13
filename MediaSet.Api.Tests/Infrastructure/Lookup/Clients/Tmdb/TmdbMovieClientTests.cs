using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using MediaSet.Api.Infrastructure.Lookup;
using MediaSet.Api.Infrastructure.Lookup.Clients.Tmdb;
using MediaSet.Api.Infrastructure.Lookup.Models;
using MediaSet.Api.Tests.Observability;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NUnit.Framework;

namespace MediaSet.Api.Tests.Infrastructure.Lookup.Clients.Tmdb;

[TestFixture]
public class TmdbMovieClientTests
{
    private sealed class Handler(HttpStatusCode status) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token) =>
            Task.FromResult(new HttpResponseMessage(status));
    }

    private static (TmdbClient Client, LevelCapturingLogger<TmdbClient> Logger, HttpClient Http) Create(HttpStatusCode status)
    {
        var logger = new LevelCapturingLogger<TmdbClient>();
        var http = new HttpClient(new Handler(status)) { BaseAddress = new Uri("https://example.test/3/") };
        return (new TmdbClient(http, Options.Create(new TmdbConfiguration()), logger), logger, http);
    }

    [Test]
    public async Task SearchMovieAsync_RateLimit_ThrowsAndLogsWarningNotError()
    {
        var (client, logger, http) = Create(HttpStatusCode.TooManyRequests);
        using var _ = http;

        await Assert.ThrowsAsync<ProviderRateLimitException>(() => client.SearchMovieAsync("Alien", CancellationToken.None));

        Assert.That(logger.Entries.Select(e => e.Level), Does.Contain(LogLevel.Warning));
        Assert.That(logger.Entries.Select(e => e.Level), Does.Not.Contain(LogLevel.Error));
        Assert.That(logger.Providers, Does.Contain("TMDB"));
    }

    [Test]
    public async Task SearchMovieAsync_ServerError_ReturnsNullAndLogsWarningNotError()
    {
        var (client, logger, http) = Create(HttpStatusCode.ServiceUnavailable);
        using var _ = http;

        var result = await client.SearchMovieAsync("Alien", CancellationToken.None);

        Assert.That(result, Is.Null);
        Assert.That(logger.Entries.Select(e => e.Level), Does.Contain(LogLevel.Warning));
        Assert.That(logger.Entries.Select(e => e.Level), Does.Not.Contain(LogLevel.Error));
    }
}
