using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MediaSet.Api.Features.Plex.Models;
using MediaSet.Api.Features.Plex.Services;
using MediaSet.Api.Infrastructure.DataAccess;
using MediaSet.Api.Infrastructure.Database;
using MediaSet.Api.Infrastructure.Plex;
using MediaSet.Api.Shared.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration.EnvironmentVariables;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;
using Moq;
using NUnit.Framework;

namespace MediaSet.Api.Tests.Features.Plex.Endpoints;

/// <summary>Music search, link and unlink (and the Plex cleanup on delete) over HTTP, with Plex served from fixtures.</summary>
[TestFixture]
public class PlexMusicLinkApiTests
{
    private const string Token = "super-secret-token-value";
    private const string MusicId = "65c000000000000000000001";
    private const string MissingMusicId = "65c0000000000000000000ff";
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

    private sealed record Harness(HttpClient Client, InMemoryPlexItemStore Items, Mock<IPlexCoverService<Music>> Covers, Mock<IEntityService<Music>> Music);

    private Harness CreateHarness(FakePlexHandler handler, bool musicEnabled = true, bool moviesEnabled = false, int? tracks = 3, InMemoryPlexItemStore? items = null)
    {
        items ??= new InMemoryPlexItemStore();
        var covers = new Mock<IPlexCoverService<Music>>();
        var backfill = new Mock<IMusicExternalIdBackfillService>();
        backfill.Setup(b => b.RunAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new ExternalIdBackfillResult(0, 0));
        var musicService = new Mock<IEntityService<Music>>();
        var music = new Music { Id = MusicId, Title = "Abbey Road", Artist = "The Beatles", Tracks = tracks ?? 0 };
        musicService.Setup(s => s.GetAsync(MusicId, It.IsAny<CancellationToken>())).ReturnsAsync(music);
        musicService.Setup(s => s.GetListAsync(It.IsAny<CancellationToken>())).ReturnsAsync([music]);
        musicService.Setup(s => s.RemoveAsync(MusicId, It.IsAny<CancellationToken>())).ReturnsAsync(new DeleteResult.Acknowledged(1));
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
            builder.UseSetting("PlexConfiguration:Entities:Music:Enabled", musicEnabled.ToString());
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
                services.AddScoped(_ => musicService.Object);
                services.AddScoped(_ => InMemoryPlexItemStore.MovieService().Object);
                services.AddScoped(_ => covers.Object);
                services.AddScoped(_ => backfill.Object);
                InMemoryPlexSyncRunStore.Replace(services);
                InMemoryPlexItemStore.Replace(services, items);
                services.AddHttpClient(nameof(IPlexClient)).ConfigurePrimaryHttpMessageHandler(() => handler);
            });
        });
        _factories.Add(factory);
        return new Harness(factory.CreateClient(), items, covers, musicService);
    }

    private static FakePlexHandler Server() => FakePlexHandler.WithServer()
        .Route("/library/sections/4/all", "music_albums.json")
        .Route("/library/metadata/7001", "music_album_7001.json")
        .Route("/library/metadata/7001/children", "music_tracks_7001.json")
        .Route("/library/metadata/1001", "metadata_1001.json");

    private static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response)
    {
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return doc.RootElement.Clone();
    }

    private static Task<HttpResponseMessage> LinkAsync(HttpClient client, string id, object body) =>
        client.PutAsJsonAsync($"/plex/music/items/{id}/link", body);

    [Test]
    public async Task Search_Music_ReturnsAlbumsWithArtistAndNoArtistItems()
    {
        var h = CreateHarness(Server());

        var response = await h.Client.GetAsync("/plex/music/search?query=abbey");
        var results = (await ReadJsonAsync(response)).EnumerateArray().ToList();
        var first = results.First();

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(results, Is.Not.Empty);
        Assert.That(first.GetProperty("title").GetString(), Is.EqualTo("Abbey Road"));
        Assert.That(first.GetProperty("plexItemId").GetString(), Is.EqualTo("7001"));
        Assert.That(first.GetProperty("type").GetString(), Is.EqualTo("album"));
        Assert.That(first.GetProperty("artist").GetString(), Is.EqualTo("The Beatles"));
        Assert.That(first.GetProperty("library").GetString(), Is.EqualTo("Music"));
    }

    [Test]
    public async Task Search_MusicShortQuery_Returns400()
    {
        var h = CreateHarness(Server());

        var response = await h.Client.GetAsync("/plex/music/search?query=a");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task Search_MusicDisabled_Returns400()
    {
        var h = CreateHarness(Server(), musicEnabled: false, moviesEnabled: true);

        var response = await h.Client.GetAsync("/plex/music/search?query=abbey");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task Search_PlexDown_Returns502()
    {
        var handler = FakePlexHandler.WithServer().Route("/library/sections/4/all", _ => new HttpResponseMessage(HttpStatusCode.InternalServerError));
        var h = CreateHarness(handler);

        var response = await h.Client.GetAsync("/plex/music/search?query=abbey");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadGateway));
    }

    [Test]
    public async Task Link_Album_StoresManualLinkWithRollupAndExpectedTracks()
    {
        var h = CreateHarness(Server());

        var response = await LinkAsync(h.Client, MusicId, new { plexItemId = "7001" });
        var json = await ReadJsonAsync(response);
        var stored = h.Items.Items[MusicId];

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(json.GetProperty("plexItemId").GetString(), Is.EqualTo("7001"));
        Assert.That(json.GetProperty("linkSource").GetString(), Is.EqualTo("manual"));
        Assert.That(json.GetProperty("onPlex").GetBoolean(), Is.True);
        Assert.That(stored.EntityType, Is.EqualTo(PlexEntityTypes.Music));
        Assert.That(stored.LinkSource, Is.EqualTo(PlexLinkSources.Manual));
        Assert.That(stored.TracksPresent, Is.EqualTo(3));
        Assert.That(stored.TracksExpected, Is.EqualTo(3));
        Assert.That(stored.PlayCount, Is.GreaterThan(0));
        Assert.That(stored.Library, Is.EqualTo("Music"));
    }

    [Test]
    public async Task Link_Album_AsksForPlexAlbumImageAsCover()
    {
        var h = CreateHarness(Server());

        await LinkAsync(h.Client, MusicId, new { plexItemId = "7001" });

        h.Covers.Verify(c => c.ApplyIfMissingAsync(MusicId, "7001", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task Link_CoverServiceFindsNothing_StillLinks()
    {
        var h = CreateHarness(Server());
        h.Covers.Setup(c => c.ApplyIfMissingAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var response = await LinkAsync(h.Client, MusicId, new { plexItemId = "7001" });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(h.Items.Items, Contains.Key(MusicId));
    }

    [Test]
    public async Task Link_ReplacesAnEarlierAutomaticLink()
    {
        var items = new InMemoryPlexItemStore();
        await items.UpsertAsync(new PlexItemData { Id = MusicId, PlexItemId = "7099", EntityType = PlexEntityTypes.Music, OnPlex = true, LinkSource = PlexLinkSources.Auto }, CancellationToken.None);
        var h = CreateHarness(Server(), items: items);

        var response = await LinkAsync(h.Client, MusicId, new { plexItemId = "7001" });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(items.Items[MusicId].PlexItemId, Is.EqualTo("7001"));
        Assert.That(items.Items[MusicId].LinkSource, Is.EqualTo(PlexLinkSources.Manual));
    }

    [Test]
    public async Task Link_UnknownPlexItem_Returns404AndStoresNothing()
    {
        var h = CreateHarness(Server());

        var response = await LinkAsync(h.Client, MusicId, new { plexItemId = "9999" });
        var json = await ReadJsonAsync(response);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        Assert.That(json.GetProperty("title").GetString(), Is.EqualTo("Plex item not found"));
        Assert.That(h.Items.Items, Is.Empty);
        h.Covers.Verify(c => c.ApplyIfMissingAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Link_PlexItemIsNotAnAlbum_Returns404AndStoresNothing()
    {
        var h = CreateHarness(Server());

        var response = await LinkAsync(h.Client, MusicId, new { plexItemId = "1001" });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        Assert.That(h.Items.Items, Is.Empty);
    }

    [Test]
    public async Task Link_MissingPlexItemId_Returns400()
    {
        var h = CreateHarness(Server());

        var response = await LinkAsync(h.Client, MusicId, new { });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task Link_ScopeForMusic_Returns400()
    {
        var h = CreateHarness(Server());

        var response = await LinkAsync(h.Client, MusicId, new { plexItemId = "7001", scope = new { kind = "all" } });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That(h.Items.Items, Is.Empty);
    }

    [Test]
    public async Task Link_UnknownOrMalformedMusicId_Returns404()
    {
        var h = CreateHarness(Server());

        var unknown = await LinkAsync(h.Client, MissingMusicId, new { plexItemId = "7001" });
        var malformed = await LinkAsync(h.Client, "not-an-id", new { plexItemId = "7001" });

        Assert.That(unknown.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        Assert.That(malformed.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task Link_PlexDown_Returns502AndStoresNothing()
    {
        var handler = FakePlexHandler.WithServer().Route("/library/metadata/7001", _ => new HttpResponseMessage(HttpStatusCode.InternalServerError));
        var h = CreateHarness(handler);

        var response = await LinkAsync(h.Client, MusicId, new { plexItemId = "7001" });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadGateway));
        Assert.That(h.Items.Items, Is.Empty);
    }

    [Test]
    public async Task Link_MusicDisabled_Returns400()
    {
        var h = CreateHarness(Server(), musicEnabled: false, moviesEnabled: true);

        var response = await LinkAsync(h.Client, MusicId, new { plexItemId = "7001" });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task Unlink_LinkedMusic_RemovesStoredData()
    {
        var items = new InMemoryPlexItemStore();
        await items.UpsertAsync(new PlexItemData { Id = MusicId, PlexItemId = "7001", EntityType = PlexEntityTypes.Music, OnPlex = true, LinkSource = PlexLinkSources.Manual }, CancellationToken.None);
        var h = CreateHarness(Server(), items: items);

        var response = await h.Client.DeleteAsync($"/plex/music/items/{MusicId}/link");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
        Assert.That(items.Items, Is.Empty);
    }

    [Test]
    public async Task Unlink_NotLinked_Returns404()
    {
        var h = CreateHarness(Server());

        var response = await h.Client.DeleteAsync($"/plex/music/items/{MusicId}/link");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task Unlink_UnknownMusic_Returns404()
    {
        var h = CreateHarness(Server());

        var response = await h.Client.DeleteAsync($"/plex/music/items/{MissingMusicId}/link");
        var json = await ReadJsonAsync(response);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        Assert.That(json.GetProperty("title").GetString(), Is.EqualTo("Music not found"));
    }

    [Test]
    public async Task ManualLink_IsKeptByALaterSync()
    {
        // The entry has no ExternalId, so only the manual link can keep it on Plex
        var h = CreateHarness(Server().Route("/library/metadata/7002/children", "music_tracks_7002.json"));
        await LinkAsync(h.Client, MusicId, new { plexItemId = "7001" });

        var start = await h.Client.PostAsync("/plex/music/sync", null);
        Assert.That(start.StatusCode, Is.EqualTo(HttpStatusCode.Accepted));
        for (var i = 0; i < 100; i++)
        {
            var status = await ReadJsonAsync(await h.Client.GetAsync("/plex/music/status"));
            if (status.GetProperty("lastRun").ValueKind != JsonValueKind.Null && status.GetProperty("lastRun").GetProperty("state").GetString() is "succeeded" or "failed")
            {
                break;
            }
            await Task.Delay(100);
        }

        Assert.That(h.Items.Items[MusicId].LinkSource, Is.EqualTo(PlexLinkSources.Manual));
        Assert.That(h.Items.Items[MusicId].PlexItemId, Is.EqualTo("7001"));
        Assert.That(h.Items.Items[MusicId].OnPlex, Is.True);
    }

    [Test]
    public async Task DeleteMusic_RemovesItsPlexData()
    {
        var items = new InMemoryPlexItemStore();
        await items.UpsertAsync(new PlexItemData { Id = MusicId, PlexItemId = "7001", EntityType = PlexEntityTypes.Music, OnPlex = true }, CancellationToken.None);
        var h = CreateHarness(Server(), items: items);

        var response = await h.Client.DeleteAsync($"/Musics/{MusicId}");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(items.Items, Is.Empty);
    }

    [Test]
    public async Task DeleteMusic_PlexCleanupFails_StillDeletes()
    {
        var items = new InMemoryPlexItemStore { DeleteFailure = new InvalidOperationException("boom") };
        await items.UpsertAsync(new PlexItemData { Id = MusicId, PlexItemId = "7001", EntityType = PlexEntityTypes.Music, OnPlex = true }, CancellationToken.None);
        var h = CreateHarness(Server(), items: items);

        var response = await h.Client.DeleteAsync($"/Musics/{MusicId}");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        h.Music.Verify(s => s.RemoveAsync(MusicId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task DeleteMusic_MusicPlexDisabled_LeavesStoredPlexDataAlone()
    {
        var items = new InMemoryPlexItemStore();
        await items.UpsertAsync(new PlexItemData { Id = MusicId, PlexItemId = "7001", EntityType = PlexEntityTypes.Music, OnPlex = true }, CancellationToken.None);
        var h = CreateHarness(Server(), musicEnabled: false, moviesEnabled: true, items: items);

        var response = await h.Client.DeleteAsync($"/Musics/{MusicId}");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(items.Items, Contains.Key(MusicId));
    }
}
