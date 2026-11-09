using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediaSet.Api.Features.Plex.Models;
using MediaSet.Api.Features.Plex.Services;
using MediaSet.Api.Infrastructure.DataAccess;
using MediaSet.Api.Infrastructure.Database;
using MediaSet.Api.Infrastructure.Lookup.Clients.Tmdb;
using MediaSet.Api.Infrastructure.Lookup.Models;
using MediaSet.Api.Shared.Models;
using MediaSet.Api.Infrastructure.Plex;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using Moq;
using NUnit.Framework;

namespace MediaSet.Api.Tests.Features.Plex.Services;

[TestFixture]
public class PlexSyncServiceTests
{
    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (!condition())
        {
            if (DateTime.UtcNow > deadline)
            {
                throw new TimeoutException("Condition was not met in time.");
            }
            await Task.Delay(10);
        }
    }

    private sealed class Harness
    {
        public InMemoryPlexSyncRunStore Store { get; } = new();
        public CancellationTokenSource Shutdown { get; } = new();
        public Mock<IPlexClient> Client { get; } = new();
        public InMemoryPlexItemStore Items { get; } = new();
        public Mock<IEntityService<Movie>> Movies { get; } = InMemoryPlexItemStore.MovieService();
        public Mock<ITmdbClient> Tmdb { get; } = new();
        public PlexSyncService Service { get; }

        /// <param name="tmdbConfigured">When false, ITmdbClient is not registered, as when the TMDB section is absent.</param>
        public Harness(bool tmdbConfigured = false)
        {
            var lifetime = new Mock<IHostApplicationLifetime>();
            lifetime.SetupGet(l => l.ApplicationStopping).Returns(Shutdown.Token);
            var collection = new ServiceCollection().AddSingleton(Client.Object).AddSingleton<IPlexItemStore>(Items).AddScoped(_ => Movies.Object)
                .AddSingleton(Options.Create(new PlexConfiguration { Backfill = new PlexBackfillConfiguration { RequestsPerMinute = 6000 } }))
                .AddLogging()
                .AddSingleton<IExternalIdBackfillStore>(new InMemoryExternalIdBackfillStore())
                .AddScoped<IExternalIdBackfillService, ExternalIdBackfillService>();
            if (tmdbConfigured)
            {
                collection.AddSingleton(Tmdb.Object);
            }
            var services = collection.BuildServiceProvider();
            Service = new PlexSyncService(
                services,
                Store,
                lifetime.Object,
                Options.Create(new PlexConfiguration { Token = "secret-token" }),
                NullLogger<PlexSyncService>.Instance);
        }
    }

    [Test]
    public async Task StartAsync_TwoEntityTypes_RunOneAtATimeBehindSharedLock()
    {
        var h = new Harness();
        var gate = new TaskCompletionSource();
        var concurrent = 0;
        var maxConcurrent = 0;
        h.Client.Setup(c => c.GetLibrariesForEntityTypeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(async (string _, CancellationToken ct) =>
            {
                var now = Interlocked.Increment(ref concurrent);
                maxConcurrent = Math.Max(maxConcurrent, now);
                await gate.Task.WaitAsync(ct);
                Interlocked.Decrement(ref concurrent);
                return (IReadOnlyList<PlexLibrary>)[];
            });

        var first = await h.Service.StartAsync("movies", PlexSyncTriggers.Manual, CancellationToken.None);
        var second = await h.Service.StartAsync("shows", PlexSyncTriggers.Manual, CancellationToken.None);
        await WaitUntilAsync(() => concurrent == 1);
        var whileFirstRuns = h.Store.Runs.Select(r => r.State).ToList();
        gate.SetResult();
        await WaitUntilAsync(() => h.Store.Runs.All(r => r.State == PlexSyncRunStates.Succeeded));

        Assert.That(first.Started && second.Started, Is.True);
        Assert.That(whileFirstRuns, Is.EqualTo(new List<string> { PlexSyncRunStates.Running, PlexSyncRunStates.Queued }));
        Assert.That(maxConcurrent, Is.EqualTo(1));
    }

    [Test]
    public async Task StartAsync_ApplicationStopping_CancelsPlexCallsAndRecordsInterrupted()
    {
        var h = new Harness();
        CancellationToken seen = default;
        h.Client.Setup(c => c.GetLibrariesForEntityTypeAsync("movies", It.IsAny<CancellationToken>()))
            .Returns(async (string _, CancellationToken ct) =>
            {
                seen = ct;
                await Task.Delay(Timeout.Infinite, ct);
                return (IReadOnlyList<PlexLibrary>)[];
            });

        await h.Service.StartAsync("movies", PlexSyncTriggers.Manual, CancellationToken.None);
        await WaitUntilAsync(() => seen.CanBeCanceled);
        h.Shutdown.Cancel();
        await WaitUntilAsync(() => h.Store.Runs.Single().State == PlexSyncRunStates.Interrupted);

        Assert.That(h.Store.Runs.Single().State, Is.EqualTo(PlexSyncRunStates.Interrupted));
        Assert.That(seen.IsCancellationRequested, Is.True);
    }

    [Test]
    public async Task StartAsync_ApplicationStoppingWhileQueued_RecordsInterruptedWithoutCallingPlex()
    {
        var h = new Harness();
        h.Client.Setup(c => c.GetLibrariesForEntityTypeAsync("movies", It.IsAny<CancellationToken>()))
            .Returns(async (string _, CancellationToken ct) =>
            {
                await Task.Delay(Timeout.Infinite, ct);
                return (IReadOnlyList<PlexLibrary>)[];
            });
        await h.Service.StartAsync("movies", PlexSyncTriggers.Manual, CancellationToken.None);
        await h.Service.StartAsync("shows", PlexSyncTriggers.Manual, CancellationToken.None);

        h.Shutdown.Cancel();
        await WaitUntilAsync(() => h.Store.Runs.All(r => r.State == PlexSyncRunStates.Interrupted));

        h.Client.Verify(c => c.GetLibrariesForEntityTypeAsync("shows", It.IsAny<CancellationToken>()), Times.Never);
        Assert.That(h.Store.Runs.Single(r => r.EntityType == "shows").EndedAt, Is.Not.Null);
    }

    [Test]
    public async Task StartAsync_SameTypeWhileInsertIsSlow_ReturnsPersistedCurrentRun()
    {
        var h = new Harness();
        h.Store.BeforeInsert = () => Task.Delay(50);
        var release = new TaskCompletionSource();
        h.Client.Setup(c => c.GetLibrariesForEntityTypeAsync("movies", It.IsAny<CancellationToken>()))
            .Returns(async (string _, CancellationToken ct) =>
            {
                await release.Task.WaitAsync(ct);
                return (IReadOnlyList<PlexLibrary>)[];
            });

        var results = await Task.WhenAll(Enumerable.Range(0, 8)
            .Select(_ => Task.Run(() => h.Service.StartAsync("movies", PlexSyncTriggers.Manual, CancellationToken.None))));
        release.SetResult();
        await WaitUntilAsync(() => h.Store.Runs.Single().State == PlexSyncRunStates.Succeeded);

        Assert.That(results.Count(r => r.Started), Is.EqualTo(1));
        Assert.That(results.Select(r => r.Run.Id), Has.All.Not.Null.And.All.EqualTo(h.Store.Runs.Single().Id));
    }

    [Test]
    public async Task StartAsync_InsertFails_ReleasesLockSoLaterStartSucceeds()
    {
        var h = new Harness();
        h.Client.Setup(c => c.GetLibrariesForEntityTypeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<PlexLibrary>)[]);
        h.Store.InsertFailure = new InvalidOperationException("db down");

        Assert.That(async () => await h.Service.StartAsync("movies", PlexSyncTriggers.Manual, CancellationToken.None), Throws.InvalidOperationException);
        h.Store.InsertFailure = null;
        var result = await h.Service.StartAsync("shows", PlexSyncTriggers.Manual, CancellationToken.None);
        await WaitUntilAsync(() => h.Store.Runs.Single().State == PlexSyncRunStates.Succeeded);

        Assert.That(result.Started, Is.True);
        Assert.That(result.Run.State, Is.EqualTo(PlexSyncRunStates.Running));
    }

    [Test]
    public async Task StopAsync_AlreadyCancelledToken_ReturnsWithoutWaiting()
    {
        var h = new Harness();
        h.Client.Setup(c => c.GetLibrariesForEntityTypeAsync("movies", It.IsAny<CancellationToken>()))
            .Returns(async (string _, CancellationToken ct) =>
            {
                await Task.Delay(Timeout.Infinite, ct);
                return (IReadOnlyList<PlexLibrary>)[];
            });
        await h.Service.StartAsync("movies", PlexSyncTriggers.Manual, CancellationToken.None);
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();

        await h.Service.StopAsync(cancelled.Token).WaitAsync(TimeSpan.FromSeconds(5));

        h.Shutdown.Cancel();
        await WaitUntilAsync(() => h.Store.Runs.Single().State == PlexSyncRunStates.Interrupted);
    }

    [Test]
    public async Task StartAsync_AfterStopAsync_IsRejectedAndCreatesNoRun()
    {
        var h = new Harness();
        await h.Service.StopAsync(CancellationToken.None);

        Assert.That(async () => await h.Service.StartAsync("movies", PlexSyncTriggers.Manual, CancellationToken.None), Throws.InvalidOperationException);
        Assert.That(h.Store.Runs, Is.Empty);
    }

    [Test]
    public async Task StopAsync_StartMidInsert_WaitsForThatRunToo()
    {
        var h = new Harness();
        var insertGate = new TaskCompletionSource();
        h.Store.BeforeInsert = () => insertGate.Task;
        h.Client.Setup(c => c.GetLibrariesForEntityTypeAsync("movies", It.IsAny<CancellationToken>()))
            .Returns(async (string _, CancellationToken ct) =>
            {
                await Task.Delay(Timeout.Infinite, ct);
                return (IReadOnlyList<PlexLibrary>)[];
            });
        var start = h.Service.StartAsync("movies", PlexSyncTriggers.Manual, CancellationToken.None);
        h.Shutdown.Cancel();

        var stop = h.Service.StopAsync(CancellationToken.None);
        insertGate.SetResult();
        await start;
        await stop;

        Assert.That(h.Store.Runs.Single().State, Is.EqualTo(PlexSyncRunStates.Interrupted));
    }

    [Test]
    public async Task StopAsync_RunInProgress_WaitsForOutcomeToBeRecorded()
    {
        var h = new Harness();
        h.Client.Setup(c => c.GetLibrariesForEntityTypeAsync("movies", It.IsAny<CancellationToken>()))
            .Returns(async (string _, CancellationToken ct) =>
            {
                await Task.Delay(Timeout.Infinite, ct);
                return (IReadOnlyList<PlexLibrary>)[];
            });
        await h.Service.StartAsync("movies", PlexSyncTriggers.Manual, CancellationToken.None);

        h.Shutdown.Cancel();
        await h.Service.StopAsync(CancellationToken.None);

        Assert.That(h.Store.Runs.Single().State, Is.EqualTo(PlexSyncRunStates.Interrupted));
    }

    [Test]
    public async Task StartAsync_ClientMessageContainsToken_StoredMessageIsRedacted()
    {
        var h = new Harness();
        h.Client.Setup(c => c.GetLibrariesForEntityTypeAsync("movies", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new PlexClientException(null, "Plex said secret-token"));

        await h.Service.StartAsync("movies", PlexSyncTriggers.Manual, CancellationToken.None);
        await WaitUntilAsync(() => h.Store.Runs.Single().State == PlexSyncRunStates.Failed);

        Assert.That(h.Store.Runs.Single().State, Is.EqualTo(PlexSyncRunStates.Failed));
        Assert.That(h.Store.Runs.Single().Message, Does.Not.Contain("secret-token"));
    }

    private static PlexItem Item(string key, PlexGuids guids, params PlexVersion[] versions) =>
        new(key, "movie", $"Item {key}", 2000, "1", "Movies", DateTimeOffset.FromUnixTimeSeconds(1700000000), $"link-{key}", guids, versions,
            2, DateTimeOffset.FromUnixTimeSeconds(1710000000));

    private static PlexVersion Version(string res, bool hdr = false, long size = 1, string path = "/p/f.mkv") =>
        new(res, hdr, "hevc", [new PlexAudioTrack("ac3", 6, "eng")], [new PlexSubtitle("eng", false), new PlexSubtitle("fra", true), new PlexSubtitle("eng", true)],
            TimeSpan.FromMinutes(120), "mkv", path, size);

    private static Movie MovieWith(string id, string? externalId) => new() { Id = id, Title = id, ExternalId = externalId };

    private static void Serve(Harness h, params PlexItem[] items)
    {
        var library = new PlexLibrary("1", "Movies", "movie");
        h.Client.Setup(c => c.GetLibrariesForEntityTypeAsync("movies", It.IsAny<CancellationToken>()))
            .ReturnsAsync([library]);
        h.Client.Setup(c => c.GetItemsAsync(library, null, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(items);
    }

    private static async Task<PlexSyncRun> RunAsync(Harness h)
    {
        await h.Service.StartAsync("movies", PlexSyncTriggers.Manual, CancellationToken.None);
        await WaitUntilAsync(() => h.Store.Runs.Count == 1 && !PlexSyncRunStates.IsInProgress(h.Store.Runs[0].State));
        return h.Store.Runs.Single();
    }

    private static void SetMovies(Harness h, params Movie[] movies) =>
        h.Movies.Setup(s => s.GetListAsync(It.IsAny<CancellationToken>())).ReturnsAsync(movies.ToList());

    [Test]
    public async Task Sync_TmdbNotConfigured_SkipsBackfillAndSyncProceeds()
    {
        var h = new Harness(tmdbConfigured: false);
        Serve(h, Item("10", new PlexGuids("603", null, null), Version("1080")));
        SetMovies(h, MovieWith("a", "tmdbid:603"), new Movie { Id = "b", Title = "No Id", ReleaseDate = "1999-03-31" });

        var run = await RunAsync(h);

        Assert.That(run.State, Is.EqualTo(PlexSyncRunStates.Succeeded));
        Assert.That(run.MatchedCount, Is.EqualTo(1));
        Assert.That(run.ExternalIdsBackfilled, Is.EqualTo(0));
        Assert.That(run.BackfillSkipped, Is.EqualTo(0));
        h.Movies.Verify(s => s.UpdateAsync(It.IsAny<string>(), It.IsAny<Movie>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Sync_ShutdownDuringBackfill_EndsInterruptedWithoutReadingPlex()
    {
        var h = new Harness(tmdbConfigured: true);
        SetMovies(h, new Movie { Id = "a", Title = "Slow", ReleaseDate = "1999-03-31" });
        var started = false;
        h.Tmdb.Setup(t => t.SearchMovieAsync("Slow", It.IsAny<CancellationToken>()))
            .Returns(async (string _, CancellationToken ct) =>
            {
                started = true;
                await Task.Delay(Timeout.Infinite, ct);
                return null;
            });

        await h.Service.StartAsync("movies", PlexSyncTriggers.Manual, CancellationToken.None);
        await WaitUntilAsync(() => started);
        h.Shutdown.Cancel();
        await WaitUntilAsync(() => h.Store.Runs.Single().State == PlexSyncRunStates.Interrupted);

        h.Client.Verify(c => c.GetLibrariesForEntityTypeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Sync_BackfilledMovie_MatchesInSameRunAndRecordsCounts()
    {
        var h = new Harness(tmdbConfigured: true);
        var movies = new List<Movie>
        {
            new() { Id = "a", Title = "The Matrix", ReleaseDate = "1999-03-31" },
            new() { Id = "b", Title = "Unknown", ReleaseDate = "2001-01-01" },
            MovieWith("c", "tmdbid:1")
        };
        h.Movies.Setup(s => s.GetListAsync(It.IsAny<CancellationToken>())).ReturnsAsync(() => movies.ToList());
        h.Movies.Setup(s => s.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((string id, CancellationToken _) => movies.First(m => m.Id == id));
        h.Movies.Setup(s => s.UpdateAsync("a", It.IsAny<Movie>(), It.IsAny<CancellationToken>()))
            .Callback((string _, Movie m, CancellationToken _) => movies[0] = m)
            .ReturnsAsync(default(ReplaceOneResult)!);
        h.Tmdb.Setup(t => t.SearchMovieAsync("The Matrix", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TmdbSearchResponse(1, [new TmdbMovieSearchResult(603, "The Matrix", "1999-03-31", null)], 1, 1));
        h.Tmdb.Setup(t => t.SearchMovieAsync("Unknown", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TmdbSearchResponse(1, [], 0, 0));
        Serve(h, Item("10", new PlexGuids("603", null, null), Version("1080")));

        var run = await RunAsync(h);

        Assert.That(run.State, Is.EqualTo(PlexSyncRunStates.Succeeded));
        Assert.That(run.ExternalIdsBackfilled, Is.EqualTo(1));
        Assert.That(run.BackfillSkipped, Is.EqualTo(1));
        Assert.That(h.Items.Items["a"].PlexItemId, Is.EqualTo("10"));
    }

    [Test]
    public async Task Sync_MovieWithTmdbImdbOrTvdbExternalId_MatchesAndStoresRatingKey()
    {
        var h = new Harness();
        Serve(h,
            Item("10", new PlexGuids("603", null, null), Version("1080")),
            Item("11", new PlexGuids(null, "tt0133093", null), Version("1080")),
            Item("12", new PlexGuids(null, null, "169"), Version("1080")));
        SetMovies(h, MovieWith("a", "tmdbid:603"), MovieWith("b", "imdbid:tt0133093"), MovieWith("c", "tvdbid:169"));

        var run = await RunAsync(h);

        Assert.That(run.State, Is.EqualTo(PlexSyncRunStates.Succeeded));
        Assert.That(h.Items.Items["a"].PlexItemId, Is.EqualTo("10"));
        Assert.That(h.Items.Items["b"].PlexItemId, Is.EqualTo("11"));
        Assert.That(h.Items.Items["c"].PlexItemId, Is.EqualTo("12"));
    }

    [Test]
    public async Task Sync_StoredPlexItemId_WinsOverExternalIdMatch()
    {
        var h = new Harness();
        h.Items.UpsertAsync(new PlexItemData { Id = "a", PlexItemId = "20", OnPlex = true }, CancellationToken.None).Wait();
        Serve(h,
            Item("10", new PlexGuids("603", null, null), Version("1080")),
            Item("20", new PlexGuids("999", null, null), Version("720")));
        SetMovies(h, MovieWith("a", "tmdbid:603"));

        await RunAsync(h);

        Assert.That(h.Items.Items["a"].PlexItemId, Is.EqualTo("20"));
        Assert.That(h.Items.Items["a"].Resolution, Is.EqualTo("720"));
    }

    [Test]
    public async Task Sync_ManualLinkWithConflictingExternalId_KeepsManualLinkAndRefreshesItsOwnItem()
    {
        var h = new Harness();
        await h.Items.UpsertAsync(new PlexItemData { Id = "a", PlexItemId = "20", OnPlex = true, LinkSource = PlexLinkSources.Manual }, CancellationToken.None);
        Serve(h,
            Item("10", new PlexGuids("603", null, null), Version("1080")),
            Item("20", new PlexGuids("999", null, null), Version("720")));
        SetMovies(h, MovieWith("a", "tmdbid:603"));

        await RunAsync(h);

        Assert.That(h.Items.Items["a"].PlexItemId, Is.EqualTo("20"));
        Assert.That(h.Items.Items["a"].Resolution, Is.EqualTo("720"));
        Assert.That(h.Items.Items["a"].LinkSource, Is.EqualTo(PlexLinkSources.Manual));
    }

    [Test]
    public async Task Sync_ManualLinkItemGoneFromPlex_IsFlaggedNotOnPlexAndNeverReplacedOrDeleted()
    {
        var h = new Harness();
        await h.Items.UpsertAsync(new PlexItemData { Id = "a", PlexItemId = "20", OnPlex = true, LinkSource = PlexLinkSources.Manual, Resolution = "720" }, CancellationToken.None);
        Serve(h, Item("10", new PlexGuids("603", null, null), Version("1080")));
        SetMovies(h, MovieWith("a", "tmdbid:603"));

        await RunAsync(h);

        Assert.That(h.Items.Items["a"].PlexItemId, Is.EqualTo("20"));
        Assert.That(h.Items.Items["a"].OnPlex, Is.False);
        Assert.That(h.Items.Items["a"].LinkSource, Is.EqualTo(PlexLinkSources.Manual));
        Assert.That(h.Items.Items["a"].Resolution, Is.EqualTo("720"));
    }

    [Test]
    public async Task Sync_ManualLinkOutsideScannedLibraries_IsRefreshedByRatingKey()
    {
        var h = new Harness();
        await h.Items.UpsertAsync(new PlexItemData { Id = "a", PlexItemId = "30", OnPlex = true, LinkSource = PlexLinkSources.Manual }, CancellationToken.None);
        Serve(h, Item("10", new PlexGuids("603", null, null), Version("1080")));
        h.Client.Setup(c => c.GetItemAsync("30", true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Item("30", new PlexGuids(null, null, null), Version("2160")));
        SetMovies(h, MovieWith("a", "tmdbid:603"));

        await RunAsync(h);

        Assert.That(h.Items.Items["a"].PlexItemId, Is.EqualTo("30"));
        Assert.That(h.Items.Items["a"].Resolution, Is.EqualTo("2160"));
    }

    [Test]
    public async Task Sync_ManualLinkMadeAfterStoredReadBeforeAutoWrite_IsNotReplaced()
    {
        var h = new Harness();
        Serve(h, Item("10", new PlexGuids("603", null, null), Version("1080")));
        SetMovies(h, MovieWith("a", "tmdbid:603"));
        var gets = 0;
        // The first read finds nothing; the user links the movie right after it, before the auto match is written
        h.Items.AfterGet = async id =>
        {
            if (id == "a" && ++gets == 1)
            {
                await h.Items.UpsertAsync(new PlexItemData { Id = "a", PlexItemId = "20", OnPlex = true, LinkSource = PlexLinkSources.Manual, Resolution = "720" }, CancellationToken.None);
            }
        };

        var run = await RunAsync(h);

        Assert.That(gets, Is.EqualTo(2));
        Assert.That(h.Items.Items["a"].PlexItemId, Is.EqualTo("20"));
        Assert.That(h.Items.Items["a"].LinkSource, Is.EqualTo(PlexLinkSources.Manual));
        Assert.That(h.Items.Items["a"].Resolution, Is.EqualTo("720"));
        Assert.That(run.MatchedCount, Is.EqualTo(1));
    }

    [Test]
    public async Task Sync_ManualLinkOfDeletedMovie_IsPurged()
    {
        var h = new Harness();
        await h.Items.UpsertAsync(new PlexItemData { Id = "gone", PlexItemId = "20", OnPlex = true, LinkSource = PlexLinkSources.Manual }, CancellationToken.None);
        Serve(h);
        SetMovies(h, MovieWith("a", null));

        await RunAsync(h);

        Assert.That(h.Items.Items, Does.Not.ContainKey("gone"));
    }

    [Test]
    public async Task RefreshItemAsync_LinkedMovie_ReloadsFromItsRatingKeyIgnoringExternalId()
    {
        var h = new Harness();
        await h.Items.UpsertAsync(new PlexItemData { Id = "a", PlexItemId = "20", OnPlex = true, LinkSource = PlexLinkSources.Manual }, CancellationToken.None);
        h.Client.Setup(c => c.GetItemAsync("20", true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Item("20", new PlexGuids("999", null, null), Version("720")));

        var result = await h.Service.RefreshItemAsync("movies", "a", CancellationToken.None);

        Assert.That(result.Status, Is.EqualTo(PlexRefreshStatus.Refreshed));
        Assert.That(result.Data!.Resolution, Is.EqualTo("720"));
        Assert.That(h.Items.Items["a"].LinkSource, Is.EqualTo(PlexLinkSources.Manual));
        Assert.That(h.Store.Runs, Is.Empty);
    }

    [Test]
    public async Task RefreshItemAsync_Unlinked_ReturnsNotLinkedWithoutCallingPlex()
    {
        var h = new Harness();

        var result = await h.Service.RefreshItemAsync("movies", "a", CancellationToken.None);

        Assert.That(result.Status, Is.EqualTo(PlexRefreshStatus.NotLinked));
        h.Client.Verify(c => c.GetItemAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task RefreshItemAsync_PlexItemGone_FlagsNotOnPlexAndKeepsData()
    {
        var h = new Harness();
        await h.Items.UpsertAsync(new PlexItemData { Id = "a", PlexItemId = "20", OnPlex = true, Resolution = "720" }, CancellationToken.None);

        var result = await h.Service.RefreshItemAsync("movies", "a", CancellationToken.None);

        Assert.That(result.Status, Is.EqualTo(PlexRefreshStatus.PlexItemMissing));
        Assert.That(h.Items.Items["a"].OnPlex, Is.False);
        Assert.That(h.Items.Items["a"].Resolution, Is.EqualTo("720"));
        Assert.That(h.Items.Items["a"].MissCount, Is.EqualTo(0));
    }

    [Test]
    public async Task RefreshItemAsync_SyncRunning_ReturnsBusyWithRunImmediately()
    {
        var h = new Harness();
        await h.Items.UpsertAsync(new PlexItemData { Id = "a", PlexItemId = "20", OnPlex = true }, CancellationToken.None);
        var release = new TaskCompletionSource();
        h.Client.Setup(c => c.GetLibrariesForEntityTypeAsync("movies", It.IsAny<CancellationToken>()))
            .Returns(async (string _, CancellationToken ct) =>
            {
                await release.Task.WaitAsync(ct);
                return (IReadOnlyList<PlexLibrary>)[];
            });
        await h.Service.StartAsync("movies", PlexSyncTriggers.Manual, CancellationToken.None);

        var result = await h.Service.RefreshItemAsync("movies", "a", CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5));
        release.SetResult();
        await WaitUntilAsync(() => h.Store.Runs.Single().State == PlexSyncRunStates.Succeeded);

        Assert.That(result.Status, Is.EqualTo(PlexRefreshStatus.Busy));
        Assert.That(result.Run!.State, Is.EqualTo(PlexSyncRunStates.Running));
        h.Client.Verify(c => c.GetItemAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task StartAsync_AfterRefreshItemAsync_LockIsReleasedSoSyncRuns()
    {
        var h = new Harness();
        await h.Service.RefreshItemAsync("movies", "a", CancellationToken.None);
        Serve(h);
        SetMovies(h);

        var result = await h.Service.StartAsync("movies", PlexSyncTriggers.Manual, CancellationToken.None);

        Assert.That(result.Run.State, Is.EqualTo(PlexSyncRunStates.Running));
        await WaitUntilAsync(() => h.Store.Runs.Single().State == PlexSyncRunStates.Succeeded);
    }

    [Test]
    public async Task Sync_LinkedMovieWithoutExternalId_StaysLinkedViaStoredPlexItemId()
    {
        var h = new Harness();
        h.Items.UpsertAsync(new PlexItemData { Id = "a", PlexItemId = "20", OnPlex = true }, CancellationToken.None).Wait();
        Serve(h, Item("20", new PlexGuids(null, null, null), Version("1080")));
        SetMovies(h, MovieWith("a", null));

        var run = await RunAsync(h);

        Assert.That(run.MatchedCount, Is.EqualTo(1));
        Assert.That(h.Items.Items["a"].LastSyncedAt, Is.Not.EqualTo(default(DateTime)));
    }

    [Test]
    public async Task Sync_NoGuidMatch_StaysUnmatchedEvenWhenTitlesAreEqual()
    {
        var h = new Harness();
        var item = Item("10", new PlexGuids("603", null, null), Version("1080")) with { Title = "Same Title" };
        Serve(h, item);
        SetMovies(h, new Movie { Id = "a", Title = "Same Title", ExternalId = "tmdbid:1" }, new Movie { Id = "b", Title = "Same Title" });

        var run = await RunAsync(h);

        Assert.That(h.Items.Items, Is.Empty);
        Assert.That(run.MatchedCount, Is.EqualTo(0));
        Assert.That(run.UnmatchedCount, Is.EqualTo(2));
    }

    [Test]
    public async Task Sync_MixedMovies_RecordsMatchedUnmatchedAndScannedCounts()
    {
        var h = new Harness();
        Serve(h, Item("10", new PlexGuids("603", null, null), Version("1080")), Item("11", new PlexGuids("1", null, null), Version("1080")));
        SetMovies(h, MovieWith("a", "tmdbid:603"), MovieWith("b", "tmdbid:2"), MovieWith("c", null));

        var run = await RunAsync(h);

        Assert.That(run.ItemsScanned, Is.EqualTo(2));
        Assert.That(run.MatchedCount, Is.EqualTo(1));
        Assert.That(run.UnmatchedCount, Is.EqualTo(2));
    }

    [Test]
    public async Task Sync_OneMovieFailsToStore_IsSkippedAndRunSucceeds()
    {
        var h = new Harness();
        h.Items.FailUpsertFor = "a";
        Serve(h, Item("10", new PlexGuids("603", null, null), Version("1080")), Item("11", new PlexGuids("604", null, null), Version("1080")));
        SetMovies(h, MovieWith("a", "tmdbid:603"), MovieWith("b", "tmdbid:604"));

        var run = await RunAsync(h);

        Assert.That(run.State, Is.EqualTo(PlexSyncRunStates.Succeeded));
        Assert.That(h.Items.Items.Keys, Is.EqualTo(["b"]));
        Assert.That(run.MatchedCount, Is.EqualTo(1));
        Assert.That(run.ErroredCount, Is.EqualTo(1));
        Assert.That(run.UnmatchedCount, Is.EqualTo(0));
    }

    [Test]
    public async Task Sync_SharedGuid_FirstScannedItemWins()
    {
        var h = new Harness();
        Serve(h, Item("10", new PlexGuids("603", null, null), Version("1080")), Item("11", new PlexGuids("603", null, null), Version("4k")));
        SetMovies(h, MovieWith("a", "tmdbid:603"));

        await RunAsync(h);

        Assert.That(h.Items.Items["a"].PlexItemId, Is.EqualTo("10"));
    }

    [Test]
    public async Task Sync_LinkedItemGoneFromPlex_IsFlaggedOffPlexAndCountedUnmatched()
    {
        var h = new Harness();
        h.Items.UpsertAsync(new PlexItemData { Id = "a", PlexItemId = "20", OnPlex = true, Resolution = "1080" }, CancellationToken.None).Wait();
        Serve(h);
        SetMovies(h, MovieWith("a", null));

        var run = await RunAsync(h);

        Assert.That(h.Items.Items["a"].OnPlex, Is.False);
        Assert.That(h.Items.Items["a"].Resolution, Is.EqualTo("1080"));
        Assert.That(run.UnmatchedCount, Is.EqualTo(1));
    }

    [Test]
    public async Task Sync_PlexDataOfDeletedMovie_IsPurged()
    {
        var h = new Harness();
        h.Items.UpsertAsync(new PlexItemData { Id = "gone", PlexItemId = "20", OnPlex = true }, CancellationToken.None).Wait();
        Serve(h);
        SetMovies(h, MovieWith("a", null));

        await RunAsync(h);

        Assert.That(h.Items.Items, Is.Empty);
    }

    [Test]
    public async Task Sync_MatchedMovie_StoresAllPlexFieldsFromBestVersion()
    {
        var h = new Harness();
        Serve(h, Item("10", new PlexGuids("603", null, null),
            Version("1080", size: 5, path: "/p/low.mkv"),
            Version("4k", hdr: true, size: 9, path: "/p/best.mkv")));
        SetMovies(h, MovieWith("a", "tmdbid:603"));

        await RunAsync(h);

        var d = h.Items.Items["a"];
        Assert.That(d.Id, Is.EqualTo("a"));
        Assert.That(d.PlexItemId, Is.EqualTo("10"));
        Assert.That(d.OnPlex, Is.True);
        Assert.That(d.Library, Is.EqualTo("Movies"));
        Assert.That(d.Resolution, Is.EqualTo("4k"));
        Assert.That(d.Hdr, Is.True);
        Assert.That(d.VideoCodec, Is.EqualTo("hevc"));
        Assert.That(d.BestAudioTrack, Is.EqualTo(new PlexAudioTrackData("ac3", 6, "eng")));
        Assert.That(d.SubtitleLanguages, Is.EqualTo(["eng", "fra"]));
        Assert.That(d.Watched, Is.True);
        Assert.That(d.ViewCount, Is.EqualTo(2));
        Assert.That(d.LastWatchedAt, Is.EqualTo(new DateTime(2024, 3, 9, 16, 0, 0, DateTimeKind.Utc)));
        Assert.That(d.DateAdded, Is.EqualTo(DateTimeOffset.FromUnixTimeSeconds(1700000000).UtcDateTime));
        Assert.That(d.DeepLink, Is.EqualTo("link-10"));
        Assert.That(d.DurationSeconds, Is.EqualTo(7200));
        Assert.That(d.Container, Is.EqualTo("mkv"));
        Assert.That(d.FilePath, Is.EqualTo("/p/best.mkv"));
        Assert.That(d.SizeBytes, Is.EqualTo(9));
        Assert.That(d.LastSyncedAt, Is.Not.EqualTo(default(DateTime)));
    }

    [Test]
    public async Task Sync_UnwatchedItem_IsNotWatched()
    {
        var h = new Harness();
        Serve(h, Item("10", new PlexGuids("603", null, null), Version("1080")) with { ViewCount = 0, LastViewedAt = null });
        SetMovies(h, MovieWith("a", "tmdbid:603"));

        await RunAsync(h);

        Assert.That(h.Items.Items["a"].Watched, Is.False);
        Assert.That(h.Items.Items["a"].LastWatchedAt, Is.Null);
    }

    [Test]
    public void BestVersion_SameResolution_PrefersHdrThenLargerFile()
    {
        var sdr = Version("1080", hdr: false, size: 99);
        var hdrSmall = Version("1080", hdr: true, size: 1);
        var hdrLarge = Version("1080", hdr: true, size: 2);

        Assert.That(PlexMatching.BestVersion([sdr, hdrSmall, hdrLarge]), Is.SameAs(hdrLarge));
        Assert.That(PlexMatching.BestVersion([Version("720", true, 100), Version("1080", false, 1)])!.Resolution, Is.EqualTo("1080"));
        Assert.That(PlexMatching.BestVersion([Version("1080", true, 100), Version("4k", false, 1)])!.Resolution, Is.EqualTo("4k"));
    }

    [Test]
    public void BestVersion_HigherResolutionWithoutHdr_BeatsLowerResolutionWithHdr()
    {
        var best = PlexMatching.BestVersion([Version("1080", hdr: true, size: 100), Version("4k", hdr: false, size: 1)]);

        Assert.That(best!.Resolution, Is.EqualTo("4k"));
        Assert.That(best.Hdr, Is.False);
    }

    [Test]
    public void BestAudio_PrefersChannelsThenCodecRank()
    {
        var tracks = new[]
        {
            new PlexAudioTrack("aac", 2, "eng"),
            new PlexAudioTrack("ac3", 6, "eng"),
            new PlexAudioTrack("truehd", 6, "eng"),
            new PlexAudioTrack("dts", 6, "fra")
        };

        Assert.That(PlexMatching.BestAudio(tracks)!.Codec, Is.EqualTo("truehd"));
    }

    private static PlexItem Show(string key, PlexGuids guids) =>
        new(key, "show", $"Show {key}", 2005, "2", "TV Shows", DateTimeOffset.FromUnixTimeSeconds(1700000000), $"link-{key}", guids, []);

    private static PlexSeason Season(string showKey, int index, int episodes) =>
        new($"{showKey}0{index}", index, index == 0 ? "Specials" : $"Season {index}", episodes, DateTimeOffset.FromUnixTimeSeconds(1700000000 + index), $"season-link-{showKey}-{index}");

    private static Movie Tv(string id, string title, string? externalId) => new() { Id = id, Title = title, ExternalId = externalId, IsTvSeries = true };

    private static void ServeShow(Harness h, string showKey, PlexGuids guids, params PlexSeason[] seasons)
    {
        h.Client.Setup(c => c.GetSeasonsAsync(showKey, It.IsAny<CancellationToken>())).ReturnsAsync(seasons);
        Serve(h, Show(showKey, guids));
    }

    [Test]
    public async Task Sync_TvEntryWithSeasonInTitle_MatchesShowByExternalIdAndStoresScopeAndSeason()
    {
        var h = new Harness();
        ServeShow(h, "90", new PlexGuids("1399", null, null), Season("90", 0, 3), Season("90", 1, 7), Season("90", 2, 13));
        SetMovies(h, Tv("a", "Show: Season 2", "tmdbid:1399"));

        var run = await RunAsync(h);

        var data = h.Items.Items["a"];
        Assert.That(run.MatchedCount, Is.EqualTo(1));
        Assert.That(data.PlexItemId, Is.EqualTo("90"));
        Assert.That(data.LinkSource, Is.EqualTo(PlexLinkSources.Auto));
        Assert.That(data.Scope, Is.EqualTo(PlexScope.Single(2)));
        Assert.That(data.Seasons.Select(x => x.Index), Is.EqualTo([2]));
        Assert.That(data.Seasons.Single().EpisodeCount, Is.EqualTo(13));
        Assert.That(data.Seasons.Single().PlexSeasonId, Is.EqualTo("9002"));
    }

    [Test]
    public async Task Sync_TvEntry_StoresShowAndSeasonLevelFields()
    {
        var h = new Harness();
        ServeShow(h, "90", new PlexGuids(null, "tt0944947", null), Season("90", 1, 7));
        SetMovies(h, Tv("a", "Show - Season 1", "imdbid:tt0944947"));

        await RunAsync(h);

        var data = h.Items.Items["a"];
        Assert.That(data.OnPlex, Is.True);
        Assert.That(data.Library, Is.EqualTo("TV Shows"));
        Assert.That(data.DateAdded, Is.EqualTo(DateTimeOffset.FromUnixTimeSeconds(1700000000).UtcDateTime));
        Assert.That(data.DeepLink, Is.EqualTo("link-90"));
        Assert.That(data.LastSyncedAt, Is.Not.EqualTo(default(DateTime)));
        Assert.That(data.Seasons.Single().DeepLink, Is.EqualTo("season-link-90-1"));
        Assert.That(data.Seasons.Single().DateAdded, Is.EqualTo(DateTimeOffset.FromUnixTimeSeconds(1700000001).UtcDateTime));
        Assert.That(data.Seasons.Single().EpisodeCount, Is.EqualTo(7));
    }

    [Test]
    public async Task Sync_TvEntryRangeAndCompleteSeries_SelectSeasonsByIndex()
    {
        var h = new Harness();
        ServeShow(h, "90", new PlexGuids("1399", null, null), Season("90", 0, 3), Season("90", 1, 7), Season("90", 2, 13), Season("90", 3, 9));
        SetMovies(h, Tv("a", "Show: Seasons 1-2", "tmdbid:1399"), Tv("b", "Show: Complete Series", "tmdbid:1399"));

        await RunAsync(h);

        Assert.That(h.Items.Items["a"].Scope, Is.EqualTo(new PlexScope(PlexScopeKinds.Range, 1, 2)));
        Assert.That(h.Items.Items["a"].Seasons.Select(x => x.Index), Is.EqualTo([1, 2]));
        Assert.That(h.Items.Items["b"].Scope, Is.EqualTo(PlexScope.AllSeasons));
        Assert.That(h.Items.Items["b"].Seasons.Select(x => x.Index), Is.EqualTo([0, 1, 2, 3]));
    }

    [Test]
    public async Task Sync_SeveralTvEntriesOneShow_EachLinksWithItsOwnScopeAndSeasonsAreReadOnce()
    {
        var h = new Harness();
        ServeShow(h, "90", new PlexGuids("1399", null, null), Season("90", 1, 7), Season("90", 2, 13));
        SetMovies(h, Tv("a", "Show: Season 1", "tmdbid:1399"), Tv("b", "Show: Season 2", "tmdbid:1399"), Tv("c", "Show: Complete Series", "tmdbid:1399"));

        var run = await RunAsync(h);

        Assert.That(run.MatchedCount, Is.EqualTo(3));
        Assert.That(h.Items.Items.Values.Select(d => d.PlexItemId), Is.All.EqualTo("90"));
        Assert.That(h.Items.Items["a"].Seasons.Select(x => x.Index), Is.EqualTo([1]));
        Assert.That(h.Items.Items["b"].Seasons.Select(x => x.Index), Is.EqualTo([2]));
        Assert.That(h.Items.Items["c"].Seasons.Select(x => x.Index), Is.EqualTo([1, 2]));
        h.Client.Verify(c => c.GetSeasonsAsync("90", It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestCase("Show")]
    [TestCase("Show: Seasons 1-3 + Movie")]
    [TestCase("Show: Season 1 Season 2")]
    public async Task Sync_TvEntryWithUnreadableScope_StaysUnmatched(string title)
    {
        var h = new Harness();
        ServeShow(h, "90", new PlexGuids("1399", null, null), Season("90", 1, 7), Season("90", 2, 13));
        SetMovies(h, Tv("a", title, "tmdbid:1399"));

        var run = await RunAsync(h);

        Assert.That(run.MatchedCount, Is.EqualTo(0));
        Assert.That(run.UnmatchedCount, Is.EqualTo(1));
        Assert.That(h.Items.Items, Is.Empty);
    }

    [Test]
    public async Task Sync_TvEntrySeasonMissingOnPlex_StaysUnmatched()
    {
        var h = new Harness();
        ServeShow(h, "90", new PlexGuids("1399", null, null), Season("90", 1, 7));
        SetMovies(h, Tv("a", "Show: Season 4", "tmdbid:1399"), Tv("b", "Show: Seasons 5-6", "tmdbid:1399"));

        var run = await RunAsync(h);

        Assert.That(run.UnmatchedCount, Is.EqualTo(2));
        Assert.That(h.Items.Items, Is.Empty);
    }

    [Test]
    public async Task Sync_TvEntryWithMovieOnlyGuidMatch_IsNotLinkedToTheMovie()
    {
        var h = new Harness();
        // A Plex movie and a TV entry share the TMDB number 603: different TMDB namespaces
        Serve(h, Item("10", new PlexGuids("603", null, null), Version("1080")));
        SetMovies(h, Tv("a", "Show: Season 1", "tmdbid:603"));

        var run = await RunAsync(h);

        Assert.That(run.MatchedCount, Is.EqualTo(0));
        Assert.That(h.Items.Items, Is.Empty);
    }

    [Test]
    public async Task Sync_MovieEntryWithShowGuid_IsNotLinkedToTheShow()
    {
        var h = new Harness();
        ServeShow(h, "90", new PlexGuids("1399", null, null), Season("90", 1, 7));
        SetMovies(h, MovieWith("a", "tmdbid:1399"));

        var run = await RunAsync(h);

        Assert.That(run.MatchedCount, Is.EqualTo(0));
        Assert.That(h.Items.Items, Is.Empty);
    }

    [Test]
    public async Task Sync_ManualTvLink_KeepsItsScopeEvenWhenTitleSaysOtherwise()
    {
        var h = new Harness();
        await h.Items.UpsertAsync(new PlexItemData { Id = "a", PlexItemId = "90", OnPlex = true, LinkSource = PlexLinkSources.Manual, Scope = PlexScope.AllSeasons }, CancellationToken.None);
        ServeShow(h, "90", new PlexGuids("1399", null, null), Season("90", 1, 7), Season("90", 2, 13));
        SetMovies(h, Tv("a", "Show: Season 1", "tmdbid:1399"));

        await RunAsync(h);

        Assert.That(h.Items.Items["a"].LinkSource, Is.EqualTo(PlexLinkSources.Manual));
        Assert.That(h.Items.Items["a"].Scope, Is.EqualTo(PlexScope.AllSeasons));
        Assert.That(h.Items.Items["a"].Seasons.Select(x => x.Index), Is.EqualTo([1, 2]));
    }

    [Test]
    public async Task Sync_ManualTvLinkWithUnreadableTitle_IsStillRefreshed()
    {
        var h = new Harness();
        await h.Items.UpsertAsync(new PlexItemData { Id = "a", PlexItemId = "90", OnPlex = true, LinkSource = PlexLinkSources.Manual, Scope = PlexScope.Single(1) }, CancellationToken.None);
        ServeShow(h, "90", new PlexGuids(null, null, null), Season("90", 1, 8));
        SetMovies(h, Tv("a", "Show", null));

        var run = await RunAsync(h);

        Assert.That(run.MatchedCount, Is.EqualTo(1));
        Assert.That(h.Items.Items["a"].Seasons.Single().EpisodeCount, Is.EqualTo(8));
    }

    [Test]
    public async Task Sync_AutoTvLinkWhoseTitleLosesItsScope_KeepsTheStoredScope()
    {
        var h = new Harness();
        await h.Items.UpsertAsync(new PlexItemData { Id = "a", PlexItemId = "90", OnPlex = true, Scope = PlexScope.Single(2) }, CancellationToken.None);
        ServeShow(h, "90", new PlexGuids("1399", null, null), Season("90", 1, 7), Season("90", 2, 13));
        SetMovies(h, Tv("a", "Show", "tmdbid:1399"));

        await RunAsync(h);

        Assert.That(h.Items.Items["a"].Scope, Is.EqualTo(PlexScope.Single(2)));
        Assert.That(h.Items.Items["a"].Seasons.Select(x => x.Index), Is.EqualTo([2]));
    }

    [Test]
    public async Task Sync_TvShowGoneFromPlex_FlagsOffPlexAndKeepsScopeAndSeasons()
    {
        var h = new Harness();
        await h.Items.UpsertAsync(new PlexItemData
        {
            Id = "a",
            PlexItemId = "90",
            OnPlex = true,
            Scope = PlexScope.Single(2),
            Seasons = [new PlexSeasonData { Index = 2, PlexSeasonId = "9002", EpisodeCount = 13 }]
        }, CancellationToken.None);
        Serve(h, Item("10", new PlexGuids("603", null, null), Version("1080")));
        SetMovies(h, Tv("a", "Show: Season 2", "tmdbid:1399"));

        await RunAsync(h);

        Assert.That(h.Items.Items["a"].OnPlex, Is.False);
        Assert.That(h.Items.Items["a"].MissCount, Is.EqualTo(1));
        Assert.That(h.Items.Items["a"].Scope, Is.EqualTo(PlexScope.Single(2)));
        Assert.That(h.Items.Items["a"].Seasons.Single().EpisodeCount, Is.EqualTo(13));
    }

    [Test]
    public async Task Sync_PlexSeasonsRequestFails_RunFailsAndStoredDataIsUntouched()
    {
        var h = new Harness();
        await h.Items.UpsertAsync(new PlexItemData { Id = "a", PlexItemId = "90", OnPlex = true, Scope = PlexScope.Single(2) }, CancellationToken.None);
        h.Client.Setup(c => c.GetSeasonsAsync("90", It.IsAny<CancellationToken>())).ThrowsAsync(new PlexClientException(500, "Plex request failed"));
        Serve(h, Show("90", new PlexGuids("1399", null, null)));
        SetMovies(h, Tv("a", "Show: Season 2", "tmdbid:1399"));

        var run = await RunAsync(h);

        Assert.That(run.State, Is.EqualTo(PlexSyncRunStates.Failed));
        Assert.That(h.Items.Items["a"].OnPlex, Is.True);
    }

    [Test]
    public async Task Sync_TvEntryNoExternalIdButBackfilledFromTmdb_MatchesInTheSameRun()
    {
        var h = new Harness(tmdbConfigured: true);
        ServeShow(h, "90", new PlexGuids("1399", null, null), Season("90", 2, 13));
        var tv = Tv("a", "Show: Season 2", null);
        SetMovies(h, tv);
        h.Movies.Setup(s => s.GetAsync("a", It.IsAny<CancellationToken>())).ReturnsAsync(() => tv);
        h.Movies.Setup(s => s.UpdateAsync("a", It.IsAny<Movie>(), It.IsAny<CancellationToken>()))
            .Callback((string _, Movie m, CancellationToken _) => tv = m)
            .ReturnsAsync(default(MongoDB.Driver.ReplaceOneResult)!);
        h.Tmdb.Setup(t => t.SearchTvAsync("Show", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MediaSet.Api.Infrastructure.Lookup.Models.TmdbTvSearchResponse([new(1399, "Show")]));
        h.Movies.Setup(s => s.GetListAsync(It.IsAny<CancellationToken>())).ReturnsAsync(() => [tv]);

        var run = await RunAsync(h);

        Assert.That(run.ExternalIdsBackfilled, Is.EqualTo(1));
        Assert.That(run.MatchedCount, Is.EqualTo(1));
        Assert.That(h.Items.Items["a"].Scope, Is.EqualTo(PlexScope.Single(2)));
    }
    [Test]
    public async Task RefreshItemAsync_AutoLinkedTvEntryWhoseTitleChanged_RereadsScopeFromTitle()
    {
        var h = new Harness();
        await h.Items.UpsertAsync(new PlexItemData { Id = "a", PlexItemId = "90", OnPlex = true, Scope = PlexScope.Single(1) }, CancellationToken.None);
        h.Client.Setup(c => c.GetItemAsync("90", true, It.IsAny<CancellationToken>())).ReturnsAsync(Show("90", new PlexGuids("1399", null, null)));
        h.Client.Setup(c => c.GetSeasonsAsync("90", It.IsAny<CancellationToken>())).ReturnsAsync([Season("90", 1, 7), Season("90", 2, 13)]);
        h.Movies.Setup(s => s.GetAsync("a", It.IsAny<CancellationToken>())).ReturnsAsync(Tv("a", "Show: Season 2", "tmdbid:1399"));

        var result = await h.Service.RefreshItemAsync("movies", "a", CancellationToken.None);

        Assert.That(result.Status, Is.EqualTo(PlexRefreshStatus.Refreshed));
        Assert.That(h.Items.Items["a"].Scope, Is.EqualTo(PlexScope.Single(2)));
        Assert.That(h.Items.Items["a"].Seasons.Select(x => x.Index), Is.EqualTo([2]));
    }

    [Test]
    public async Task RefreshItemAsync_ManualLinkedTvEntry_KeepsItsScopeDespiteTitle()
    {
        var h = new Harness();
        await h.Items.UpsertAsync(new PlexItemData { Id = "a", PlexItemId = "90", OnPlex = true, LinkSource = PlexLinkSources.Manual, Scope = PlexScope.Single(1) }, CancellationToken.None);
        h.Client.Setup(c => c.GetItemAsync("90", true, It.IsAny<CancellationToken>())).ReturnsAsync(Show("90", new PlexGuids("1399", null, null)));
        h.Client.Setup(c => c.GetSeasonsAsync("90", It.IsAny<CancellationToken>())).ReturnsAsync([Season("90", 1, 7), Season("90", 2, 13)]);
        h.Movies.Setup(s => s.GetAsync("a", It.IsAny<CancellationToken>())).ReturnsAsync(Tv("a", "Show: Season 2", "tmdbid:1399"));

        await h.Service.RefreshItemAsync("movies", "a", CancellationToken.None);

        Assert.That(h.Items.Items["a"].Scope, Is.EqualTo(PlexScope.Single(1)));
    }

    private static PlexEpisode Episode(string showKey, int season, int number, int views = 0, params PlexVersion[] versions) =>
        new($"{showKey}{season}{number:00}", season, number, $"Episode {season}x{number}", views,
            views > 0 ? DateTimeOffset.FromUnixTimeSeconds(1710000000 + number) : null, versions);

    private static void ServeEpisodes(Harness h, string seasonKey, params PlexEpisode[] episodes) =>
        h.Client.Setup(c => c.GetEpisodesAsync(seasonKey, It.IsAny<CancellationToken>())).ReturnsAsync(episodes);

    private static PlexEpisodeData Stored(int season, int number, bool onPlex = true, int misses = 0) =>
        new() { SeasonNumber = season, EpisodeNumber = number, Title = $"Old {season}x{number}", PlexEpisodeId = $"old{season}{number}", OnPlex = onPlex, MissCount = misses, FilePath = "/old.mkv" };

    private static PlexItemData StoredShow(string source, PlexScope scope, params PlexEpisodeData[] episodes) => new()
    {
        Id = "a",
        PlexItemId = "90",
        OnPlex = true,
        LinkSource = source,
        Scope = scope,
        Seasons = [.. Enumerable.Range(scope.From ?? 0, (scope.To ?? 0) - (scope.From ?? 0) + 1).Select(i => new PlexSeasonData { Index = i, PlexSeasonId = $"900{i}", EpisodeCount = 5 })],
        Episodes = [.. episodes]
    };

    [Test]
    public async Task Sync_LinkedTvEntry_StoresEveryEpisodeWithItsFileDetailsAndWatchedState()
    {
        var h = new Harness();
        ServeShow(h, "90", new PlexGuids("1399", null, null), Season("90", 2, 13));
        ServeEpisodes(h, "9002", Episode("90", 2, 1, 2, Version("720", size: 5, path: "/p/low.mkv"), Version("1080", hdr: true, size: 9, path: "/p/s02e01.mkv")), Episode("90", 2, 2));
        SetMovies(h, Tv("a", "Show: Season 2", "tmdbid:1399"));

        await RunAsync(h);

        var episodes = h.Items.Items["a"].Episodes;
        var first = episodes.Single(e => e.EpisodeNumber == 1);
        Assert.That(episodes, Has.Count.EqualTo(2));
        Assert.That(first.SeasonNumber, Is.EqualTo(2));
        Assert.That(first.Title, Is.EqualTo("Episode 2x1"));
        Assert.That(first.PlexEpisodeId, Is.EqualTo("90201"));
        Assert.That(first.Watched, Is.True);
        Assert.That(first.LastWatchedAt, Is.EqualTo(DateTimeOffset.FromUnixTimeSeconds(1710000001).UtcDateTime));
        Assert.That(first.Resolution, Is.EqualTo("1080"));
        Assert.That(first.Hdr, Is.True);
        Assert.That(first.VideoCodec, Is.EqualTo("hevc"));
        Assert.That(first.BestAudioTrack, Is.EqualTo(new PlexAudioTrackData("ac3", 6, "eng")));
        Assert.That(first.AudioLanguages, Is.EqualTo(["eng"]));
        Assert.That(first.SubtitleLanguages, Is.EqualTo(["eng", "fra"]));
        Assert.That(first.DurationSeconds, Is.EqualTo(7200));
        Assert.That(first.Container, Is.EqualTo("mkv"));
        Assert.That(first.FilePath, Is.EqualTo("/p/s02e01.mkv"));
        Assert.That(first.SizeBytes, Is.EqualTo(9));
        Assert.That(first.OnPlex, Is.True);
        Assert.That(first.MissCount, Is.EqualTo(0));
        Assert.That(episodes.Single(e => e.EpisodeNumber == 2).Watched, Is.False);
    }

    [Test]
    public async Task Sync_LinkedTvEntry_FillsRollupsAndTopLevelFieldsFromThem()
    {
        var h = new Harness();
        ServeShow(h, "90", new PlexGuids("1399", null, null), Season("90", 2, 13));
        ServeEpisodes(h, "9002", Episode("90", 2, 1, 1, Version("1080", hdr: true)), Episode("90", 2, 2, 1, Version("1080")));
        SetMovies(h, Tv("a", "Show: Season 2", "tmdbid:1399"));

        await RunAsync(h);

        var data = h.Items.Items["a"];
        Assert.That(data.Rollup!.EpisodesPresent, Is.EqualTo(2));
        Assert.That(data.Rollup.EpisodesExpected, Is.EqualTo(13));
        Assert.That(data.Seasons.Single().Rollup!.EpisodesPresent, Is.EqualTo(2));
        Assert.That(data.Seasons.Single().Rollup!.EpisodesExpected, Is.EqualTo(13));
        Assert.That(data.Watched, Is.True);
        Assert.That(data.LastWatchedAt, Is.EqualTo(DateTimeOffset.FromUnixTimeSeconds(1710000002).UtcDateTime));
        Assert.That(data.Resolution, Is.EqualTo("1080"));
        Assert.That(data.Hdr, Is.True);
        Assert.That(data.SubtitleLanguages, Is.EqualTo(["eng", "fra"]));
    }

    [Test]
    public async Task Sync_SingleSeasonScope_RequestsEpisodesOfThatSeasonOnly()
    {
        var h = new Harness();
        ServeShow(h, "90", new PlexGuids("1399", null, null), Season("90", 0, 3), Season("90", 1, 7), Season("90", 2, 13), Season("90", 3, 9));
        SetMovies(h, Tv("a", "Show: Season 2", "tmdbid:1399"));

        await RunAsync(h);

        h.Client.Verify(c => c.GetEpisodesAsync("9002", It.IsAny<CancellationToken>()), Times.Once);
        h.Client.Verify(c => c.GetEpisodesAsync(It.Is<string>(k => k != "9002"), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Sync_RangeScope_RequestsEpisodesOfTheSeasonsInTheRangeOnly()
    {
        var h = new Harness();
        ServeShow(h, "90", new PlexGuids("1399", null, null), Season("90", 0, 3), Season("90", 1, 7), Season("90", 2, 13), Season("90", 3, 9));
        SetMovies(h, Tv("a", "Show: Seasons 1-2", "tmdbid:1399"));

        await RunAsync(h);

        h.Client.Verify(c => c.GetEpisodesAsync("9001", It.IsAny<CancellationToken>()), Times.Once);
        h.Client.Verify(c => c.GetEpisodesAsync("9002", It.IsAny<CancellationToken>()), Times.Once);
        h.Client.Verify(c => c.GetEpisodesAsync(It.Is<string>(k => k != "9001" && k != "9002"), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Sync_SeveralEntriesCoveringOneSeason_ReadItsEpisodesOnce()
    {
        var h = new Harness();
        ServeShow(h, "90", new PlexGuids("1399", null, null), Season("90", 1, 7), Season("90", 2, 13));
        ServeEpisodes(h, "9002", Episode("90", 2, 1));
        SetMovies(h, Tv("a", "Show: Season 2", "tmdbid:1399"), Tv("b", "Show: Complete Series", "tmdbid:1399"));

        await RunAsync(h);

        h.Client.Verify(c => c.GetEpisodesAsync("9002", It.IsAny<CancellationToken>()), Times.Once);
        Assert.That(h.Items.Items["a"].Episodes, Has.Count.EqualTo(1));
        Assert.That(h.Items.Items["b"].Episodes, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task Sync_ShowInPlexButNoEntryLinked_RequestsNoEpisodes()
    {
        var h = new Harness();
        ServeShow(h, "90", new PlexGuids("1399", null, null), Season("90", 1, 7));
        // Different ExternalId, and an entry whose title gives no readable scope
        SetMovies(h, Tv("a", "Other: Season 1", "tmdbid:555"), Tv("b", "Show", "tmdbid:1399"));

        var run = await RunAsync(h);

        Assert.That(run.MatchedCount, Is.EqualTo(0));
        Assert.That(h.Items.Items, Is.Empty);
        h.Client.Verify(c => c.GetEpisodesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Sync_ShowWithoutTheEntrysSeasons_IsNotMatchedAndRequestsNoEpisodes()
    {
        var h = new Harness();
        ServeShow(h, "90", new PlexGuids("1399", null, null), Season("90", 1, 7));
        SetMovies(h, Tv("a", "Show: Season 5", "tmdbid:1399"));

        await RunAsync(h);

        h.Client.Verify(c => c.GetEpisodesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Sync_MovieEntry_RequestsNoEpisodes()
    {
        var h = new Harness();
        Serve(h, Item("10", new PlexGuids("603", null, null), Version("1080")));
        SetMovies(h, MovieWith("a", "tmdbid:603"));

        await RunAsync(h);

        h.Client.Verify(c => c.GetEpisodesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.That(h.Items.Items["a"].Episodes, Is.Empty);
        Assert.That(h.Items.Items["a"].Rollup, Is.Null);
    }

    [Test]
    public async Task Sync_ManualLinkedTvEntry_ReadsEpisodesOfItsScope()
    {
        var h = new Harness();
        await h.Items.UpsertAsync(StoredShow(PlexLinkSources.Manual, PlexScope.Single(1)), CancellationToken.None);
        ServeShow(h, "90", new PlexGuids("1", null, null), Season("90", 1, 7), Season("90", 2, 13));
        ServeEpisodes(h, "9001", Episode("90", 1, 1));
        SetMovies(h, Tv("a", "Whatever", null));

        await RunAsync(h);

        Assert.That(h.Items.Items["a"].Episodes.Select(e => e.PlexEpisodeId), Is.EqualTo(["90101"]));
        h.Client.Verify(c => c.GetEpisodesAsync("9002", It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Sync_AutoLinkEpisodeMissing_IsFlaggedThenDroppedOnTheSecondMiss()
    {
        var h = new Harness();
        await h.Items.UpsertAsync(StoredShow(PlexLinkSources.Auto, PlexScope.Single(2), Stored(2, 1), Stored(2, 2, onPlex: false, misses: 1)), CancellationToken.None);
        ServeShow(h, "90", new PlexGuids("1399", null, null), Season("90", 2, 13));
        ServeEpisodes(h, "9002", Episode("90", 2, 3));
        SetMovies(h, Tv("a", "Show: Season 2", "tmdbid:1399"));

        await RunAsync(h);

        var episodes = h.Items.Items["a"].Episodes;
        Assert.That(episodes.Select(e => e.EpisodeNumber), Is.EqualTo([1, 3]));
        var missing = episodes.Single(e => e.EpisodeNumber == 1);
        Assert.That(missing.OnPlex, Is.False);
        Assert.That(missing.MissCount, Is.EqualTo(1));
        Assert.That(missing.FilePath, Is.EqualTo("/old.mkv"));
        Assert.That(episodes.Single(e => e.EpisodeNumber == 3).OnPlex, Is.True);
    }

    [Test]
    public async Task Sync_ManualLinkEpisodeMissing_IsKeptAndFlaggedNeverDropped()
    {
        var h = new Harness();
        await h.Items.UpsertAsync(StoredShow(PlexLinkSources.Manual, PlexScope.Single(2), Stored(2, 1, onPlex: false, misses: 7)), CancellationToken.None);
        ServeShow(h, "90", new PlexGuids("1399", null, null), Season("90", 2, 13));
        SetMovies(h, Tv("a", "Show: Season 2", null));

        await RunAsync(h);

        var episode = h.Items.Items["a"].Episodes.Single();
        Assert.That(episode.OnPlex, Is.False);
        Assert.That(episode.MissCount, Is.EqualTo(8));
        Assert.That(h.Items.Items["a"].Rollup!.EpisodesPresent, Is.EqualTo(0));
    }

    [Test]
    public async Task Sync_EpisodeFoundAgain_ResetsMissCountAndOnPlexWithFreshData()
    {
        var h = new Harness();
        await h.Items.UpsertAsync(StoredShow(PlexLinkSources.Auto, PlexScope.Single(2), Stored(2, 1, onPlex: false, misses: 1)), CancellationToken.None);
        ServeShow(h, "90", new PlexGuids("1399", null, null), Season("90", 2, 13));
        ServeEpisodes(h, "9002", Episode("90", 2, 1, 0, Version("720", path: "/new.mkv")));
        SetMovies(h, Tv("a", "Show: Season 2", "tmdbid:1399"));

        await RunAsync(h);

        var episode = h.Items.Items["a"].Episodes.Single();
        Assert.That(episode.OnPlex, Is.True);
        Assert.That(episode.MissCount, Is.EqualTo(0));
        Assert.That(episode.FilePath, Is.EqualTo("/new.mkv"));
    }

    [Test]
    public async Task Sync_EpisodeStreamLookupFailed_KeepsPreviouslyStoredStreamFields()
    {
        var h = new Harness();
        var old = Stored(2, 1);
        old.Hdr = true;
        old.BestAudioTrack = new PlexAudioTrackData("truehd", 8, "eng");
        old.AudioLanguages = ["eng", "jpn"];
        old.SubtitleLanguages = ["eng"];
        await h.Items.UpsertAsync(StoredShow(PlexLinkSources.Auto, PlexScope.Single(2), old), CancellationToken.None);
        ServeShow(h, "90", new PlexGuids("1399", null, null), Season("90", 2, 13));
        var noStreams = new PlexVersion("1080", false, null, [], [], TimeSpan.FromMinutes(40), "mkv", "/p/new.mkv", 3);
        h.Client.Setup(c => c.GetEpisodesAsync("9002", It.IsAny<CancellationToken>()))
            .ReturnsAsync([Episode("90", 2, 1, 0, noStreams) with { StreamsAvailable = false }]);
        SetMovies(h, Tv("a", "Show: Season 2", "tmdbid:1399"));

        await RunAsync(h);

        var episode = h.Items.Items["a"].Episodes.Single();
        Assert.That(episode.FilePath, Is.EqualTo("/p/new.mkv"));
        Assert.That(episode.Hdr, Is.True);
        Assert.That(episode.BestAudioTrack, Is.EqualTo(new PlexAudioTrackData("truehd", 8, "eng")));
        Assert.That(episode.AudioLanguages, Is.EqualTo(["eng", "jpn"]));
        Assert.That(episode.SubtitleLanguages, Is.EqualTo(["eng"]));
    }

    [Test]
    public async Task Sync_EpisodeStreamLookupFailed_RollupStaysStableFromKeptStreamFields()
    {
        var h = new Harness();
        var old = Stored(2, 1);
        old.Hdr = true;
        old.AudioLanguages = ["eng"];
        old.SubtitleLanguages = ["eng", "fra"];
        await h.Items.UpsertAsync(StoredShow(PlexLinkSources.Auto, PlexScope.Single(2), old), CancellationToken.None);
        ServeShow(h, "90", new PlexGuids("1399", null, null), Season("90", 2, 13));
        var noStreams = new PlexVersion("1080", false, null, [], [], TimeSpan.FromMinutes(40), "mkv", "/p/new.mkv", 3);
        h.Client.Setup(c => c.GetEpisodesAsync("9002", It.IsAny<CancellationToken>()))
            .ReturnsAsync([Episode("90", 2, 1, 0, noStreams) with { StreamsAvailable = false }]);
        SetMovies(h, Tv("a", "Show: Season 2", "tmdbid:1399"));

        await RunAsync(h);

        var data = h.Items.Items["a"];
        Assert.That(data.Rollup!.Hdr, Is.True);
        Assert.That(data.Rollup.AudioLanguages, Is.EqualTo(["eng"]));
        Assert.That(data.Rollup.SubtitleLanguages, Is.EqualTo(["eng", "fra"]));
        Assert.That(data.Hdr, Is.True);
        Assert.That(data.SubtitleLanguages, Is.EqualTo(["eng", "fra"]));
    }

    [Test]
    public async Task Sync_EpisodeStreamLookupFailedWithNoStoredRecord_LeavesStreamFieldsEmpty()
    {
        var h = new Harness();
        ServeShow(h, "90", new PlexGuids("1399", null, null), Season("90", 2, 13));
        var noStreams = new PlexVersion("1080", false, null, [], [], TimeSpan.FromMinutes(40), "mkv", "/p/new.mkv", 3);
        h.Client.Setup(c => c.GetEpisodesAsync("9002", It.IsAny<CancellationToken>()))
            .ReturnsAsync([Episode("90", 2, 1, 0, noStreams) with { StreamsAvailable = false }]);
        SetMovies(h, Tv("a", "Show: Season 2", "tmdbid:1399"));

        var run = await RunAsync(h);

        var episode = h.Items.Items["a"].Episodes.Single();
        Assert.That(run.State, Is.EqualTo(PlexSyncRunStates.Succeeded));
        Assert.That(episode.Hdr, Is.False);
        Assert.That(episode.BestAudioTrack, Is.Null);
        Assert.That(episode.AudioLanguages, Is.Empty);
        Assert.That(episode.SubtitleLanguages, Is.Empty);
        Assert.That(episode.FilePath, Is.EqualTo("/p/new.mkv"));
    }

    [Test]
    public async Task Sync_SeasonInScopeGoneFromPlex_TreatsItsEpisodesAsMissing()
    {
        var h = new Harness();
        await h.Items.UpsertAsync(StoredShow(PlexLinkSources.Auto, new PlexScope(PlexScopeKinds.Range, 1, 2), Stored(1, 1), Stored(2, 1), Stored(2, 2, misses: 1)), CancellationToken.None);
        ServeShow(h, "90", new PlexGuids("1399", null, null), Season("90", 1, 7));
        ServeEpisodes(h, "9001", Episode("90", 1, 1));
        SetMovies(h, Tv("a", "Show: Seasons 1-2", "tmdbid:1399"));

        await RunAsync(h);

        var episodes = h.Items.Items["a"].Episodes;
        Assert.That(episodes.Select(e => (e.SeasonNumber, e.EpisodeNumber, e.OnPlex, e.MissCount)), Is.EqualTo([(1, 1, true, 0), (2, 1, false, 1)]));
        h.Client.Verify(c => c.GetEpisodesAsync("9002", It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Sync_ShowGoneFromPlex_StoredEpisodesStayAsTheyWere()
    {
        var h = new Harness();
        await h.Items.UpsertAsync(StoredShow(PlexLinkSources.Auto, PlexScope.Single(2), Stored(2, 1)), CancellationToken.None);
        Serve(h, Item("10", new PlexGuids("603", null, null), Version("1080")));
        SetMovies(h, Tv("a", "Show: Season 2", "tmdbid:1399"));

        await RunAsync(h);

        var data = h.Items.Items["a"];
        Assert.That(data.OnPlex, Is.False);
        Assert.That(data.Episodes.Single().OnPlex, Is.True);
        Assert.That(data.Episodes.Single().MissCount, Is.EqualTo(0));
        h.Client.Verify(c => c.GetEpisodesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Sync_EpisodeRequestFails_RunFailsAndStoredEpisodesAreUntouched()
    {
        var h = new Harness();
        var stored = StoredShow(PlexLinkSources.Auto, PlexScope.Single(2), Stored(2, 1, misses: 1));
        await h.Items.UpsertAsync(stored, CancellationToken.None);
        ServeShow(h, "90", new PlexGuids("1399", null, null), Season("90", 2, 13));
        h.Client.Setup(c => c.GetEpisodesAsync("9002", It.IsAny<CancellationToken>())).ThrowsAsync(new PlexClientException(500, "Plex request failed"));
        SetMovies(h, Tv("a", "Show: Season 2", "tmdbid:1399"));

        var run = await RunAsync(h);

        Assert.That(run.State, Is.EqualTo(PlexSyncRunStates.Failed));
        Assert.That(h.Items.Items["a"], Is.SameAs(stored));
        Assert.That(stored.Episodes.Single().MissCount, Is.EqualTo(1));
        Assert.That(stored.Episodes.Single().OnPlex, Is.True);
    }

    [Test]
    public async Task RefreshItemAsync_LinkedTvEntry_StoresEpisodesAndFlagsMissingOnesWithoutCountingMisses()
    {
        var h = new Harness();
        await h.Items.UpsertAsync(StoredShow(PlexLinkSources.Manual, PlexScope.Single(2), Stored(2, 9)), CancellationToken.None);
        h.Client.Setup(c => c.GetItemAsync("90", true, It.IsAny<CancellationToken>())).ReturnsAsync(Show("90", new PlexGuids("1399", null, null)));
        h.Client.Setup(c => c.GetSeasonsAsync("90", It.IsAny<CancellationToken>())).ReturnsAsync([Season("90", 1, 7), Season("90", 2, 13)]);
        ServeEpisodes(h, "9002", Episode("90", 2, 1, 1, Version("1080")));

        var result = await h.Service.RefreshItemAsync("movies", "a", CancellationToken.None);

        var episodes = h.Items.Items["a"].Episodes;
        Assert.That(result.Status, Is.EqualTo(PlexRefreshStatus.Refreshed));
        Assert.That(episodes.Select(e => (e.EpisodeNumber, e.OnPlex, e.MissCount)), Is.EqualTo([(1, true, 0), (9, false, 0)]));
        h.Client.Verify(c => c.GetEpisodesAsync("9001", It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task RefreshItemAsync_EpisodeRequestFails_ThrowsAndStoredDataIsUntouched()
    {
        var h = new Harness();
        var stored = StoredShow(PlexLinkSources.Manual, PlexScope.Single(2), Stored(2, 1));
        await h.Items.UpsertAsync(stored, CancellationToken.None);
        h.Client.Setup(c => c.GetItemAsync("90", true, It.IsAny<CancellationToken>())).ReturnsAsync(Show("90", new PlexGuids("1399", null, null)));
        h.Client.Setup(c => c.GetSeasonsAsync("90", It.IsAny<CancellationToken>())).ReturnsAsync([Season("90", 2, 13)]);
        h.Client.Setup(c => c.GetEpisodesAsync("9002", It.IsAny<CancellationToken>())).ThrowsAsync(new PlexClientException(null, "Plex server could not be reached"));

        Assert.That(async () => await h.Service.RefreshItemAsync("movies", "a", CancellationToken.None), Throws.TypeOf<PlexClientException>());
        Assert.That(h.Items.Items["a"], Is.SameAs(stored));
    }
}

[TestFixture]
public class PlexSyncRunStoreTests
{
    private static string RenderKeys(CreateIndexModel<PlexSyncRun> model) =>
        model.Keys.Render(new RenderArgs<PlexSyncRun>(
            MongoDB.Bson.Serialization.BsonSerializer.LookupSerializer<PlexSyncRun>(),
            MongoDB.Bson.Serialization.BsonSerializer.SerializerRegistry)).ToString();

    [Test]
    public async Task InsertAsync_IndexNotYetEnsured_CreatesIndexBeforeInserting()
    {
        var indexes = new Mock<IMongoIndexManager<PlexSyncRun>>();
        var collection = new Mock<IMongoCollection<PlexSyncRun>>();
        collection.SetupGet(c => c.Indexes).Returns(indexes.Object);
        collection.Setup(c => c.InsertOneAsync(It.IsAny<PlexSyncRun>(), It.IsAny<InsertOneOptions>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var database = new Mock<IDatabaseService>();
        database.Setup(d => d.GetCollection<PlexSyncRun>()).Returns(collection.Object);
        var store = new PlexSyncRunStore(database.Object, NullLogger<PlexSyncRunStore>.Instance);

        await store.InsertAsync(new PlexSyncRun(), CancellationToken.None);
        await store.InsertAsync(new PlexSyncRun(), CancellationToken.None);

        indexes.Verify(i => i.CreateOneAsync(It.IsAny<CreateIndexModel<PlexSyncRun>>(), It.IsAny<CreateOneIndexOptions>(), It.IsAny<CancellationToken>()), Times.Once);
        collection.Verify(c => c.InsertOneAsync(It.IsAny<PlexSyncRun>(), It.IsAny<InsertOneOptions>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Test]
    public async Task InsertAsync_FirstEnsureFails_InsertsAnywayAndRetriesEnsureOnNextInsert()
    {
        var indexes = new Mock<IMongoIndexManager<PlexSyncRun>>();
        var calls = 0;
        indexes.Setup(i => i.CreateOneAsync(It.IsAny<CreateIndexModel<PlexSyncRun>>(), It.IsAny<CreateOneIndexOptions>(), It.IsAny<CancellationToken>()))
            .Returns(() => ++calls == 1 ? Task.FromException<string>(new MongoException("down")) : Task.FromResult("ok"));
        var collection = new Mock<IMongoCollection<PlexSyncRun>>();
        collection.SetupGet(c => c.Indexes).Returns(indexes.Object);
        collection.Setup(c => c.InsertOneAsync(It.IsAny<PlexSyncRun>(), It.IsAny<InsertOneOptions>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var database = new Mock<IDatabaseService>();
        database.Setup(d => d.GetCollection<PlexSyncRun>()).Returns(collection.Object);
        var store = new PlexSyncRunStore(database.Object, NullLogger<PlexSyncRunStore>.Instance);

        await store.InsertAsync(new PlexSyncRun(), CancellationToken.None);
        await store.InsertAsync(new PlexSyncRun(), CancellationToken.None);
        await store.InsertAsync(new PlexSyncRun(), CancellationToken.None);

        Assert.That(calls, Is.EqualTo(2));
        collection.Verify(c => c.InsertOneAsync(It.IsAny<PlexSyncRun>(), It.IsAny<InsertOneOptions>(), It.IsAny<CancellationToken>()), Times.Exactly(3));
    }

    [Test]
    public async Task EnsureCreatedAsync_CreatesIndexOnEntityTypeAndStartedAtDescending()
    {
        var indexes = new Mock<IMongoIndexManager<PlexSyncRun>>();
        var collection = new Mock<IMongoCollection<PlexSyncRun>>();
        collection.SetupGet(c => c.Indexes).Returns(indexes.Object);
        var database = new Mock<IDatabaseService>();
        database.Setup(d => d.GetCollection<PlexSyncRun>()).Returns(collection.Object);
        var store = new PlexSyncRunStore(database.Object, NullLogger<PlexSyncRunStore>.Instance);

        await store.EnsureCreatedAsync(CancellationToken.None);

        indexes.Verify(i => i.CreateOneAsync(
            It.Is<CreateIndexModel<PlexSyncRun>>(m => m.Options.Name == PlexSyncRunStore.IndexName
                && RenderKeys(m) == "{ \"EntityType\" : 1, \"StartedAt\" : -1 }"),
            It.IsAny<CreateOneIndexOptions>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
