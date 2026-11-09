using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using MediaSet.Api.Features.Plex.Services;
using MediaSet.Api.Infrastructure.DataAccess;
using MediaSet.Api.Infrastructure.Lookup.Clients.Tmdb;
using MediaSet.Api.Infrastructure.Lookup.Models;
using MediaSet.Api.Infrastructure.Plex;
using MediaSet.Api.Shared.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using Moq;
using NUnit.Framework;

namespace MediaSet.Api.Tests.Features.Plex.Services;

[TestFixture]
public class ExternalIdBackfillServiceTests
{
    private sealed class Harness
    {
        public List<Movie> Movies { get; }
        public Mock<IEntityService<Movie>> Service { get; } = new();
        public Mock<ITmdbClient> Tmdb { get; } = new();
        public FakeTimeProvider Time { get; } = new(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
        public PlexBackfillConfiguration Config { get; } = new() { RequestsPerMinute = 30, BatchSize = 50 };
        public List<(string Title, DateTimeOffset At)> Calls { get; } = [];

        public Harness(params Movie[] movies)
        {
            Movies = [.. movies];
            Service.Setup(s => s.GetListAsync(It.IsAny<CancellationToken>())).ReturnsAsync(() => [.. Movies.Select(Copy)]);
            Service.Setup(s => s.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((string id, CancellationToken _) => Movies.Where(m => m.Id == id).Select(Copy).FirstOrDefault());
            Service.Setup(s => s.UpdateAsync(It.IsAny<string>(), It.IsAny<Movie>(), It.IsAny<CancellationToken>()))
                .Callback((string id, Movie m, CancellationToken _) => Movies[Movies.FindIndex(x => x.Id == id)] = m)
                .ReturnsAsync(default(ReplaceOneResult)!);
            Tmdb.Setup(t => t.SearchMovieAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Callback((string title, CancellationToken _) => Calls.Add((title, Time.GetUtcNow())))
                .ReturnsAsync(Results());
        }

        public ExternalIdBackfillService Build(bool tmdbConfigured = true) =>
            new(Service.Object, Options.Create(new PlexConfiguration { Backfill = Config }), NullLogger<ExternalIdBackfillService>.Instance,
                tmdbConfigured ? Tmdb.Object : null, Time);

        public void Returns(string title, params TmdbMovieSearchResult[] results) =>
            Tmdb.Setup(t => t.SearchMovieAsync(title, It.IsAny<CancellationToken>()))
                .Callback((string t, CancellationToken _) => Calls.Add((t, Time.GetUtcNow())))
                .ReturnsAsync(Results(results));

        private static Movie Copy(Movie m) => new() { Id = m.Id, Title = m.Title, ReleaseDate = m.ReleaseDate, ExternalId = m.ExternalId, Format = m.Format, Plot = m.Plot };
    }

    private static TmdbSearchResponse Results(params TmdbMovieSearchResult[] results) => new(1, [.. results], 1, results.Length);

    private static TmdbMovieSearchResult Hit(int id, string title, string? date) => new(id, title, date, null);

    private static Movie NoId(string id, string title, string? date = "1999-03-31") => new() { Id = id, Title = title, ReleaseDate = date ?? string.Empty };

    [Test]
    public async Task RunAsync_SingleResultWithSameTitleAndYear_SavesTmdbId()
    {
        var h = new Harness(NoId("a", "The Matrix"));
        h.Returns("The Matrix", Hit(603, "the matrix ", "1999-03-31"));

        var result = await h.Build().RunAsync(CancellationToken.None);

        Assert.That(h.Movies[0].ExternalId, Is.EqualTo("tmdbid:603"));
        Assert.That(result, Is.EqualTo(new ExternalIdBackfillResult(1, 0)));
    }

    [Test]
    public async Task RunAsync_ConfidentMatch_ChangesOnlyExternalId()
    {
        var movie = NoId("a", "The Matrix");
        movie.Format = "Blu-ray";
        movie.Plot = "Plot";
        var h = new Harness(movie);
        h.Returns("The Matrix", Hit(603, "The Matrix", "1999-03-31"));

        await h.Build().RunAsync(CancellationToken.None);

        Assert.That(h.Movies[0].Format, Is.EqualTo("Blu-ray"));
        Assert.That(h.Movies[0].Plot, Is.EqualTo("Plot"));
        Assert.That(h.Movies[0].Title, Is.EqualTo("The Matrix"));
    }

    [Test]
    public async Task RunAsync_LoneResultWithDifferentTitle_LeavesBlankAndCountsSkipped()
    {
        var h = new Harness(NoId("a", "The Matrix"));
        h.Returns("The Matrix", Hit(604, "The Matrix Reloaded", "1999-03-31"));

        var result = await h.Build().RunAsync(CancellationToken.None);

        Assert.That(h.Movies[0].ExternalId, Is.Null);
        Assert.That(result, Is.EqualTo(new ExternalIdBackfillResult(0, 1)));
    }

    [Test]
    public async Task RunAsync_LoneResultWithDifferentYear_LeavesBlankAndCountsSkipped()
    {
        var h = new Harness(NoId("a", "The Matrix"));
        h.Returns("The Matrix", Hit(603, "The Matrix", "2021-12-22"));

        var result = await h.Build().RunAsync(CancellationToken.None);

        Assert.That(h.Movies[0].ExternalId, Is.Null);
        Assert.That(result, Is.EqualTo(new ExternalIdBackfillResult(0, 1)));
    }

    [Test]
    public async Task RunAsync_SeveralResultsMatchTitleAndYear_IsAmbiguousAndLeavesBlank()
    {
        var h = new Harness(NoId("a", "Dune", "1984-12-14"));
        h.Returns("Dune", Hit(1, "Dune", "1984-12-14"), Hit(2, "Dune", "1984-01-01"));

        var result = await h.Build().RunAsync(CancellationToken.None);

        Assert.That(h.Movies[0].ExternalId, Is.Null);
        Assert.That(result, Is.EqualTo(new ExternalIdBackfillResult(0, 1)));
    }

    [Test]
    public async Task RunAsync_OneExactAmongSeveralResults_SavesTheExactOne()
    {
        var h = new Harness(NoId("a", "Dune", "1984-12-14"));
        h.Returns("Dune", Hit(1, "Dune", "2021-09-15"), Hit(2, "Dune", "1984-12-14"), Hit(3, "Dune: Part Two", "1984-12-14"));

        await h.Build().RunAsync(CancellationToken.None);

        Assert.That(h.Movies[0].ExternalId, Is.EqualTo("tmdbid:2"));
    }

    [Test]
    public async Task RunAsync_NoResults_LeavesBlankAndCountsSkipped()
    {
        var h = new Harness(NoId("a", "Nothing"));
        h.Returns("Nothing");

        var result = await h.Build().RunAsync(CancellationToken.None);

        Assert.That(h.Movies[0].ExternalId, Is.Null);
        Assert.That(result, Is.EqualTo(new ExternalIdBackfillResult(0, 1)));
    }

    [Test]
    public async Task RunAsync_MovieWithoutParseableYear_IsSkippedWithoutSearching()
    {
        var h = new Harness(NoId("a", "The Matrix", null));

        var result = await h.Build().RunAsync(CancellationToken.None);

        Assert.That(result, Is.EqualTo(new ExternalIdBackfillResult(0, 1)));
        h.Tmdb.Verify(t => t.SearchMovieAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task RunAsync_MovieWithExistingExternalId_IsNeverSearchedOrChanged()
    {
        var handSet = NoId("a", "The Matrix");
        handSet.ExternalId = "imdbid:tt0133093";
        var h = new Harness(handSet, NoId("b", "Other"));
        h.Returns("The Matrix", Hit(603, "The Matrix", "1999-03-31"));
        h.Returns("Other", Hit(9, "Other", "1999-03-31"));

        await h.Build().RunAsync(CancellationToken.None);

        Assert.That(h.Movies[0].ExternalId, Is.EqualTo("imdbid:tt0133093"));
        Assert.That(h.Calls.Select(c => c.Title), Is.EqualTo(new List<string> { "Other" }));
        h.Service.Verify(s => s.UpdateAsync("a", It.IsAny<Movie>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task RunAsync_ExternalIdSetWhileSearching_IsNotOverwritten()
    {
        var h = new Harness(NoId("a", "The Matrix"));
        h.Tmdb.Setup(t => t.SearchMovieAsync("The Matrix", It.IsAny<CancellationToken>()))
            .Callback(() => h.Movies[0].ExternalId = "imdbid:tt0133093")
            .ReturnsAsync(Results(Hit(603, "The Matrix", "1999-03-31")));

        await h.Build().RunAsync(CancellationToken.None);

        Assert.That(h.Movies[0].ExternalId, Is.EqualTo("imdbid:tt0133093"));
    }

    [Test]
    public async Task RunAsync_Requests_AreSpacedByTheConfiguredRate()
    {
        var h = new Harness(NoId("a", "A"), NoId("b", "B"), NoId("c", "C"));
        h.Config.RequestsPerMinute = 30; // one every 2 seconds
        var start = h.Time.GetUtcNow();

        var run = h.Build().RunAsync(CancellationToken.None);
        await h.Time.WaitForTimerAsync();
        Assert.That(h.Calls, Has.Count.EqualTo(1));
        h.Time.Advance(TimeSpan.FromSeconds(1.9));
        await Task.Delay(50);
        Assert.That(h.Calls, Has.Count.EqualTo(1), "second request must wait the full interval");
        h.Time.Advance(TimeSpan.FromSeconds(0.1));
        await h.Time.WaitForTimerAsync();
        Assert.That(h.Calls, Has.Count.EqualTo(2));
        h.Time.Advance(TimeSpan.FromSeconds(2));
        await run;

        Assert.That(h.Calls.Select(c => c.At - start), Is.EqualTo(new List<TimeSpan> { TimeSpan.Zero, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(4) }));
    }

    [Test]
    public async Task RunAsync_MoreCandidatesThanBatchSize_SearchesOnlyBatchSize()
    {
        var h = new Harness([.. Enumerable.Range(1, 5).Select(i => NoId($"m{i}", $"Movie {i}"))]);
        h.Config.BatchSize = 2;
        h.Config.RequestsPerMinute = 6000;

        var run = h.Build().RunAsync(CancellationToken.None);
        while (!run.IsCompleted)
        {
            h.Time.Advance(TimeSpan.FromSeconds(1));
            await Task.Delay(5);
        }
        var result = await run;

        Assert.That(h.Calls.Select(c => c.Title), Is.EqualTo(new List<string> { "Movie 1", "Movie 2" }));
        Assert.That(result.Skipped, Is.EqualTo(2));
    }

    [Test]
    public async Task RunAsync_TmdbNotConfigured_SkipsWithoutReadingMovies()
    {
        var h = new Harness(NoId("a", "The Matrix"));

        var result = await h.Build(tmdbConfigured: false).RunAsync(CancellationToken.None);

        Assert.That(result, Is.EqualTo(new ExternalIdBackfillResult(0, 0)));
        h.Service.Verify(s => s.GetListAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task RunAsync_TmdbErrorOnOneMovie_CountsSkippedAndContinues()
    {
        var h = new Harness(NoId("a", "Bad"), NoId("b", "Good"));
        h.Config.RequestsPerMinute = 6000;
        h.Tmdb.Setup(t => t.SearchMovieAsync("Bad", It.IsAny<CancellationToken>())).ThrowsAsync(new HttpRequestException("boom", null, HttpStatusCode.InternalServerError));
        h.Returns("Good", Hit(7, "Good", "1999-03-31"));

        var run = h.Build().RunAsync(CancellationToken.None);
        while (!run.IsCompleted)
        {
            h.Time.Advance(TimeSpan.FromSeconds(1));
            await Task.Delay(5);
        }
        var result = await run;

        Assert.That(result, Is.EqualTo(new ExternalIdBackfillResult(1, 1)));
        Assert.That(h.Movies[1].ExternalId, Is.EqualTo("tmdbid:7"));
    }

    [Test]
    public async Task RunAsync_RateLimited_StopsEarlyKeepingEarlierResults()
    {
        var h = new Harness(NoId("a", "First"), NoId("b", "Limited"), NoId("c", "Never"));
        h.Config.RequestsPerMinute = 6000;
        h.Returns("First", Hit(1, "First", "1999-03-31"));
        h.Tmdb.Setup(t => t.SearchMovieAsync("Limited", It.IsAny<CancellationToken>())).ThrowsAsync(new HttpRequestException("limit", null, HttpStatusCode.TooManyRequests));

        var run = h.Build().RunAsync(CancellationToken.None);
        while (!run.IsCompleted)
        {
            h.Time.Advance(TimeSpan.FromSeconds(1));
            await Task.Delay(5);
        }
        var result = await run;

        Assert.That(result, Is.EqualTo(new ExternalIdBackfillResult(1, 0)));
        h.Tmdb.Verify(t => t.SearchMovieAsync("Never", It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public void RunAsync_CancelledToken_PropagatesCancellation()
    {
        var h = new Harness(NoId("a", "The Matrix"));
        using var cts = new CancellationTokenSource();
        h.Tmdb.Setup(t => t.SearchMovieAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(() => { cts.Cancel(); throw new OperationCanceledException(cts.Token); });

        Assert.That(() => h.Build().RunAsync(cts.Token), Throws.InstanceOf<OperationCanceledException>());
    }
}
