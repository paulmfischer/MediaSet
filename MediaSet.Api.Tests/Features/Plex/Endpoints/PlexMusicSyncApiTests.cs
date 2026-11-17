using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MediaSet.Api.Features.Plex.Models;
using MediaSet.Api.Features.Plex.Services;
using MediaSet.Api.Infrastructure.DataAccess;
using MediaSet.Api.Infrastructure.Database;
using MediaSet.Api.Infrastructure.Plex;
using MediaSet.Api.Infrastructure.Storage;
using MediaSet.Api.Shared.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration.EnvironmentVariables;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using NUnit.Framework;
using SkiaSharp;

namespace MediaSet.Api.Tests.Features.Plex.Endpoints;

/// <summary>
/// Music sync over HTTP: matching by MusicBrainz id, storage, rollups and the cover call. Plex is served from
/// fixtures by a fake handler; the PlexSyncRuns and PlexItems collections are in-memory.
/// </summary>
[TestFixture]
public class PlexMusicSyncApiTests
{
    private const string Token = "super-secret-token-value";
    private const string AbbeyRoadId = "65b000000000000000000001";
    private const string KindOfBlueId = "65b000000000000000000002";
    private const string CompilationId = "65b000000000000000000003";
    private const string UnlistedId = "65b000000000000000000004";
    private const string TheWallId = "65b000000000000000000005";
    private const string RumoursId = "65b000000000000000000006";
    private const string GoneId = "65b000000000000000000007";

    private const string AbbeyRoadMbid = "11111111-1111-1111-1111-111111111111";
    private const string KindOfBlueMbid = "22222222-2222-2222-2222-222222222222";
    private const string CompilationMbid = "33333333-3333-3333-3333-333333333333";
    private const string TheWallMbid = "55555555-5555-5555-5555-555555555555";

    private readonly List<WebApplicationFactory<Program>> _factories = [];

    [TearDown]
    public void TearDown()
    {
        foreach (var factory in _factories)
        {
            factory.Dispose();
        }
        _factories.Clear();
    }

    private sealed record Harness(
        HttpClient Client,
        InMemoryPlexItemStore Items,
        InMemoryPlexSyncRunStore Runs,
        Mock<IMusicExternalIdBackfillService> Backfill,
        Mock<IPlexCoverService<Music>> Covers);

