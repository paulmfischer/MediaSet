using System.Collections.Generic;
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

    private sealed class JsonHandler(string json, List<string> urls) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            urls.Add(request.RequestUri!.PathAndQuery);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json"),
            });
        }
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

    [Test]
    public async Task SearchByBarcodeAsync_ReturnsEveryReleaseAndQueriesTheBarcode()
    {
        var urls = new List<string>();
        var json = "{\"releases\":[{\"id\":\"r1\",\"release-group\":{\"id\":\"g1\"}},{\"id\":\"r2\",\"release-group\":{\"id\":\"g2\"}}]}";
        using var http = new HttpClient(new JsonHandler(json, urls)) { BaseAddress = new Uri("https://example.test/") };
        var client = new MusicBrainzClient(http, new LevelCapturingLogger<MusicBrainzClient>());

        var releases = await client.SearchByBarcodeAsync("012345678901", CancellationToken.None);

        Assert.That(releases.Select(r => r.ReleaseGroup!.Id), Is.EqualTo(["g1", "g2"]));
        Assert.That(urls, Has.Count.EqualTo(1));
        Assert.That(urls[0], Does.Contain("query=barcode:012345678901").And.Contain("limit=10"));
    }

    [Test]
    public async Task SearchByBarcodeAsync_NoReleases_ReturnsEmpty()
    {
        using var http = new HttpClient(new JsonHandler("{\"releases\":[]}", [])) { BaseAddress = new Uri("https://example.test/") };
        var client = new MusicBrainzClient(http, new LevelCapturingLogger<MusicBrainzClient>());

        var releases = await client.SearchByBarcodeAsync("012345678901", CancellationToken.None);

        Assert.That(releases, Is.Empty);
    }

    [Test]
    public async Task SearchByBarcodeAsync_ServiceUnavailable_ThrowsRateLimit()
    {
        using var http = new HttpClient(new Handler(HttpStatusCode.ServiceUnavailable)) { BaseAddress = new Uri("https://example.test/") };
        var client = new MusicBrainzClient(http, new LevelCapturingLogger<MusicBrainzClient>());

        await Assert.ThatAsync(() => client.SearchByBarcodeAsync("012345678901", CancellationToken.None),
            Throws.TypeOf<ProviderRateLimitException>());
    }

    [Test]
    public async Task SearchByBarcodeAsync_OtherErrorStatus_ThrowsInsteadOfReturningEmpty()
    {
        using var http = new HttpClient(new Handler(HttpStatusCode.InternalServerError)) { BaseAddress = new Uri("https://example.test/") };
        var client = new MusicBrainzClient(http, new LevelCapturingLogger<MusicBrainzClient>());

        await Assert.ThatAsync(() => client.SearchByBarcodeAsync("012345678901", CancellationToken.None),
            Throws.InstanceOf<HttpRequestException>().With.Property("StatusCode").EqualTo(HttpStatusCode.InternalServerError));
    }
}
