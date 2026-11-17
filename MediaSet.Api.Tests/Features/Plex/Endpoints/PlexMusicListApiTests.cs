using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MediaSet.Api.Features.Plex.Models;
using MediaSet.Api.Infrastructure.DataAccess;
using MediaSet.Api.Infrastructure.Database;
using MediaSet.Api.Infrastructure.Plex;
using MediaSet.Api.Shared.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration.EnvironmentVariables;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using NUnit.Framework;

namespace MediaSet.Api.Tests.Features.Plex.Endpoints;

/// <summary>
/// The Plex summary, the single-lookup Plex data and the <c>plex</c> filter on the music endpoints over HTTP, with
/// in-memory Plex stores and a music service that applies the id filter the endpoint passes it.
/// </summary>
[TestFixture]
public class PlexMusicListApiTests
{
    private const string PlayedId = "65d000000000000000000001";
    private const string NeverPlayedId = "65d000000000000000000002";
    private const string PartialId = "65d000000000000000000003";
    private const string UnlinkedId = "65d000000000000000000004";
    private const string MovieDocId = "65d000000000000000000005";
    private const string SecretPath = "/data/music/Secret/Track.flac";
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

    private HttpClient CreateClient(InMemoryPlexItemStore items, bool plexEnabled = true, bool musicEnabled = true)
    {
        var music = MusicService();
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
            if (plexEnabled)
            {
                builder.UseSetting("PlexConfiguration:BaseUrl", "http://plex.local:32400");
                builder.UseSetting("PlexConfiguration:Token", "token");
                builder.UseSetting("PlexConfiguration:Entities:Music:Enabled", musicEnabled ? "true" : "false");
                builder.UseSetting("PlexConfiguration:Entities:Movies:Enabled", "true");
            }
            builder.ConfigureServices(services =>
            {
                foreach (var d in services.Where(d => d.ServiceType == typeof(IDatabaseService)
                    || d.ServiceType == typeof(IEntityService<Music>)
                    || d.ServiceType == typeof(IEntityService<Movie>)).ToList())
                {
                    services.Remove(d);
                }
                services.AddSingleton(new Mock<IDatabaseService>().Object);
                services.AddScoped(_ => music.Object);
                services.AddScoped(_ => InMemoryPlexItemStore.MovieService().Object);
                if (plexEnabled)
                {
                    InMemoryPlexSyncRunStore.Replace(services);
                    InMemoryPlexItemStore.Replace(services, items);
                }
            });
        });
        _factories.Add(factory);
        return factory.CreateClient();
    }

    private static readonly string[] _allIds = [PlayedId, NeverPlayedId, PartialId, UnlinkedId];

    /// <summary>A music service whose paged search applies the id filter like the real one does and whose lookup returns an album.</summary>
    private static Mock<IEntityService<Music>> MusicService()
    {
        var mock = new Mock<IEntityService<Music>>();
        mock.Setup(s => s.PagedSearchAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<IdFilter?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string _, string _, int page, int pageSize, IdFilter? filter, CancellationToken _) =>
            {
                var albums = _allIds
                    .Where(id => filter == null || filter.Ids.Contains(id) != filter.Exclude)
                    .Select(id => new Music { Id = id, Title = "Album " + id, Artist = "Artist" })
                    .ToList();
                return new PagedResult<Music>(albums, albums.Count, page, pageSize);
            });
        mock.Setup(s => s.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string id, CancellationToken _) => new Music { Id = id, Title = "Album " + id, Artist = "Artist" });
        return mock;
    }

    private static InMemoryPlexItemStore Store()
    {
        var items = new InMemoryPlexItemStore();
        var synced = new DateTime(2024, 4, 1, 0, 0, 0, DateTimeKind.Utc);
        var played = new DateTime(2024, 3, 20, 12, 0, 0, DateTimeKind.Utc);
        items.UpsertAsync(new PlexItemData
        {
            Id = PlayedId,
            PlexItemId = "7001",
            EntityType = PlexEntityTypes.Music,
            OnPlex = true,
            PlayCount = 12,
            LastPlayedAt = played,
            TracksPresent = 10,
            TracksExpected = 10,
            AudioCodec = "flac",
            Bitrate = 900,
            SizeBytes = 1_000_000,
            FilePath = SecretPath,
            DeepLink = "http://plex.local:32400/web/index.html#!/server/abc/details?key=%2Flibrary%2Fmetadata%2F7001",
            LastSyncedAt = synced
        }, CancellationToken.None).Wait();
        items.UpsertAsync(new PlexItemData
        {
            Id = NeverPlayedId,
            PlexItemId = "7002",
            EntityType = PlexEntityTypes.Music,
            OnPlex = true,
            PlayCount = 0,
            TracksPresent = 8,
            TracksExpected = 8,
            LastSyncedAt = synced
        }, CancellationToken.None).Wait();
        items.UpsertAsync(new PlexItemData
        {
            Id = PartialId,
            PlexItemId = "7003",
            EntityType = PlexEntityTypes.Music,
            OnPlex = true,
            PlayCount = 3,
            TracksPresent = 7,
            TracksExpected = 10,
            LastSyncedAt = synced
        }, CancellationToken.None).Wait();
        // A movie document must never surface on the music list or filter
        items.UpsertAsync(new PlexItemData { Id = MovieDocId, PlexItemId = "1", OnPlex = true, LastSyncedAt = synced }, CancellationToken.None).Wait();
        return items;
    }

    private static async Task<(HttpStatusCode Status, string Body, JsonElement Json)> GetAsync(HttpClient client, string path)
    {
        var response = await client.GetAsync(path);
        var body = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        return (response.StatusCode, body, doc.RootElement.Clone());
    }

    private static Task<(HttpStatusCode Status, string Body, JsonElement Json)> ListAsync(HttpClient client, string query) =>
        GetAsync(client, "/musics/pagedsearch?searchText=&orderBy=title:asc" + query);

    private static string[] Ids(JsonElement json) =>
        [.. json.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("id").GetString()!)];

    private static JsonElement Item(JsonElement json, string id) =>
        json.GetProperty("items").EnumerateArray().Single(i => i.GetProperty("id").GetString() == id);

    [Test]
    public async Task PagedSearch_MusicEnabled_AttachesSummaryToLinkedAlbumsOnly()
    {
        using var client = CreateClient(Store());

        var (status, _, json) = await ListAsync(client, "");

        Assert.That(status, Is.EqualTo(HttpStatusCode.OK));
        var played = Item(json, PlayedId).GetProperty("plex");
        Assert.That(played.GetProperty("onPlex").GetBoolean(), Is.True);
        Assert.That(played.GetProperty("playCount").GetInt32(), Is.EqualTo(12));
        Assert.That(played.GetProperty("lastPlayedAt").GetDateTimeOffset(), Is.EqualTo(new DateTimeOffset(2024, 3, 20, 12, 0, 0, TimeSpan.Zero)));
        Assert.That(played.GetProperty("tracksPresent").GetInt32(), Is.EqualTo(10));
        Assert.That(played.GetProperty("tracksExpected").GetInt32(), Is.EqualTo(10));
        var never = Item(json, NeverPlayedId).GetProperty("plex");
        Assert.That(never.GetProperty("playCount").GetInt32(), Is.EqualTo(0));
        Assert.That(never.GetProperty("lastPlayedAt").ValueKind, Is.EqualTo(JsonValueKind.Null));
        var partial = Item(json, PartialId).GetProperty("plex");
        Assert.That(partial.GetProperty("tracksPresent").GetInt32(), Is.EqualTo(7));
        Assert.That(partial.GetProperty("tracksExpected").GetInt32(), Is.EqualTo(10));
        Assert.That(Item(json, UnlinkedId).TryGetProperty("plex", out _), Is.False);
    }

    [Test]
    public async Task PagedSearch_MusicEnabled_NeverExposesFilePaths()
    {
        using var client = CreateClient(Store());

        var (_, body, _) = await ListAsync(client, "");

        Assert.That(body, Does.Not.Contain(SecretPath));
        Assert.That(body, Does.Not.Contain("filePath"));
    }

    [Test]
    public async Task PagedSearch_MusicEnabled_JoinsTheWholePageInOneQuery()
    {
        var items = Store();
        using var client = CreateClient(items);

        await ListAsync(client, "");

        Assert.That(items.SummaryReads, Is.EqualTo(1));
    }

    [Test]
    public async Task PagedSearch_PlexNotConfigured_HasNoPlexProperty()
    {
        using var client = CreateClient(Store(), plexEnabled: false);

        var (status, _, json) = await ListAsync(client, "");

        Assert.That(status, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(json.GetProperty("items").EnumerateArray().Any(i => i.TryGetProperty("plex", out _)), Is.False);
    }

    [Test]
    public async Task PagedSearch_MusicNotEnabledForPlex_HasNoPlexProperty()
    {
        using var client = CreateClient(Store(), musicEnabled: false);

        var (_, _, json) = await ListAsync(client, "");

        Assert.That(json.GetProperty("items").EnumerateArray().Any(i => i.TryGetProperty("plex", out _)), Is.False);
    }

    [Test]
    public async Task PagedSearch_SummaryReadFails_ReturnsListWithoutSummaries()
    {
        var items = Store();
        items.SummariesFailure = new InvalidOperationException("mongo down");
        using var client = CreateClient(items);

        var (status, _, json) = await ListAsync(client, "");

        Assert.That(status, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(Ids(json), Has.Length.EqualTo(_allIds.Length));
        Assert.That(json.GetProperty("items").EnumerateArray().Any(i => i.TryGetProperty("plex", out _)), Is.False);
    }

    [TestCase("onPlex", new[] { PlayedId, NeverPlayedId, PartialId })]
    [TestCase("ONPLEX", new[] { PlayedId, NeverPlayedId, PartialId })]
    [TestCase("notOnPlex", new[] { UnlinkedId })]
    public async Task PagedSearch_PlexFilter_ReturnsOnlyMatchingAlbums(string filter, string[] expected)
    {
        using var client = CreateClient(Store());

        var (status, _, json) = await ListAsync(client, $"&plex={filter}");

        Assert.That(status, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(Ids(json), Is.EquivalentTo(expected));
    }

    [Test]
    public async Task PagedSearch_BrokenFilter_ReturnsOnlyAlbumsWithABrokenLink()
    {
        var items = Store();
        items.Items[PartialId].Broken = true;
        using var client = CreateClient(items);

        var (status, _, json) = await ListAsync(client, "&plex=broken");

        Assert.That(status, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(Ids(json), Is.EquivalentTo([PartialId]));
    }

    [Test]
    public async Task PagedSearch_BrokenFilterIgnoresBrokenMovieDocuments()
    {
        var items = Store();
        // A broken movie document: it must never leak into the music list, whatever its id
        await items.UpsertAsync(new PlexItemData { Id = UnlinkedId, PlexItemId = "1", OnPlex = true, Broken = true }, CancellationToken.None);
        using var client = CreateClient(items);

        var (status, _, json) = await ListAsync(client, "&plex=broken");

        Assert.That(status, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(Ids(json), Is.Empty);
    }

    [Test]
    public void MusicFilters_AreExactlyTheOnesTheListPageOffers()
    {
        // The UI offers these in PLEX_MUSIC_FILTERS (MediaSet.Remix/app/models.ts); the API must accept each of them and nothing else
        var offered = ReadUiMusicFilters();

        Assert.That(offered, Is.Not.Empty);
        foreach (var filter in offered)
        {
            Assert.That(PlexListFilters.NormalizeMusic(filter), Is.EqualTo(filter), $"The API rejects the UI music filter '{filter}'");
        }
        var accepted = new[] { PlexListFilters.OnPlex, PlexListFilters.NotOnPlex, PlexListFilters.Unwatched, PlexListFilters.Broken }
            .Where(f => PlexListFilters.NormalizeMusic(f) != null);
        Assert.That(accepted, Is.EquivalentTo(offered), "The UI and the API music filters have drifted apart");
    }

    private static string[] ReadUiMusicFilters()
    {
        var dir = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "MediaSet.sln")))
        {
            dir = dir.Parent;
        }
        Assert.That(dir, Is.Not.Null, "Repository root not found");
        var models = File.ReadAllText(Path.Combine(dir!.FullName, "MediaSet.Remix", "app", "models.ts"));
        var match = System.Text.RegularExpressions.Regex.Match(models, @"PLEX_MUSIC_FILTERS\s*=\s*\[([^\]]*)\]");
        Assert.That(match.Success, Is.True, "PLEX_MUSIC_FILTERS not found in models.ts");
        return [.. System.Text.RegularExpressions.Regex.Matches(match.Groups[1].Value, "'([^']+)'").Select(m => m.Groups[1].Value)];
    }

    [TestCase("unwatched")]
    [TestCase("bogus")]
    public async Task PagedSearch_UnsupportedMusicPlexFilter_ReturnsBadRequest(string filter)
    {
        using var client = CreateClient(Store());

        var (status, _, _) = await ListAsync(client, $"&plex={filter}");

        Assert.That(status, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task PagedSearch_PlexFilterButMusicNotEnabled_IsIgnored()
    {
        using var client = CreateClient(Store(), musicEnabled: false);

        var (status, _, json) = await ListAsync(client, "&plex=onPlex");

        Assert.That(status, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(Ids(json), Has.Length.EqualTo(_allIds.Length));
    }

    [Test]
    public async Task GetMusic_LinkedAlbum_ReturnsPlexDataWithoutFilePath()
    {
        using var client = CreateClient(Store());

        var (status, body, json) = await GetAsync(client, $"/musics/{PlayedId}");

        Assert.That(status, Is.EqualTo(HttpStatusCode.OK));
        var plex = json.GetProperty("plex");
        Assert.That(plex.GetProperty("playCount").GetInt32(), Is.EqualTo(12));
        Assert.That(plex.GetProperty("audioCodec").GetString(), Is.EqualTo("flac"));
        Assert.That(plex.GetProperty("bitrate").GetInt32(), Is.EqualTo(900));
        Assert.That(plex.GetProperty("sizeBytes").GetInt64(), Is.EqualTo(1_000_000));
        Assert.That(plex.GetProperty("deepLink").GetString(), Does.StartWith("http://plex.local"));
        Assert.That(body, Does.Not.Contain(SecretPath));
    }

    [Test]
    public async Task GetMusic_UnlinkedAlbum_HasNoPlexProperty()
    {
        using var client = CreateClient(Store());

        var (_, _, json) = await GetAsync(client, $"/musics/{UnlinkedId}");

        Assert.That(json.TryGetProperty("plex", out _), Is.False);
    }

    [Test]
    public async Task GetMusic_MusicNotEnabledForPlex_HasNoPlexProperty()
    {
        using var client = CreateClient(Store(), musicEnabled: false);

        var (_, _, json) = await GetAsync(client, $"/musics/{PlayedId}");

        Assert.That(json.TryGetProperty("plex", out _), Is.False);
    }

    [Test]
    public async Task GetMusic_IdOfAMovieDocument_HasNoPlexProperty()
    {
        using var client = CreateClient(Store());

        var (_, _, json) = await GetAsync(client, $"/musics/{MovieDocId}");

        Assert.That(json.TryGetProperty("plex", out _), Is.False);
    }
}
