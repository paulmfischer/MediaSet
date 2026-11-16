using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using MediaSet.Api.Features.Plex.Services;
using MediaSet.Api.Infrastructure.DataAccess;
using MediaSet.Api.Infrastructure.Lookup;
using MediaSet.Api.Infrastructure.Lookup.Clients.MusicBrainz;
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
public class MusicExternalIdBackfillServiceTests
{
    /// <summary>Fake MusicBrainz: answers barcode and title searches from tables and records every request with its (virtual) time.</summary>
    private sealed class FakeMusicBrainz(TimeProvider clock) : IMusicBrainzClient
    {
        public Dictionary<string, List<MusicBrainzRelease>> ByBarcode { get; } = new(StringComparer.Ordinal);
        public Dictionary<string, List<MusicBrainzRelease>> ByTitle { get; } = new(StringComparer.Ordinal);
        public List<(string Kind, string Key, DateTimeOffset At)> Calls { get; } = [];
        public Func<string, Exception?>? FailOn { get; set; }

        public Task<IReadOnlyList<MusicBrainzRelease>> SearchByBarcodeAsync(string barcode, CancellationToken cancellationToken)
        {
            Calls.Add(("barcode", barcode, clock.GetUtcNow()));
            if (FailOn?.Invoke(barcode) is { } ex)
            {
                throw ex;
            }
            return Task.FromResult<IReadOnlyList<MusicBrainzRelease>>(ByBarcode.TryGetValue(barcode, out var list) ? list : []);
        }

        public Task<IReadOnlyList<MusicBrainzRelease>> SearchByEntityPropertiesAsync(IReadOnlyDictionary<string, string> searchParams, CancellationToken cancellationToken)
        {
            var key = $"{searchParams["title"]}|{searchParams["artist"]}";
            Calls.Add(("title", key, clock.GetUtcNow()));
            if (FailOn?.Invoke(key) is { } ex)
            {
                throw ex;
            }
            return Task.FromResult<IReadOnlyList<MusicBrainzRelease>>(ByTitle.TryGetValue(key, out var list) ? list : []);
        }

