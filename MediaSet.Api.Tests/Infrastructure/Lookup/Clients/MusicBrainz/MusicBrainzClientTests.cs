using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using MediaSet.Api.Infrastructure.Lookup;
using MediaSet.Api.Infrastructure.Lookup.Clients.MusicBrainz;
using MediaSet.Api.Tests.Observability;
using Microsoft.Extensions.Logging;
using NUnit.Framework;

namespace MediaSet.Api.Tests.Infrastructure.Lookup.Clients.MusicBrainz;

[TestFixture]
public class MusicBrainzClientTests
{
    private sealed class Handler(HttpStatusCode status) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token) =>
            Task.FromResult(new HttpResponseMessage(status));
    }

    [Test]
    public async Task GetReleaseByBarcodeAsync_ServiceUnavailable_ThrowsAndLogsWarningNotError()
    {
        var logger = new LevelCapturingLogger<MusicBrainzClient>();
        using var http = new HttpClient(new Handler(HttpStatusCode.ServiceUnavailable)) { BaseAddress = new Uri("https://example.test/") };
        var client = new MusicBrainzClient(http, logger);

        await Assert.ThatAsync(() => client.GetReleaseByBarcodeAsync("012345678901", CancellationToken.None),
            Throws.TypeOf<ProviderRateLimitException>());

        Assert.That(logger.Entries.Select(e => e.Level), Does.Contain(LogLevel.Warning));
        Assert.That(logger.Entries.Select(e => e.Level), Does.Not.Contain(LogLevel.Error));
        Assert.That(logger.Providers, Does.Contain("MusicBrainz"));
    }
}