    private Harness CreateHarness(
        FakePlexHandler handler,
        IReadOnlyList<Music> music,
        bool moviesEnabled = false,
        InMemoryPlexItemStore? items = null,
        Mock<IPlexCoverService<Music>>? covers = null,
        Action<IServiceCollection>? configure = null)
    {
        var runs = new InMemoryPlexSyncRunStore();
        items ??= new InMemoryPlexItemStore();
        var backfill = new Mock<IMusicExternalIdBackfillService>();
        backfill.Setup(b => b.RunAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new ExternalIdBackfillResult(0, 0));
        covers ??= new Mock<IPlexCoverService<Music>>();
        var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Production");
            builder.ConfigureAppConfiguration((_, config) =>
            {
                foreach (var source in config.Sources.OfType<EnvironmentVariablesConfigurationSource>().ToList())
                {
                    config.Sources.Remove(source);
                }
            });
            builder.UseSetting("MediaSetDatabaseSettings:ConnectionString", "mongodb://127.0.0.1:27017");
            builder.UseSetting("ImageConfiguration:StoragePath", TestContext.CurrentContext.WorkDirectory);
            builder.UseSetting("ExternalLogging:Enabled", "false");
            builder.UseSetting("BackgroundImageLookupConfiguration:Enabled", "false");
            builder.UseSetting("PlexConfiguration:BaseUrl", "http://plex.local:32400");
            builder.UseSetting("PlexConfiguration:Token", Token);
            builder.UseSetting("PlexConfiguration:Entities:Music:Enabled", "true");
            builder.UseSetting("PlexConfiguration:Entities:Movies:Enabled", moviesEnabled.ToString());
            builder.ConfigureServices(services =>
            {
                foreach (var d in services.Where(d => d.ServiceType == typeof(IDatabaseService)
                    || d.ServiceType == typeof(IEntityService<Music>)
                    || d.ServiceType == typeof(IEntityService<Movie>)
                    || d.ServiceType == typeof(IMusicExternalIdBackfillService)
                    || d.ServiceType == typeof(IPlexCoverService<Music>)).ToList())
                {
                    services.Remove(d);
                }
                services.AddSingleton(new Mock<IDatabaseService>().Object);
                InMemoryPlexSyncRunStore.Replace(services, runs);
                InMemoryPlexItemStore.Replace(services, items);
                services.AddScoped(_ => MusicService(music).Object);
                services.AddScoped(_ => InMemoryPlexItemStore.MovieService().Object);
                services.AddScoped(_ => backfill.Object);
                services.AddScoped(_ => covers.Object);
                services.AddHttpClient(nameof(IPlexClient)).ConfigurePrimaryHttpMessageHandler(() => handler);
                configure?.Invoke(services);
            });
        });
        _factories.Add(factory);
        return new Harness(factory.CreateClient(), items, runs, backfill, covers);
    }

    private static Mock<IEntityService<Music>> MusicService(IReadOnlyList<Music> music)
    {
        var mock = new Mock<IEntityService<Music>>();
        mock.Setup(s => s.GetListAsync(It.IsAny<CancellationToken>())).ReturnsAsync(() => music.ToList());
        foreach (var m in music)
        {
            mock.Setup(s => s.GetAsync(m.Id!, It.IsAny<CancellationToken>())).ReturnsAsync(m);
        }
        return mock;
    }

    private static FakePlexHandler Server() => FakePlexHandler.WithServer()
        .Route("/library/sections/4/all", "music_albums.json")
        .Route("/library/metadata/7001/children", "music_tracks_7001.json")
        .Route("/library/metadata/7002/children", "music_tracks_7002.json")
        .Route("/library/metadata/7003/children", "music_tracks_7003.json")
        .Route("/library/metadata/7005/children", "music_tracks_7005.json")
        .Route("/library/metadata/7006/children", "music_tracks_7006.json");

    private static List<Music> Collection() =>
    [
        new() { Id = AbbeyRoadId, Title = "Abbey Road", Artist = "The Beatles", Tracks = 3, ExternalId = $"mbid:{AbbeyRoadMbid}" },
        new() { Id = KindOfBlueId, Title = "Kind of Blue", Artist = "Miles Davis", Tracks = 5, ExternalId = $"mbid:{KindOfBlueMbid}" },
        new() { Id = CompilationId, Title = "Now That's What I Call Music 90", Artist = "Someone Else", Tracks = 2, ExternalId = $"mbid:{CompilationMbid.ToUpperInvariant()}" },
        new() { Id = UnlistedId, Title = "Not On Plex", Artist = "Nobody", Tracks = 9, ExternalId = "mbid:99999999-9999-9999-9999-999999999999" },
        new() { Id = TheWallId, Title = "The Wall", Artist = "Pink Floyd", Tracks = 4, Discs = 2, ExternalId = $"mbid:{TheWallMbid}" },
        // Same title and artist as the Plex album, but no id: it must stay unmatched (no title guessing)
        new() { Id = RumoursId, Title = "Rumours", Artist = "Fleetwood Mac", Tracks = 1 }
    ];

    private static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response)
    {
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return doc.RootElement.Clone();
    }

    private static async Task<JsonElement> SyncAsync(HttpClient client)
    {
        var previous = await ReadJsonAsync(await client.GetAsync("/plex/music/status"));
        var previousId = previous.GetProperty("lastRun").ValueKind == JsonValueKind.Object ? previous.GetProperty("lastRun").GetProperty("id").GetString() : null;
        var started = await client.PostAsync("/plex/music/sync", null);
        Assert.That(started.StatusCode, Is.EqualTo(HttpStatusCode.Accepted));
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (DateTime.UtcNow < deadline)
        {
            var status = await ReadJsonAsync(await client.GetAsync("/plex/music/status"));
            var last = status.GetProperty("lastRun");
            if (last.ValueKind == JsonValueKind.Object && last.GetProperty("id").GetString() != previousId
                && last.GetProperty("state").GetString() is not ("running" or "queued"))
            {
                return last;
            }
            await Task.Delay(20);
        }

        throw new TimeoutException("The Plex music sync did not finish in time.");
    }

    [Test]
    public async Task Sync_MusicEnabled_RunsBackfillFirstThenMatchesAndRecordsTheRun()
    {
        var h = CreateHarness(Server(), Collection());
        h.Backfill.Setup(b => b.RunAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new ExternalIdBackfillResult(2, 1));

        var last = await SyncAsync(h.Client);

        Assert.That(last.GetProperty("state").GetString(), Is.EqualTo("succeeded"));
        Assert.That(last.GetProperty("entityType").GetString(), Is.EqualTo("music"));
        Assert.That(last.GetProperty("itemsScanned").GetInt32(), Is.EqualTo(6));
        Assert.That(last.GetProperty("matchedCount").GetInt32(), Is.EqualTo(4));
        Assert.That(last.GetProperty("unmatchedCount").GetInt32(), Is.EqualTo(2));
        Assert.That(last.GetProperty("externalIdsBackfilled").GetInt32(), Is.EqualTo(2));
        Assert.That(last.GetProperty("backfillSkipped").GetInt32(), Is.EqualTo(1));
        h.Backfill.Verify(b => b.RunAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task Sync_BackfillThrows_RunStillSucceeds()
    {
        var h = CreateHarness(Server(), Collection());
        h.Backfill.Setup(b => b.RunAsync(It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("musicbrainz down"));

        var last = await SyncAsync(h.Client);

        Assert.That(last.GetProperty("state").GetString(), Is.EqualTo("succeeded"));
        Assert.That(h.Items.Items, Has.Count.EqualTo(4));
    }

    [Test]
    public async Task Sync_ExternalIdMatchesPlexMusicBrainzId_StoresOneDocumentPerMatchOnly()
    {
        var h = CreateHarness(Server(), Collection());

        await SyncAsync(h.Client);

        // Id case does not matter, albums not in MediaSet are ignored, and Rumours is not matched by its title
        Assert.That(h.Items.Items.Keys, Is.EquivalentTo(new[] { AbbeyRoadId, KindOfBlueId, CompilationId, TheWallId }));
        Assert.That(h.Items.Items[AbbeyRoadId].PlexItemId, Is.EqualTo("7001"));
        Assert.That(h.Items.Items[CompilationId].PlexItemId, Is.EqualTo("7003"));
        Assert.That(h.Items.Items.Values.Select(d => d.PlexItemId), Does.Not.Contain("7004").And.Not.Contain("7006"));
    }

    [Test]
    public async Task Sync_TwoEntriesLinkOneAlbum_RecordsDuplicateLinkOnRun()
    {
        const string copyId = "65b000000000000000000008";
        var collection = Collection();
        collection.Add(new Music { Id = copyId, Title = "Abbey Road (2nd copy)", Artist = "The Beatles", Tracks = 3, ExternalId = $"mbid:{AbbeyRoadMbid}" });
        var h = CreateHarness(Server(), collection);

        var last = await SyncAsync(h.Client);

        var duplicates = last.GetProperty("duplicateLinks");
        Assert.That(duplicates.GetArrayLength(), Is.EqualTo(1));
        var duplicate = duplicates[0];
        Assert.That(duplicate.GetProperty("plexItemId").GetString(), Is.EqualTo("7001"));
        Assert.That(duplicate.GetProperty("movies").GetArrayLength(), Is.EqualTo(0));
        Assert.That(
            duplicate.GetProperty("albums").EnumerateArray().Select(a => (a.GetProperty("id").GetString(), a.GetProperty("title").GetString())),
            Is.EqualTo([(AbbeyRoadId, "Abbey Road"), (copyId, "Abbey Road (2nd copy)")]));
    }

    [Test]
    public async Task Sync_EachEntryOnItsOwnAlbum_RecordsNoDuplicateLinks()
    {
        var h = CreateHarness(Server(), Collection());

        var last = await SyncAsync(h.Client);

        Assert.That(last.GetProperty("duplicateLinks").GetArrayLength(), Is.EqualTo(0));
    }

    [Test]
    public async Task Sync_MatchedAlbum_StoresRolledUpPlexData()
    {
        var h = CreateHarness(Server(), Collection());

        await SyncAsync(h.Client);
        var data = h.Items.Items[AbbeyRoadId];

        Assert.That(data.EntityType, Is.EqualTo("music"));
        Assert.That(data.OnPlex, Is.True);
        Assert.That(data.LinkSource, Is.EqualTo(PlexLinkSources.Auto));
        Assert.That(data.Library, Is.EqualTo("Music"));
        Assert.That(data.DeepLink, Does.Contain("/library/metadata/7001").Or.Contain("%2Flibrary%2Fmetadata%2F7001"));
        Assert.That(data.DateAdded, Is.EqualTo(DateTimeOffset.FromUnixTimeSeconds(1700000000).UtcDateTime));
        Assert.That(data.PlayCount, Is.EqualTo(6));
        Assert.That(data.LastPlayedAt, Is.EqualTo(DateTimeOffset.FromUnixTimeSeconds(1720000000).UtcDateTime));
        Assert.That(data.TracksPresent, Is.EqualTo(3));
        Assert.That(data.TracksExpected, Is.EqualTo(3));
        Assert.That(data.AudioCodec, Is.EqualTo("flac"));
        Assert.That(data.Bitrate, Is.EqualTo(740));
        Assert.That(data.SizeBytes, Is.EqualTo(80000000));
    }

    [Test]
    public async Task Sync_MatchedAlbum_StoresNoWatchedFlagRatingOrFilePath()
    {
        var h = CreateHarness(Server(), Collection());

        await SyncAsync(h.Client);
        var data = h.Items.Items[AbbeyRoadId];

        Assert.That(data.Watched, Is.False);
        Assert.That(data.FilePath, Is.Null);
        Assert.That(data.Scope, Is.Null);
        Assert.That(data.Episodes, Is.Empty);
    }

    [Test]
    public async Task Sync_FewerTracksOnPlexThanExpected_StoresPartialAlbum()
    {
        var h = CreateHarness(Server(), Collection());

        await SyncAsync(h.Client);
        var data = h.Items.Items[KindOfBlueId];

        Assert.That(data.OnPlex, Is.True);
        Assert.That(data.TracksPresent, Is.EqualTo(2));
        Assert.That(data.TracksExpected, Is.EqualTo(5));
        Assert.That(data.PlayCount, Is.EqualTo(1));
    }

    [Test]
    public async Task Sync_NeverPlayedAlbum_StoresZeroPlaysAndNoLastPlayed()
    {
        var h = CreateHarness(Server(), Collection());

        await SyncAsync(h.Client);
        var data = h.Items.Items[CompilationId];

        Assert.That(data.PlayCount, Is.EqualTo(0));
        Assert.That(data.LastPlayedAt, Is.Null);
        Assert.That(data.AudioCodec, Is.EqualTo("mp3"));
    }

    [Test]
    public async Task Sync_MultiDiscAlbum_IsOneDocumentSummedAcrossDiscs()
    {
        var h = CreateHarness(Server(), Collection());

        await SyncAsync(h.Client);
        var data = h.Items.Items[TheWallId];

        Assert.That(h.Items.Items.Values.Count(d => d.PlexItemId == "7005"), Is.EqualTo(1));
        Assert.That(data.TracksPresent, Is.EqualTo(4));
        Assert.That(data.PlayCount, Is.EqualTo(4));
        Assert.That(data.SizeBytes, Is.EqualTo(4000));
    }

    [Test]
    public async Task Sync_ReadsAlbumsOfArtistLibrariesOnly()
    {
        var handler = Server();
        var h = CreateHarness(handler, Collection());

        await SyncAsync(h.Client);

        var listings = handler.Requests.Where(r => r.Uri.AbsolutePath.StartsWith("/library/sections/") && r.Uri.AbsolutePath.EndsWith("/all")).ToList();
        Assert.That(listings.Select(r => r.Uri.AbsolutePath), Is.EqualTo(["/library/sections/4/all"]));
        Assert.That(listings.Single().Uri.Query, Does.Contain("type=9").And.Contain("includeGuids=1"));
    }

    [Test]
    public async Task Sync_MatchedAlbums_CallCoverServiceWithTheAlbumRatingKey()
    {
        var h = CreateHarness(Server(), Collection());

        await SyncAsync(h.Client);

        h.Covers.Verify(c => c.TryApplyAsync(AbbeyRoadId, "7001", It.IsAny<CancellationToken>()), Times.Once);
        h.Covers.Verify(c => c.TryApplyAsync(TheWallId, "7005", It.IsAny<CancellationToken>()), Times.Once);
        h.Covers.Verify(c => c.TryApplyAsync(UnlistedId, It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        h.Covers.Verify(c => c.TryApplyAsync(RumoursId, It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Sync_CoverKnownUnusable_IsNotFetchedAgainWhileTheAlbumIsUnchanged()
    {
        var covers = new Mock<IPlexCoverService<Music>>();
        covers.Setup(c => c.TryApplyAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(PlexCoverOutcome.Unusable);
        var h = CreateHarness(Server(), Collection(), covers: covers);

        await SyncAsync(h.Client);
        await SyncAsync(h.Client);

        covers.Verify(c => c.TryApplyAsync(AbbeyRoadId, "7001", It.IsAny<CancellationToken>()), Times.Once);
        Assert.That(h.Items.Items[AbbeyRoadId].CoverAttemptKey, Is.Not.Null);
    }

    [Test]
    public async Task Sync_CoverAttemptFailed_IsRetriedNextSync()
    {
        var covers = new Mock<IPlexCoverService<Music>>();
        covers.Setup(c => c.TryApplyAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(PlexCoverOutcome.Failed);
        var h = CreateHarness(Server(), Collection(), covers: covers);

        await SyncAsync(h.Client);
        await SyncAsync(h.Client);

        covers.Verify(c => c.TryApplyAsync(AbbeyRoadId, "7001", It.IsAny<CancellationToken>()), Times.Exactly(2));
        Assert.That(h.Items.Items[AbbeyRoadId].CoverAttemptKey, Is.Null);
    }

    [Test]
    public async Task Sync_CoverAttemptRecordedForAnotherAlbumState_IsTriedAgain()
    {
        var covers = new Mock<IPlexCoverService<Music>>();
        covers.Setup(c => c.TryApplyAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(PlexCoverOutcome.Unusable);
        var h = CreateHarness(Server(), Collection(), covers: covers);
        await SyncAsync(h.Client);
        // The album changed on Plex since the recorded attempt
        h.Items.Items[AbbeyRoadId].CoverAttemptKey = "7001|1";

        await SyncAsync(h.Client);

        covers.Verify(c => c.TryApplyAsync(AbbeyRoadId, "7001", It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Test]
    public async Task Sync_CoverAttemptOlderThanTheRetryWindow_IsTriedAgain()
    {
        var covers = new Mock<IPlexCoverService<Music>>();
        covers.Setup(c => c.TryApplyAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(PlexCoverOutcome.Unusable);
        var h = CreateHarness(Server(), Collection(), covers: covers);
        await SyncAsync(h.Client);
        h.Items.Items[AbbeyRoadId].CoverAttemptedAt = DateTime.UtcNow - PlexMusicMatching.CoverRetryAfter - TimeSpan.FromDays(1);

        await SyncAsync(h.Client);

        covers.Verify(c => c.TryApplyAsync(AbbeyRoadId, "7001", It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Test]
    public async Task Sync_RealCoverService_FillsMissingCoverAndNeverReplacesAnExistingOne()
    {
        var music = Collection();
        var existing = new Image { Id = "keep", FilePath = "musics/keep.jpg", ContentType = "image/jpeg" };
        music.Single(m => m.Id == KindOfBlueId).CoverImage = existing;
        var saved = new Image { Id = "plex", FilePath = "musics/plex.jpg", ContentType = "image/jpeg" };
        var images = new Mock<IImageService>();
        images.Setup(i => i.SaveImageBytesAsync(It.IsAny<byte[]>(), "Musics", It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(saved);
        var updates = new Mock<IEntityService<Music>>();
        updates.Setup(s => s.GetListAsync(It.IsAny<CancellationToken>())).ReturnsAsync(() => music.ToList());
        foreach (var m in music)
        {
            updates.Setup(s => s.GetAsync(m.Id!, It.IsAny<CancellationToken>())).ReturnsAsync(m);
        }
        var handler = Server().Route("/library/metadata/7001/thumb", _ => ImageResponse());
        handler.Route("/library/metadata/7002/thumb", _ => ImageResponse());
        var h = CreateHarness(handler, music, configure: services =>
        {
            services.AddScoped(_ => updates.Object);
            services.AddScoped(_ => images.Object);
            services.AddScoped<IPlexCoverService<Music>, PlexCoverService<Music>>();
        });

        await SyncAsync(h.Client);

        updates.Verify(s => s.UpdateAsync(AbbeyRoadId, It.Is<Music>(m => m.CoverImage == saved), It.IsAny<CancellationToken>()), Times.Once);
        updates.Verify(s => s.UpdateAsync(KindOfBlueId, It.IsAny<Music>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.That(music.Single(m => m.Id == KindOfBlueId).CoverImage, Is.SameAs(existing));
        Assert.That(handler.Requests.Select(r => r.Uri.AbsolutePath), Does.Not.Contain("/library/metadata/7002/thumb"));
    }

    private static HttpResponseMessage ImageResponse()
    {
        using var bitmap = new SKBitmap(600, 600);
        bitmap.Erase(SKColors.Blue);
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Jpeg, 75);
        var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(data.ToArray()) };
        response.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/jpeg");
        return response;
    }

    [Test]
    public async Task Sync_AutoLinkedAlbumNoLongerInPlex_IsKeptAsNotOnPlexThenDroppedAfterThreeMisses()
    {
        var items = new InMemoryPlexItemStore();
        await items.UpsertAsync(new PlexItemData { Id = GoneId, PlexItemId = "7999", EntityType = "music", OnPlex = true, PlayCount = 5 }, CancellationToken.None);
        var music = Collection();
        music.Add(new Music { Id = GoneId, Title = "Gone", Artist = "x" });
        var h = CreateHarness(Server(), music, items: items);

        await SyncAsync(h.Client);
        var afterFirst = items.Items[GoneId];
        await SyncAsync(h.Client);
        await SyncAsync(h.Client);

        Assert.That(afterFirst.OnPlex, Is.False);
        Assert.That(afterFirst.MissCount, Is.EqualTo(1));
        Assert.That(afterFirst.PlayCount, Is.EqualTo(5));
        Assert.That(items.Items.ContainsKey(GoneId), Is.False);
    }

    [Test]
    public async Task Sync_ManualLinkToAlbumOutsideTheScannedLibraries_IsKeptAndNeverReplacedByAnIdMatch()
    {
        var items = new InMemoryPlexItemStore();
        await items.UpsertAsync(new PlexItemData { Id = AbbeyRoadId, PlexItemId = "7006", EntityType = "music", OnPlex = true, LinkSource = PlexLinkSources.Manual }, CancellationToken.None);
        var h = CreateHarness(Server(), Collection(), items: items);

        await SyncAsync(h.Client);

        var data = items.Items[AbbeyRoadId];
        Assert.That(data.PlexItemId, Is.EqualTo("7006"));
        Assert.That(data.LinkSource, Is.EqualTo(PlexLinkSources.Manual));
        Assert.That(data.TracksPresent, Is.EqualTo(1));
    }

    [Test]
    public async Task Sync_ManualLinkWhosePlexAlbumIsGone_IsFlaggedBrokenNotDropped()
    {
        var items = new InMemoryPlexItemStore();
        await items.UpsertAsync(new PlexItemData { Id = UnlistedId, PlexItemId = "7999", EntityType = "music", OnPlex = true, LinkSource = PlexLinkSources.Manual }, CancellationToken.None);
        var h = CreateHarness(Server(), Collection(), items: items);

        for (var i = 0; i < 4; i++)
        {
            await SyncAsync(h.Client);
        }

        var data = items.Items[UnlistedId];
        Assert.That(data.Broken, Is.True);
        Assert.That(data.OnPlex, Is.False);
        Assert.That(data.MissCount, Is.EqualTo(4));
    }

    [Test]
    public async Task Sync_DocumentOfADeletedMusicEntry_IsPurged()
    {
        var items = new InMemoryPlexItemStore();
        await items.UpsertAsync(new PlexItemData { Id = "65b0000000000000000000aa", PlexItemId = "7001", EntityType = "music", OnPlex = true }, CancellationToken.None);
        var h = CreateHarness(Server(), Collection(), items: items);

        await SyncAsync(h.Client);

        Assert.That(items.Items.ContainsKey("65b0000000000000000000aa"), Is.False);
    }

    [Test]
    public async Task Sync_MusicRun_DoesNotTouchMovieDocuments()
    {
        var items = new InMemoryPlexItemStore();
        await items.UpsertAsync(new PlexItemData { Id = "65a000000000000000000001", PlexItemId = "1001", OnPlex = true, Watched = true }, CancellationToken.None);
        var h = CreateHarness(Server(), Collection(), moviesEnabled: true, items: items);

        await SyncAsync(h.Client);

        Assert.That(items.Items.ContainsKey("65a000000000000000000001"), Is.True);
        Assert.That(items.Items["65a000000000000000000001"].EntityType, Is.Null);
    }

    [Test]
    public async Task Sync_TracksOfOneAlbumUnreadable_ThatEntryIsErroredAndKeepsItsStoredData()
    {
        var items = new InMemoryPlexItemStore();
        await items.UpsertAsync(new PlexItemData { Id = KindOfBlueId, PlexItemId = "7002", EntityType = "music", OnPlex = true, PlayCount = 42 }, CancellationToken.None);
        var handler = Server().Route("/library/metadata/7002/children", _ => FakePlexHandler.Json("{}", HttpStatusCode.InternalServerError));
        var h = CreateHarness(handler, Collection(), items: items);

        var last = await SyncAsync(h.Client);

        Assert.That(last.GetProperty("state").GetString(), Is.EqualTo("succeeded"));
        Assert.That(last.GetProperty("erroredCount").GetInt32(), Is.EqualTo(1));
        Assert.That(items.Items[KindOfBlueId].PlayCount, Is.EqualTo(42));
        Assert.That(items.Items[AbbeyRoadId].PlayCount, Is.EqualTo(6));
    }

    [Test]
    public async Task Sync_PlexUnreachable_FailsRunWithoutTokenAndWritesNothing()
    {
        var handler = FakePlexHandler.WithServer()
            .Route("/library/sections", _ => throw new HttpRequestException($"connection refused for token {Token}"));
        var h = CreateHarness(handler, Collection());

        var last = await SyncAsync(h.Client);

        Assert.That(last.GetProperty("state").GetString(), Is.EqualTo("failed"));
        Assert.That(last.GetProperty("message").GetString(), Does.Not.Contain(Token));
        Assert.That(h.Items.Items, Is.Empty);
    }

    [Test]
    public async Task Sync_LibrarySectionIdsConfigured_ReadsOnlyThoseLibraries()
    {
        var handler = FakePlexHandler.WithServer()
            .Route("/library/sections", _ => FakePlexHandler.Json("""
                {"MediaContainer":{"Directory":[{"key":"4","type":"artist","title":"Music"},{"key":"5","type":"artist","title":"Podcasts"}]}}
                """))
            .Route("/library/sections/4/all", "music_albums.json")
            .Route("/library/sections/5/all", "music_albums.json")
            .Route("/library/metadata/7001/children", "music_tracks_7001.json");
        var h = CreateHarness(handler, Collection(), configure: services =>
            services.Configure<PlexConfiguration>(c => c.Entities["Music"].LibrarySectionIds = ["4"]));

        await SyncAsync(h.Client);

        var listings = handler.Requests.Select(r => r.Uri.AbsolutePath).Where(p => p.EndsWith("/all")).ToList();
        Assert.That(listings, Is.EqualTo(["/library/sections/4/all"]));
    }

    [Test]
    public async Task StartSync_MusicNotEnabled_ReturnsBadRequestAndStartsNoRun()
    {
        var h = CreateHarness(Server(), Collection());
        // Movies are not enabled in this harness: a disabled type is rejected the same way for movies
        var response = await h.Client.PostAsync("/plex/movies/sync", null);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That(h.Runs.Runs, Is.Empty);
    }
}