        public Task<MusicBrainzRelease?> GetReleaseByBarcodeAsync(string barcode, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<MusicBrainzRelease?> GetReleaseByIdAsync(string releaseId, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class Harness
    {
        public List<Music> Albums { get; }
        public Mock<IEntityService<Music>> Service { get; } = new();
        public InMemoryMusicBackfillStore Attempts { get; } = new();
        public PlexBackfillConfiguration Config { get; } = new() { RequestsPerMinute = 30, BatchSize = 50 };
        public AutoAdvanceTimeProvider Clock { get; } = new(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
        public FakeMusicBrainz MusicBrainz { get; }

        public Harness(params Music[] albums)
        {
            MusicBrainz = new FakeMusicBrainz(Clock);
            Albums = [.. albums];
            Service.Setup(s => s.GetListAsync(It.IsAny<CancellationToken>())).ReturnsAsync(() => [.. Albums.Select(Copy)]);
            Service.Setup(s => s.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((string id, CancellationToken _) => Albums.Where(m => m.Id == id).Select(Copy).FirstOrDefault());
            Service.Setup(s => s.UpdateAsync(It.IsAny<string>(), It.IsAny<Music>(), It.IsAny<CancellationToken>()))
                .Callback((string id, Music m, CancellationToken _) => Albums[Albums.FindIndex(x => x.Id == id)] = m)
                .ReturnsAsync(default(ReplaceOneResult)!);
        }

        public MusicExternalIdBackfillService Build() =>
            new(Service.Object, Attempts, Options.Create(new PlexConfiguration { Backfill = Config }),
                NullLogger<MusicExternalIdBackfillService>.Instance, MusicBrainz, Clock);

        private static Music Copy(Music m) => new()
        {
            Id = m.Id,
            Title = m.Title,
            Artist = m.Artist,
            Barcode = m.Barcode,
            ExternalId = m.ExternalId,
            Format = m.Format,
            Label = m.Label,
        };
    }

    private sealed class InMemoryMusicBackfillStore : IMusicExternalIdBackfillStore
    {
        private readonly InMemoryExternalIdBackfillStore _inner = new();

        public IReadOnlyDictionary<string, DateTime> Attempts => _inner.Attempts;
        public Task<IReadOnlyDictionary<string, DateTime>> GetAttemptsAsync(CancellationToken cancellationToken) => _inner.GetAttemptsAsync(cancellationToken);
        public Task RecordAttemptAsync(string movieId, DateTime attemptedAt, CancellationToken cancellationToken) => _inner.RecordAttemptAsync(movieId, attemptedAt, cancellationToken);
        public Task DeleteAsync(IReadOnlyCollection<string> movieIds, CancellationToken cancellationToken) => _inner.DeleteAsync(movieIds, cancellationToken);
    }

    private static Music Album(string id, string title = "Kind of Blue", string artist = "Miles Davis", string barcode = "", string? externalId = null) =>
        new() { Id = id, Title = title, Artist = artist, Barcode = barcode, ExternalId = externalId };

    private static MusicBrainzRelease Release(string releaseId, string? groupId, string title = "Kind of Blue", params string[] artists) => new()
    {
        Id = releaseId,
        Title = title,
        ReleaseGroup = groupId == null ? null : new MusicBrainzReleaseGroup { Id = groupId },
        ArtistCredit = [.. (artists.Length == 0 ? ["Miles Davis"] : artists).Select(a => new MusicBrainzArtistCredit { Name = a })],
    };

    [Test]
    public async Task RunAsync_BarcodeWithOneReleaseGroup_SavesMbidAndSkipsTitleSearch()
    {
        var h = new Harness(Album("a", barcode: "0123"));
        h.MusicBrainz.ByBarcode["0123"] = [Release("r1", "g1"), Release("r2", "g1")];

        var result = await h.Build().RunAsync(CancellationToken.None);

        Assert.That(h.Albums[0].ExternalId, Is.EqualTo("mbid:g1"));
        Assert.That(result, Is.EqualTo(new ExternalIdBackfillResult(1, 0)));
        Assert.That(h.MusicBrainz.Calls.Select(c => c.Kind), Is.EqualTo(["barcode"]));
    }

    [Test]
    public async Task RunAsync_BarcodeWithNoResult_FallsBackToExactArtistAndTitle()
    {
        var h = new Harness(Album("a", barcode: "0123"));
        h.MusicBrainz.ByTitle["Kind of Blue|Miles Davis"] = [Release("r1", "g9", "kind of blue")];

        var result = await h.Build().RunAsync(CancellationToken.None);

        Assert.That(h.Albums[0].ExternalId, Is.EqualTo("mbid:g9"));
        Assert.That(result, Is.EqualTo(new ExternalIdBackfillResult(1, 0)));
        Assert.That(h.MusicBrainz.Calls.Select(c => c.Kind), Is.EqualTo(["barcode", "title"]));
    }

    [Test]
    public async Task RunAsync_NoBarcode_SearchesByArtistAndTitleOnly()
    {
        var h = new Harness(Album("a"));
        h.MusicBrainz.ByTitle["Kind of Blue|Miles Davis"] = [Release("r1", "g1")];

        await h.Build().RunAsync(CancellationToken.None);

        Assert.That(h.Albums[0].ExternalId, Is.EqualTo("mbid:g1"));
        Assert.That(h.MusicBrainz.Calls.Select(c => c.Kind), Is.EqualTo(["title"]));
    }

    [Test]
    public async Task RunAsync_BarcodeMatchesSeveralReleaseGroups_LeavesBlankWithoutTitleFallback()
    {
        var h = new Harness(Album("a", barcode: "0123"));
        h.MusicBrainz.ByBarcode["0123"] = [Release("r1", "g1"), Release("r2", "g2")];
        h.MusicBrainz.ByTitle["Kind of Blue|Miles Davis"] = [Release("r3", "g3")];

        var result = await h.Build().RunAsync(CancellationToken.None);

        Assert.That(h.Albums[0].ExternalId, Is.Null);
        Assert.That(result, Is.EqualTo(new ExternalIdBackfillResult(0, 1)));
        Assert.That(h.MusicBrainz.Calls.Select(c => c.Kind), Is.EqualTo(["barcode"]));
    }

    [Test]
    public async Task RunAsync_TitleSearchMatchesSeveralReleaseGroups_LeavesBlank()
    {
        var h = new Harness(Album("a"));
        h.MusicBrainz.ByTitle["Kind of Blue|Miles Davis"] = [Release("r1", "g1"), Release("r2", "g2")];

        var result = await h.Build().RunAsync(CancellationToken.None);

        Assert.That(h.Albums[0].ExternalId, Is.Null);
        Assert.That(result, Is.EqualTo(new ExternalIdBackfillResult(0, 1)));
    }

    [Test]
    public async Task RunAsync_NoResultsAnywhere_LeavesBlankAndCountsSkipped()
    {
        var h = new Harness(Album("a", barcode: "0123"));

        var result = await h.Build().RunAsync(CancellationToken.None);

        Assert.That(h.Albums[0].ExternalId, Is.Null);
        Assert.That(result, Is.EqualTo(new ExternalIdBackfillResult(0, 1)));
    }

    [Test]
    public async Task RunAsync_TitleOrArtistNotExact_IsNotAccepted()
    {
        var h = new Harness(Album("a"));
        h.MusicBrainz.ByTitle["Kind of Blue|Miles Davis"] =
        [
            Release("r1", "g1", "Kind of Blue (Legacy Edition)"),
            Release("r2", "g2", "Kind of Blue", "Miles Davis Quintet"),
            Release("r3", "g3", "Kind of Blue", "Miles Davis", "John Coltrane"),
            Release("r4", null),
        ];

        var result = await h.Build().RunAsync(CancellationToken.None);

        Assert.That(h.Albums[0].ExternalId, Is.Null);
        Assert.That(result, Is.EqualTo(new ExternalIdBackfillResult(0, 1)));
    }

    [Test]
    public async Task RunAsync_ConfidentMatch_ChangesOnlyExternalId()
    {
        var album = Album("a");
        album.Format = "CD";
        album.Label = "Columbia";
        var h = new Harness(album);
        h.MusicBrainz.ByTitle["Kind of Blue|Miles Davis"] = [Release("r1", "g1")];

        await h.Build().RunAsync(CancellationToken.None);

        Assert.That(h.Albums[0].Format, Is.EqualTo("CD"));
        Assert.That(h.Albums[0].Label, Is.EqualTo("Columbia"));
        Assert.That(h.Albums[0].Title, Is.EqualTo("Kind of Blue"));
    }

    [Test]
    public async Task RunAsync_AlbumWithExistingExternalId_IsNeverSearchedOrChanged()
    {
        var h = new Harness(Album("a", barcode: "0123", externalId: "mbid:manual"));
        h.MusicBrainz.ByBarcode["0123"] = [Release("r1", "g1")];

        var result = await h.Build().RunAsync(CancellationToken.None);

        Assert.That(h.Albums[0].ExternalId, Is.EqualTo("mbid:manual"));
        Assert.That(h.MusicBrainz.Calls, Is.Empty);
        Assert.That(result, Is.EqualTo(new ExternalIdBackfillResult(0, 0)));
    }

    [Test]
    public async Task RunAsync_ExternalIdEnteredWhileSearching_IsNotOverwritten()
    {
        var h = new Harness(Album("a", barcode: "0123"));
        h.MusicBrainz.ByBarcode["0123"] = [Release("r1", "g1")];
        h.MusicBrainz.FailOn = _ =>
        {
            h.Albums[0].ExternalId = "mbid:manual";
            return null;
        };

        var result = await h.Build().RunAsync(CancellationToken.None);

        Assert.That(h.Albums[0].ExternalId, Is.EqualTo("mbid:manual"));
        Assert.That(result, Is.EqualTo(new ExternalIdBackfillResult(0, 1)));
        h.Service.Verify(s => s.UpdateAsync(It.IsAny<string>(), It.IsAny<Music>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task RunAsync_AlbumWithoutBarcodeOrArtist_IsNotACandidate()
    {
        var h = new Harness(Album("a", artist: ""), Album("b", title: ""));

        var result = await h.Build().RunAsync(CancellationToken.None);

        Assert.That(h.MusicBrainz.Calls, Is.Empty);
        Assert.That(result, Is.EqualTo(new ExternalIdBackfillResult(0, 0)));
    }

    [Test]
    public async Task RunAsync_Requests_AreSpacedByTheConfiguredRate()
    {
        var h = new Harness(Album("a", barcode: "1"), Album("b", title: "B", artist: "X"));
        h.Config.RequestsPerMinute = 30; // one request every 2 seconds
        var start = h.Clock.GetUtcNow();

        await h.Build().RunAsync(CancellationToken.None);

        // Album a: barcode (no hit) then title search; album b: title search
        Assert.That(h.MusicBrainz.Calls.Select(c => c.At - start),
            Is.EqualTo(new List<TimeSpan> { TimeSpan.Zero, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(4) }));
    }

    [Test]
    public async Task RunAsync_MoreCandidatesThanBatchSize_SearchesOnlyBatchSize()
    {
        var h = new Harness([.. Enumerable.Range(1, 5).Select(i => Album($"m{i}", title: $"Album {i}"))]);
        h.Config.BatchSize = 2;
        h.Config.RequestsPerMinute = 6000;

        await h.Build().RunAsync(CancellationToken.None);

        Assert.That(h.MusicBrainz.Calls, Has.Count.EqualTo(2));
    }

    [Test]
    public async Task RunAsync_UnresolvableAlbumsBeyondBatch_LaterAlbumIsSearchedOnFollowingRun()
    {
        var h = new Harness(Album("m1", title: "Miss 1"), Album("m2", title: "Miss 2"), Album("m3", title: "Hit"));
        h.Config.BatchSize = 2;
        h.Config.RequestsPerMinute = 6000;
        h.MusicBrainz.ByTitle["Hit|Miles Davis"] = [Release("r", "g", "Hit")];
        var service = h.Build();

        await service.RunAsync(CancellationToken.None);
        var second = await service.RunAsync(CancellationToken.None);

        Assert.That(h.Albums[2].ExternalId, Is.EqualTo("mbid:g"));
        Assert.That(second.Filled, Is.EqualTo(1));
    }

    [Test]
    public async Task RunAsync_RateLimited_StopsEarlyKeepingEarlierResults()
    {
        var h = new Harness(Album("a", title: "First"), Album("b", title: "Limited"), Album("c", title: "Never"));
        h.Config.RequestsPerMinute = 6000;
        h.MusicBrainz.ByTitle["First|Miles Davis"] = [Release("r", "g1", "First")];
        h.MusicBrainz.FailOn = key => key.StartsWith("Limited", StringComparison.Ordinal)
            ? new ProviderRateLimitException("limit", null, HttpStatusCode.ServiceUnavailable)
            : null;

        var result = await h.Build().RunAsync(CancellationToken.None);

        Assert.That(result, Is.EqualTo(new ExternalIdBackfillResult(1, 0)));
        Assert.That(h.MusicBrainz.Calls.Select(c => c.Key), Does.Not.Contain("Never|Miles Davis"));
    }

    [Test]
    public async Task RunAsync_ErrorOnOneAlbum_CountsSkippedAndContinues()
    {
        var h = new Harness(Album("a", title: "Boom"), Album("b", title: "Fine"));
        h.Config.RequestsPerMinute = 6000;
        h.MusicBrainz.ByTitle["Fine|Miles Davis"] = [Release("r", "g2", "Fine")];
        h.MusicBrainz.FailOn = key => key.StartsWith("Boom", StringComparison.Ordinal) ? new InvalidOperationException("boom") : null;

        var result = await h.Build().RunAsync(CancellationToken.None);

        Assert.That(result, Is.EqualTo(new ExternalIdBackfillResult(1, 1)));
        Assert.That(h.Albums[1].ExternalId, Is.EqualTo("mbid:g2"));
    }

    [Test]
    public async Task RunAsync_AttemptsOfAlbumsNoLongerEligible_ArePurged()
    {
        var h = new Harness(Album("a"), Album("b", externalId: "mbid:x"));
        await h.Attempts.RecordAttemptAsync("b", DateTime.UtcNow, CancellationToken.None);
        await h.Attempts.RecordAttemptAsync("gone", DateTime.UtcNow, CancellationToken.None);
        h.Config.RequestsPerMinute = 6000;

        await h.Build().RunAsync(CancellationToken.None);

        Assert.That(h.Attempts.Attempts.Keys, Is.EqualTo(new List<string> { "a" }));
    }

    [Test]
    public void RunAsync_CancelledToken_PropagatesCancellation()
    {
        var h = new Harness(Album("a"));
        using var cts = new CancellationTokenSource();
        h.MusicBrainz.FailOn = _ =>
        {
            cts.Cancel();
            return new OperationCanceledException(cts.Token);
        };

        Assert.That(() => h.Build().RunAsync(cts.Token), Throws.InstanceOf<OperationCanceledException>());
    }

    /// <summary>Completes each timer at once and moves its clock forward by the due time, so throttle waits need no real time.</summary>
    private sealed class AutoAdvanceTimeProvider(DateTimeOffset start) : TimeProvider
    {
        private readonly object _gate = new();
        private DateTimeOffset _now = start;

        public override DateTimeOffset GetUtcNow()
        {
            lock (_gate)
            {
                return _now;
            }
        }

        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            if (dueTime != Timeout.InfiniteTimeSpan)
            {
                lock (_gate)
                {
                    _now += dueTime;
                }
                ThreadPool.QueueUserWorkItem(_ => callback(state));
            }
            return new NoopTimer();
        }

        private sealed class NoopTimer : ITimer
        {
            public bool Change(TimeSpan dueTime, TimeSpan period) => true;
            public void Dispose() { }
            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
    }
}
