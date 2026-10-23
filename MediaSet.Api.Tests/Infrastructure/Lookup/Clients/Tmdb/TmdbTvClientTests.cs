using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using MediaSet.Api.Infrastructure.Lookup;
using MediaSet.Api.Infrastructure.Lookup.Clients.Tmdb;
using MediaSet.Api.Infrastructure.Lookup.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NUnit.Framework;

namespace MediaSet.Api.Tests.Infrastructure.Lookup.Clients.Tmdb;

[TestFixture]
public class TmdbTvClientTests
{
    private sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token) =>
            send(request, token);
    }

    private static TmdbClient Client(HttpClient http) =>
        new(http, Options.Create(new TmdbConfiguration()), NullLogger<TmdbClient>.Instance);

    [Test]
    public async Task SearchTvAsync_EncodesQueryAndReadsTvNames()
    {
        using var handler = new Handler((request, _) =>
        {
            Assert.That(request.RequestUri?.AbsolutePath, Is.EqualTo("/3/search/tv"));
            Assert.That(System.Web.HttpUtility.ParseQueryString(request.RequestUri!.Query)["query"],
                Is.EqualTo("Bob's Burgers & Friends"));
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                { Content = new StringContent("{\"results\":[{\"id\":32726,\"name\":\"Bob's Burgers\"}]}") });
        });
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://example.test/3/") };
        var result = await Client(http).SearchTvAsync("Bob's Burgers & Friends", CancellationToken.None);
        Assert.That(result?.Results[0].Name, Is.EqualTo("Bob's Burgers"));
    }

    [Test]
    public async Task GetSeasonDetailsAsync_ReadsPosterAndSpecialEpisodes()
    {
        using var handler = new Handler((request, _) =>
        {
            Assert.That(request.RequestUri?.AbsolutePath, Is.EqualTo("/3/tv/57243/season/0"));
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""
                    {"season_number":0,"air_date":"2013-12-25","poster_path":"/specials.jpg",
                     "episodes":[{"episode_number":84,"name":"The Time of the Doctor","air_date":"2013-12-25"}]}
                    """)
            });
        });
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://example.test/3/") };
        var result = await Client(http).GetSeasonDetailsAsync(57243, 0, CancellationToken.None);
        Assert.That(result?.PosterPath, Is.EqualTo("/specials.jpg"));
        Assert.That(result?.Episodes?[0].EpisodeNumber, Is.EqualTo(84));
    }

    [Test]
    public async Task GetTvDetailsAsync_ReadsSeriesFields()
    {
        using var handler = new Handler((request, _) =>
        {
            Assert.That(request.RequestUri?.AbsolutePath, Is.EqualTo("/3/tv/18347"));
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""
                    {"id":18347,"name":"Community","first_air_date":"2009-09-17",
                     "overview":"Plot","poster_path":"/series.jpg"}
                    """)
            });
        });
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://example.test/3/") };
        var result = await Client(http).GetTvDetailsAsync(18347, CancellationToken.None);
        Assert.That(result?.FirstAirDate, Is.EqualTo("2009-09-17"));
        Assert.That(result?.PosterPath, Is.EqualTo("/series.jpg"));
    }

    [Test]
    public void SearchTvAsync_RateLimit_PreservesProviderFailure()
    {
        using var handler = new Handler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.TooManyRequests)));
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://example.test/3/") };
        using var scope = new LookupFailureScope();
        var exception = Assert.ThrowsAsync<HttpRequestException>(() => Client(http).SearchTvAsync("Community", CancellationToken.None));
        Assert.That(exception?.StatusCode, Is.EqualTo(HttpStatusCode.TooManyRequests));
    }

    [Test]
    public void GetSeasonDetailsAsync_Canceled_PropagatesCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        using var handler = new Handler((_, _) =>
        {
            cancellation.Cancel();
            throw new OperationCanceledException(cancellation.Token);
        });
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://example.test/3/") };
        Assert.ThrowsAsync<OperationCanceledException>(() =>
            Client(http).GetSeasonDetailsAsync(1, 6, cancellation.Token));
    }
}
