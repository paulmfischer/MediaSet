using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using MediaSet.Api.Infrastructure.Lookup;
using NUnit.Framework;

namespace MediaSet.Api.Tests.Infrastructure.Lookup;

[TestFixture]
public class ProviderFailureStampingTests
{
    private const string FakeCaller = "/src/Clients/FakeProvider/FakeProviderClient.cs";

    [Test]
    public void Record_HttpRequestException_StampsFriendlyProviderName()
    {
        var ex = new HttpRequestException("boom");

        LookupFailureScope.Record(ex, FakeCaller);

        Assert.That(ex.Data[LookupFailureScope.ProviderDataKey], Is.EqualTo("FakeProvider"));
    }

    [Test]
    public void Record_NotFoundHttpRequestException_StillStampsProvider()
    {
        var ex = new HttpRequestException("missing", null, HttpStatusCode.NotFound);

        LookupFailureScope.Record(ex, FakeCaller);

        Assert.That(ex.Data[LookupFailureScope.ProviderDataKey], Is.EqualTo("FakeProvider"));
    }

    [Test]
    public void Record_ExceptionAlreadyStamped_KeepsFirstProvider()
    {
        var ex = new HttpRequestException("boom");
        LookupFailureScope.Stamp(ex, "First");

        LookupFailureScope.Record(ex, FakeCaller);

        Assert.That(ex.Data[LookupFailureScope.ProviderDataKey], Is.EqualTo("First"));
    }

    [Test]
    public void Stamp_ProviderLookupException_DoesNotAddData()
    {
        var ex = new ProviderLookupException("Tmdb", MediaSet.Api.Shared.Models.ImageLookupOutcome.QuotaLimited, "quota");

        LookupFailureScope.Stamp(ex, "Other");

        Assert.That(ex.Data.Contains(LookupFailureScope.ProviderDataKey), Is.False);
    }

    [Test]
    public void ProviderName_ClientFilePath_StripsClientSuffix()
    {
        Assert.That(LookupFailureScope.ProviderName("/a/b/MusicBrainzClient.cs"), Is.EqualTo("MusicBrainz"));
    }

    [Test]
    public async Task GetProviderFromJsonAsync_ErrorStatus_ThrowsStampedWithCallerProvider()
    {
        using var client = new HttpClient(new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.Forbidden)))
        { BaseAddress = new Uri("https://stamp-status.test/") };

        var ex = await Assert.ThrowsAsync<HttpRequestException>(async () =>
            await client.GetProviderFromJsonAsync<object>("x", CancellationToken.None, FakeCaller));

        Assert.That(ex!.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
        Assert.That(ex.Data[LookupFailureScope.ProviderDataKey], Is.EqualTo("FakeProvider"));
    }

    [Test]
    public async Task GetProviderAsync_NetworkFailure_ThrowsStampedWithCallerProvider()
    {
        using var client = new HttpClient(new StubHandler(_ => throw new HttpRequestException("down")))
        { BaseAddress = new Uri("https://stamp-network.test/") };

        var ex = await Assert.ThrowsAsync<HttpRequestException>(async () =>
            await client.GetProviderAsync("x", CancellationToken.None, FakeCaller));

        Assert.That(ex!.Data[LookupFailureScope.ProviderDataKey], Is.EqualTo("FakeProvider"));
    }

    [Test]
    public async Task GetProviderAsync_Timeout_ThrowsStampedTaskCanceledException()
    {
        using var client = new HttpClient(new StubHandler(_ => throw new TaskCanceledException("t", new TimeoutException())))
        { BaseAddress = new Uri("https://stamp-timeout.test/") };

        var ex = await Assert.ThrowsAsync<TaskCanceledException>(async () =>
            await client.GetProviderAsync("x", CancellationToken.None, FakeCaller));

        Assert.That(ex!.Data[LookupFailureScope.ProviderDataKey], Is.EqualTo("FakeProvider"));
    }

    [Test]
    public async Task GetProviderAsync_QuotaLimitedInScope_UsesFriendlyProviderNameNotHost()
    {
        using var client = new HttpClient(new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.TooManyRequests)))
        { BaseAddress = new Uri($"https://{Guid.NewGuid():N}.test/") };
        using var scope = new LookupFailureScope();

        using var response = await client.GetProviderAsync("x", CancellationToken.None, FakeCaller);

        var error = Assert.Throws<ProviderLookupException>(scope.ThrowIfFailed)!;
        Assert.That(error.Provider, Is.EqualTo("FakeProvider"));
        Assert.That(scope.AttemptedProviders, Is.EqualTo("FakeProvider"));
    }

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(respond(request));
    }
}
