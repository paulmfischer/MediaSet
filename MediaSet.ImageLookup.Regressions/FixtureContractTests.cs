using System.Net;
using MediaSet.Api.Infrastructure.Lookup.Clients.Igdb;
using MediaSet.Api.Infrastructure.Lookup.Models;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NUnit.Framework;

namespace MediaSet.ImageLookup.Regressions;

[TestFixture]
[NonParallelizable]
public sealed class FixtureContractTests
{
    [SetUp]
    public void RecordStart() => RegressionSuite.Progress("scenario-start", TestContext.CurrentContext.Test.Name);

    [TearDown]
    public void RecordResult() => RegressionSuite.Progress("scenario-finished", new
    {
        id = TestContext.CurrentContext.Test.Name,
        status = TestContext.CurrentContext.Result.Outcome.Status.ToString(),
        failure = TestContext.CurrentContext.Result.Message
    });

    [Test]
    public async Task StrictTransport_SequenceBodyHeadersAndBinary_UsesOnlyFixturesAsync()
    {
        using var transport = new StrictHttpFixtures(RegressionSuite.Progress);
        transport.Add(new Fixture
        {
            Id = "contract-sequence",
            Method = "POST",
            Host = "fixture.invalid",
            Path = "/lookup",
            Query = "?q=book",
            Body = "query body",
            Responses = [new() { Status = 429, Headers = new() { ["Retry-After"] = "60" } },
                new() { ContentType = "image/png", Base64 = "AQID" }]
        });
        using var client = transport.CreateClient("");
        using var first = await client.PostAsync("https://fixture.invalid/lookup?q=book", new StringContent("query body"));
        Assert.That(first.StatusCode, Is.EqualTo(HttpStatusCode.TooManyRequests));
        Assert.That(first.Headers.RetryAfter!.Delta, Is.EqualTo(TimeSpan.FromSeconds(60)));
        using var second = await client.PostAsync("https://fixture.invalid/lookup?q=book", new StringContent("query body"));
        Assert.That(await second.Content.ReadAsByteArrayAsync(), Is.EqualTo(new byte[] { 1, 2, 3 }));
        transport.VerifyComplete();
    }

    [TestCase("method")]
    [TestCase("host")]
    [TestCase("path")]
    [TestCase("query")]
    [TestCase("body")]
    [TestCase("header")]
    public void StrictTransport_UnexpectedRequest_RemainsFailureAfterCallerCatches(string mismatch)
    {
        using var transport = new StrictHttpFixtures(RegressionSuite.Progress);
        transport.Add(new Fixture { Id = "expected", Host = "fixture.invalid", Path = "/expected", RequestHeaders = new() { ["X-Fixture"] = "expected" }, Responses = [new()] });
        using var client = transport.CreateClient("");
        using var request = new HttpRequestMessage(mismatch == "method" ? HttpMethod.Post : HttpMethod.Get,
            $"https://{(mismatch == "host" ? "other.invalid" : "fixture.invalid")}/{(mismatch == "path" ? "other" : "expected")}{(mismatch == "query" ? "?extra=1" : "")}");
        request.Headers.Add("X-Fixture", mismatch == "header" ? "other" : "expected");
        if (mismatch == "body")
        {
            request.Content = new StringContent("unexpected");
        }
        Assert.ThrowsAsync<InvalidOperationException>(async () => await client.SendAsync(request));
        Assert.Throws<InvalidOperationException>(transport.VerifyComplete);
    }

    [TestCase("network", typeof(HttpRequestException))]
    [TestCase("timeout", typeof(TaskCanceledException))]
    public void StrictTransport_ConfiguredFailure_ThrowsWithoutNetwork(string failure, Type expected)
    {
        using var transport = new StrictHttpFixtures(RegressionSuite.Progress);
        transport.Add(new Fixture { Id = failure, Host = "fixture.invalid", Path = "/", Responses = [new() { Failure = failure }] });
        using var client = transport.CreateClient("");
        Assert.ThrowsAsync(expected, async () => await client.GetAsync("https://fixture.invalid/"));
        transport.VerifyComplete();
    }

    [Test]
    public async Task StrictTransport_PendingRequest_ObservesCancellationAsync()
    {
        using var transport = new StrictHttpFixtures(RegressionSuite.Progress);
        transport.Add(new Fixture { Id = "cancellation", Host = "fixture.invalid", Path = "/", Responses = [new() { Failure = "wait-for-cancellation" }] });
        using var client = transport.CreateClient("");
        using var cancellation = new CancellationTokenSource();
        var pending = client.GetAsync("https://fixture.invalid/", cancellation.Token);
        await cancellation.CancelAsync();
        Assert.That(async () => await pending, Throws.InstanceOf<OperationCanceledException>());
        transport.VerifyComplete();
    }

    [Test]
    public async Task IgdbAuthentication_InjectedTransport_UsesFixtureAndCachesTokenAsync()
    {
        using var transport = new StrictHttpFixtures(RegressionSuite.Progress);
        transport.Add(new Fixture
        {
            Id = "igdb-auth",
            Method = "POST",
            Host = "id.twitch.tv",
            Path = "/oauth2/token",
            Body = "client_id=fixture-client&client_secret=fixture-secret&grant_type=client_credentials",
            Responses = [new() { Text = "{\"access_token\":\"fixture-token\",\"expires_in\":3600,\"token_type\":\"bearer\"}" }]
        });
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var service = new IgdbTokenService(Options.Create(new IgdbConfiguration
        {
            ClientId = "fixture-client",
            ClientSecret = "fixture-secret",
            TokenUrl = "https://id.twitch.tv/oauth2/token"
        }), cache, NullLogger<IgdbTokenService>.Instance, transport);
        Assert.That(await service.GetAccessTokenAsync(CancellationToken.None), Is.EqualTo("fixture-token"));
        Assert.That(await service.GetAccessTokenAsync(CancellationToken.None), Is.EqualTo("fixture-token"));
        transport.VerifyComplete();
    }
}
