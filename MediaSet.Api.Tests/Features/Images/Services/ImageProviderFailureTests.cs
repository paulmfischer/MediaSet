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
    public async Task LookupAndSaveImageAsync_UpcBurstLimitRecovers_DownloadsArtworkSuccessfully()
    {
        var calls = 0;
        using var http = new HttpClient(new Handler((_, _) =>
        {
            var response = new HttpResponseMessage(++calls == 1 ? HttpStatusCode.TooManyRequests : HttpStatusCode.OK);
            if (calls == 1)
            {
                response.Headers.Add("X-RateLimit-Reset", DateTimeOffset.UtcNow.AddSeconds(2).ToUnixTimeSeconds().ToString());
            }
            else
            {
                response.Content = new StringContent("""{"code":"OK","items":[{"title":"Movie"}]}""");
            }
            return Task.FromResult(response);
        })) { BaseAddress = new Uri("https://example.com/") };
        var upc = new MediaSet.Api.Infrastructure.Lookup.Clients.UpcItemDb.UpcItemDbClient(http,
            Options.Create(new UpcItemDbConfiguration { MinDelayBetweenRequestsMs = 0,
                MaxRequestsPerMinute = 10000, MaxRequestsPerDay = 10000, MaxRetryPauseSeconds = 5 }),
            NullLogger<MediaSet.Api.Infrastructure.Lookup.Clients.UpcItemDb.UpcItemDbClient>.Instance);
        var tmdb = new Mock<ITmdbClient>();
        tmdb.Setup(t => t.SearchMovieAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TmdbSearchResponse(1, [new TmdbMovieSearchResult(1, "Movie", null, null)], 1, 1));
        tmdb.Setup(t => t.GetMovieDetailsAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TmdbMovieResponse(1, "Movie", null, null, null, [], [], 0, "/cover.jpg"));
        using var services = new ServiceCollection().AddSingleton<ILookupStrategy<MovieResponse>>(
            new MovieLookupStrategy(upc, tmdb.Object, NullLogger<MovieLookupStrategy>.Instance)).BuildServiceProvider();
        var images = new Mock<IImageService>();
        images.Setup(i => i.DownloadAndSaveImageAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<CancellationToken>())).ReturnsAsync(new Image { FilePath = "saved.jpg" });
        var lookup = new ImageLookupService(new LookupStrategyFactory(services), images.Object,
            NullLogger<ImageLookupService>.Instance);
        var result = await lookup.LookupAndSaveImageAsync(new Movie { Id = "1", Title = "Movie", Barcode = "123" },
            MediaTypes.Movies, CancellationToken.None);
        Assert.That(calls, Is.EqualTo(2));
        Assert.That(result.Success, Is.True);
        Assert.That(result.SavedImage?.FilePath, Is.EqualTo("saved.jpg"));
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
