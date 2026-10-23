using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using MediaSet.Api.Features.Images.Models;
using MediaSet.Api.Features.Images.Services;
using MediaSet.Api.Infrastructure.Lookup;
using MediaSet.Api.Infrastructure.Lookup.Clients.UpcItemDb;
using MediaSet.Api.Infrastructure.Lookup.Models;
using MediaSet.Api.Shared.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NUnit.Framework;

namespace MediaSet.Api.Tests.Features.Images.Services;

[TestFixture]
public class ProviderOutcomeTests
{
    private sealed class FakeHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public int Calls { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            Calls++;
            return Task.FromResult(respond(request));
        }
    }

    [TestCase(HttpStatusCode.TooManyRequests, ImageLookupOutcome.QuotaLimited)]
    [TestCase(HttpStatusCode.ServiceUnavailable, ImageLookupOutcome.ProviderUnavailable)]
    [TestCase(HttpStatusCode.Unauthorized, ImageLookupOutcome.Unauthorized)]
    [TestCase(HttpStatusCode.Forbidden, ImageLookupOutcome.Unauthorized)]
    public void HttpFailures_PreserveClassification(HttpStatusCode status, ImageLookupOutcome outcome)
    {
        using var scope = new LookupFailureScope();
        using var response = new HttpResponseMessage(status);
        LookupFailureScope.RecordStatus(response, "TmdbClient.cs");
        var error = Assert.Throws<ProviderLookupException>(scope.ThrowIfFailed)!;
        Assert.That(error.Outcome, Is.EqualTo(outcome));
        Assert.That(error.Provider, Is.EqualTo("Tmdb"));
    }

    [Test]
    public void NotFound_DoesNotBecomeProviderFailure()
    {
        using var scope = new LookupFailureScope();
        using var response = new HttpResponseMessage(HttpStatusCode.NotFound);
        LookupFailureScope.RecordStatus(response);
        Assert.DoesNotThrow(scope.ThrowIfFailed);
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task Quota_DefersSubsequentClientsUntilReset_AndPersistsEligibility(bool retryAfter)
    {
        var reset = DateTimeOffset.UtcNow.AddHours(11);
        using var handler = new FakeHandler(_ =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
            if (retryAfter)
            {
                response.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.FromHours(11));
            }
            else
            {
                response.Headers.Add("X-RateLimit-Reset", reset.ToUnixTimeSeconds().ToString());
            }
            return response;
        });
        using var http = new HttpClient(handler) { BaseAddress = new Uri($"https://{Guid.NewGuid():N}.test/") };
        var config = Options.Create(new UpcItemDbConfiguration { MinDelayBetweenRequestsMs = 0 });
        for (var i = 0; i < 2; i++)
        {
            using var scope = new LookupFailureScope();
            var client = new UpcItemDbClient(http, config, NullLogger<UpcItemDbClient>.Instance);
            Assert.That(await client.GetItemByCodeAsync("883929293513", CancellationToken.None), Is.Null);
            var error = Assert.Throws<ProviderLookupException>(scope.ThrowIfFailed)!;
            Assert.That(error.Outcome, Is.EqualTo(ImageLookupOutcome.QuotaLimited));
            Assert.That(error.RetryAt, Is.EqualTo(reset.UtcDateTime).Within(TimeSpan.FromSeconds(2)));
            var state = ImageRetryPolicy.Complete(new Movie(), new(false, null, null, error.Message,
                Outcome: error.Outcome, Provider: error.Provider, AttemptedStrategy: "Movies:Upc", RetryAt: error.RetryAt),
                new BackgroundImageLookupConfiguration(), DateTime.UtcNow);
            Assert.That(state.PermanentFailure, Is.False);
            Assert.That(state.NextEligibleAt, Is.EqualTo(error.RetryAt));
            Assert.That(state.Provider, Is.EqualTo("UpcItemDb"));
            Assert.That(state.AttemptedStrategy, Is.EqualTo("Movies:Upc"));
        }
        Assert.That(handler.Calls, Is.EqualTo(1));
    }

    [TestCase("Trial", "prod/trial/lookup", false, false)]
    [TestCase("Paid", "prod/v1/lookup", true, false)]
    [TestCase("Trial", "prod/trial/lookup", false, true)]
    [TestCase("Paid", "prod/v1/lookup", true, true)]
    public async Task Plan_RoutesEndpointAndHeaders(string plan, string path, bool keyed, bool burst)
    {
        var calls = 0;
        using var handler = new FakeHandler(request =>
        {
            Assert.That(request.RequestUri!.AbsolutePath, Is.EqualTo("/" + path));
            Assert.That(request.Headers.Contains("user_key"), Is.EqualTo(keyed));
            Assert.That(request.Headers.Contains("key_type"), Is.EqualTo(keyed));
            if (keyed)
            {
                Assert.That(request.Headers.GetValues("user_key"), Is.EqualTo(new[] { "fake-key" }));
                Assert.That(request.Headers.GetValues("key_type"), Is.EqualTo(new[] { "3scale" }));
            }
            if (++calls == 1 && burst)
            {
                var limited = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
                limited.Headers.Add("X-RateLimit-Reset", DateTimeOffset.UtcNow.AddSeconds(2).ToUnixTimeSeconds().ToString());
                return limited;
            }
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"items\":[]}") };
        });
        using var http = new HttpClient(handler) { BaseAddress = new Uri($"https://{Guid.NewGuid():N}.test/") };
        var config = Options.Create(new UpcItemDbConfiguration { Plan = plan, ApiKey = "fake-key", MinDelayBetweenRequestsMs = 0 });
        using var scope = new LookupFailureScope();
        var client = new UpcItemDbClient(http, config, NullLogger<UpcItemDbClient>.Instance);
        Assert.That(await client.GetItemByCodeAsync("123", CancellationToken.None), Is.Not.Null);
        Assert.DoesNotThrow(scope.ThrowIfFailed);
        Assert.That(handler.Calls, Is.EqualTo(burst ? 2 : 1));
    }

    [TestCase("Paid", null, false)]
    [TestCase("Trial", null, true)]
    [TestCase("Paid", "fake-key", true)]
    [TestCase("Unknown", "fake-key", false)]
    public void Plan_ValidatesExplicitPaidOptIn(string plan, string? key, bool valid)
    {
        Assert.That(new UpcItemDbConfiguration { Plan = plan, ApiKey = key }.IsValid(out _), Is.EqualTo(valid));
    }

    [Test]
    public async Task GenericProviderQuota_PreservesHeadersAndAllowsIndependentProvider()
    {
        using var handler = new FakeHandler(_ =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
            response.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.FromHours(3));
            return response;
        });
        using var exhausted = new HttpClient(handler) { BaseAddress = new Uri($"https://{Guid.NewGuid():N}.test/") };
        using (var first = new LookupFailureScope())
        {
            using var response = await exhausted.GetProviderAsync("lookup", CancellationToken.None);
            var error = Assert.Throws<ProviderLookupException>(first.ThrowIfFailed)!;
            Assert.That(error.RetryAt, Is.EqualTo(DateTime.UtcNow.AddHours(3)).Within(TimeSpan.FromSeconds(2)));
        }
        using (var second = new LookupFailureScope())
        {
            Assert.ThrowsAsync<ProviderLookupException>(async () => await exhausted.GetProviderAsync("lookup", CancellationToken.None));
            using var independentHandler = new FakeHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
            using var independent = new HttpClient(independentHandler) { BaseAddress = new Uri($"https://{Guid.NewGuid():N}.test/") };
            using var success = await independent.GetProviderAsync("lookup", CancellationToken.None);
            Assert.That(success.IsSuccessStatusCode, Is.True);
            Assert.That(independentHandler.Calls, Is.EqualTo(1));
        }
        Assert.That(handler.Calls, Is.EqualTo(1));
    }
}
