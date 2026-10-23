using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using MediaSet.Api.Features.Images.Services;
using MediaSet.Api.Infrastructure.Lookup;
using MediaSet.Api.Infrastructure.Lookup.Clients.Tmdb;
using MediaSet.Api.Infrastructure.Lookup.Models;
using MediaSet.Api.Infrastructure.Lookup.Strategies;
using MediaSet.Api.Infrastructure.Storage;
using MediaSet.Api.Shared.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using NUnit.Framework;

namespace MediaSet.Api.Tests.Features.Images.Services;

[TestFixture]
public class ImageProviderFailureTests
{
    private sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token) => send(request, token);
    }

    [TestCase(HttpStatusCode.TooManyRequests)]
    [TestCase(HttpStatusCode.InternalServerError)]
    [TestCase(HttpStatusCode.ServiceUnavailable)]
    public async Task LookupAndSaveImageAsync_ProviderHttpFailure_IsRetryable(HttpStatusCode status)
    {
        using var http = new HttpClient(new Handler((_, _) => Task.FromResult(new HttpResponseMessage(status))))
            { BaseAddress = new Uri("https://example.com/") };
        var result = await LookupAsync(http);
        Assert.That(result.Success, Is.False);
        Assert.That(result.PermanentFailure, Is.False);
        Assert.That(result.Outcome ?? ImageLookupOutcome.TransientFailure, Is.EqualTo(ImageLookupOutcome.TransientFailure));
    }

    [Test]
    public async Task LookupAndSaveImageAsync_ProviderTimeout_IsRetryable()
    {
        using var http = new HttpClient(new Handler((_, _) => throw new TaskCanceledException("timeout")))
            { BaseAddress = new Uri("https://example.com/") };
        var result = await LookupAsync(http);
        Assert.That(result.PermanentFailure, Is.False);
        Assert.That(result.ErrorMessage, Does.Contain("timeout"));
    }

    private static async Task<ImageLookupResult> LookupAsync(HttpClient http)
    {
        var client = new TmdbClient(http, Options.Create(new TmdbConfiguration()), NullLogger<TmdbClient>.Instance);
        using var services = new ServiceCollection()
            .AddSingleton<MediaSet.Api.Infrastructure.Lookup.Strategies.ILookupStrategy<MediaSet.Api.Infrastructure.Lookup.Models.MovieResponse>>(
                new MovieLookupStrategy(Mock.Of<MediaSet.Api.Infrastructure.Lookup.Clients.UpcItemDb.IUpcItemDbClient>(),
                    client, NullLogger<MovieLookupStrategy>.Instance))
            .BuildServiceProvider();
        var lookup = new ImageLookupService(new LookupStrategyFactory(services), Mock.Of<IImageService>(),
            NullLogger<ImageLookupService>.Instance);
        return await lookup.LookupAndSaveImageAsync(new Movie { Id = "1", Title = "Movie" }, MediaTypes.Movies, CancellationToken.None);
    }

    [Test]
    public async Task LookupFailureScope_ConcurrentLookups_DoNotLeakFailures()
    {
        var recorded = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var checkedClean = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var failed = Task.Run(async () =>
        {
            using var scope = new LookupFailureScope();
            LookupFailureScope.Record(new TimeoutException("failed task"));
            recorded.SetResult();
            await checkedClean.Task;
            Assert.Throws<TimeoutException>(() => scope.ThrowIfFailed());
        });
        var clean = Task.Run(async () =>
        {
            await recorded.Task;
            using var scope = new LookupFailureScope();
            Assert.DoesNotThrow(() => scope.ThrowIfFailed());
            checkedClean.SetResult();
        });
        await Task.WhenAll(failed, clean);
        using var final = new LookupFailureScope();
        Assert.DoesNotThrow(() => final.ThrowIfFailed());
    }
}
