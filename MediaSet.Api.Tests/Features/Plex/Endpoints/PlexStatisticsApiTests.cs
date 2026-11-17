using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MediaSet.Api.Features.Plex.Models;
using MediaSet.Api.Infrastructure.DataAccess;
using MediaSet.Api.Infrastructure.Database;
using MediaSet.Api.Shared.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration.EnvironmentVariables;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using NUnit.Framework;

namespace MediaSet.Api.Tests.Features.Plex.Endpoints;

/// <summary><c>GET /plex/statistics</c> over HTTP with in-memory Plex stores and a mocked movie service.</summary>
[TestFixture]
public class PlexStatisticsApiTests
{
    private const string SecretPath = "/data/movies/Secret Path/Movie.mkv";
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

    private HttpClient CreateClient(InMemoryPlexItemStore items, bool plexEnabled = true, bool moviesEnabled = true, bool musicEnabled = true)
    {
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
                builder.UseSetting("PlexConfiguration:Entities:Movies:Enabled", moviesEnabled ? "true" : "false");
                builder.UseSetting("PlexConfiguration:Entities:Music:Enabled", musicEnabled ? "true" : "false");
            }
            builder.ConfigureServices(services =>
            {
                var replaced = new[] { typeof(IDatabaseService), typeof(IEntityService<Book>), typeof(IEntityService<Movie>), typeof(IEntityService<Game>), typeof(IEntityService<Music>) };
                foreach (var d in services.Where(d => replaced.Contains(d.ServiceType)).ToList())
                {
                    services.Remove(d);
                }
                var movies = new List<Movie>
                {
                    new() { Id = "65a000000000000000000001", Title = "Heat" },
                    new() { Id = "65a000000000000000000002", Title = "Alien" },
                    new() { Id = "65a000000000000000000003", Title = "Severance", IsTvSeries = true }
                };
                var movieService = new Mock<IEntityService<Movie>>();
                movieService.Setup(s => s.GetListAsync(It.IsAny<CancellationToken>())).ReturnsAsync(movies);
                services.AddSingleton(new Mock<IDatabaseService>().Object);
                services.AddScoped(_ => movieService.Object);
                services.AddScoped(_ => new Mock<IEntityService<Book>>().Object);
                services.AddScoped(_ => new Mock<IEntityService<Game>>().Object);
                var albums = new List<Music>
                {
                    new() { Id = "65b000000000000000000001", Title = "Kind of Blue", Artist = "Miles Davis" },
                    new() { Id = "65b000000000000000000002", Title = "Blue Train", Artist = "John Coltrane" },
                    new() { Id = "65b000000000000000000003", Title = "Unlinked", Artist = "Nobody" }
                };
                var musicService = new Mock<IEntityService<Music>>();
                musicService.Setup(s => s.GetListAsync(It.IsAny<CancellationToken>())).ReturnsAsync(albums);
                services.AddScoped(_ => musicService.Object);
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

    private static async Task<InMemoryPlexItemStore> StoreAsync()
    {
        var items = new InMemoryPlexItemStore();
        await items.UpsertAsync(new PlexItemData
        {
            Id = "65a000000000000000000001",
            PlexItemId = "1",
            OnPlex = true,
            Resolution = "4k",
            VideoCodec = "hevc",
            Hdr = true,
            ViewCount = 3,
            Watched = true,
            SizeBytes = 5000,
            DurationSeconds = 6000,
            FilePath = SecretPath,
            LastSyncedAt = new DateTime(2024, 4, 1, 0, 0, 0, DateTimeKind.Utc)
        }, CancellationToken.None);
        await items.UpsertAsync(new PlexItemData
        {
            Id = "65a000000000000000000003",
            PlexItemId = "3",
            OnPlex = true,
            Scope = PlexScope.AllSeasons,
            Rollup = new PlexRollup { EpisodesPresent = 1, EpisodesExpected = 2 },
            Episodes = [new PlexEpisodeData { OnPlex = true, Resolution = "1080", FilePath = SecretPath }],
            LastSyncedAt = new DateTime(2024, 4, 1, 0, 0, 0, DateTimeKind.Utc)
        }, CancellationToken.None);
        await items.UpsertAsync(new PlexItemData
        {
            Id = "65b000000000000000000001",
            PlexItemId = "a1",
            EntityType = "music",
            OnPlex = true,
            PlayCount = 7,
            LastPlayedAt = new DateTime(2024, 5, 1, 0, 0, 0, DateTimeKind.Utc),
            DateAdded = new DateTime(2023, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            TracksPresent = 5,
            TracksExpected = 9,
            AudioCodec = "flac",
            Bitrate = 1100,
            SizeBytes = 4000,
            FilePath = SecretPath,
            LastSyncedAt = new DateTime(2024, 4, 1, 0, 0, 0, DateTimeKind.Utc)
        }, CancellationToken.None);
        await items.UpsertAsync(new PlexItemData
        {
            Id = "65b000000000000000000002",
            PlexItemId = "a2",
            EntityType = "music",
            OnPlex = true,
            PlayCount = 0,
            TracksPresent = 6,
            TracksExpected = 6,
            AudioCodec = "mp3",
            Bitrate = 320,
            SizeBytes = 2000,
            LastSyncedAt = new DateTime(2024, 4, 1, 0, 0, 0, DateTimeKind.Utc)
        }, CancellationToken.None);
        return items;
    }

    [Test]
    public async Task GetStatistics_PlexEnabled_ReturnsMovieAndTvSections()
    {
        using var client = CreateClient(await StoreAsync());

        var response = await client.GetAsync("/plex/statistics");
        var body = await response.Content.ReadAsStringAsync();

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        using var doc = JsonDocument.Parse(body);
        var movies = doc.RootElement.GetProperty("movies");
        Assert.That(movies.GetProperty("coverage").GetProperty("total").GetInt32(), Is.EqualTo(2));
        Assert.That(movies.GetProperty("coverage").GetProperty("onPlex").GetInt32(), Is.EqualTo(1));
        Assert.That(movies.GetProperty("resolution")[0].GetProperty("name").GetString(), Is.EqualTo("4k"));
        Assert.That(movies.GetProperty("mostWatched")[0].GetProperty("title").GetString(), Is.EqualTo("Heat"));
        var tv = doc.RootElement.GetProperty("tv");
        Assert.That(tv.GetProperty("units").GetInt32(), Is.EqualTo(1));
        Assert.That(tv.GetProperty("completeness").GetProperty("episodesExpected").GetInt32(), Is.EqualTo(2));
    }

    [Test]
    public async Task GetStatistics_SectionsMusic_ReturnsOnlyMusicAndDoesNotReadMovies()
    {
        var items = await StoreAsync();
        using var client = CreateClient(items);

        var response = await client.GetAsync("/plex/statistics?sections=music");
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(doc.RootElement.GetProperty("music").GetProperty("coverage").GetProperty("total").GetInt32(), Is.EqualTo(3));
        Assert.That(doc.RootElement.GetProperty("movies").ValueKind, Is.EqualTo(JsonValueKind.Null));
        Assert.That(doc.RootElement.GetProperty("tv").ValueKind, Is.EqualTo(JsonValueKind.Null));
        Assert.That(items.MovieStatisticsReads, Is.EqualTo(0));
    }

    [Test]
    public async Task GetStatistics_SectionsMovies_ReturnsMoviesAndTvWithoutMusic()
    {
        using var client = CreateClient(await StoreAsync());

        var response = await client.GetAsync("/plex/statistics?sections=Movies");
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(doc.RootElement.GetProperty("movies").ValueKind, Is.EqualTo(JsonValueKind.Object));
        Assert.That(doc.RootElement.GetProperty("tv").ValueKind, Is.EqualTo(JsonValueKind.Object));
        Assert.That(doc.RootElement.GetProperty("music").ValueKind, Is.EqualTo(JsonValueKind.Null));
    }

    [Test]
    public async Task GetStatistics_SectionsMoviesAndMusic_ReturnsBoth()
    {
        using var client = CreateClient(await StoreAsync());

        var response = await client.GetAsync("/plex/statistics?sections=movies,music");
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.That(doc.RootElement.GetProperty("movies").ValueKind, Is.EqualTo(JsonValueKind.Object));
        Assert.That(doc.RootElement.GetProperty("music").ValueKind, Is.EqualTo(JsonValueKind.Object));
    }

    [TestCase("bogus")]
    [TestCase("music,tv")]
    [TestCase(",")]
    public async Task GetStatistics_UnknownSection_Returns400(string sections)
    {
        using var client = CreateClient(await StoreAsync());

        var response = await client.GetAsync($"/plex/statistics?sections={sections}");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task GetStatistics_SectionsMusicButMusicNotEnabled_ReturnsNoSections()
    {
        using var client = CreateClient(await StoreAsync(), musicEnabled: false);

        var response = await client.GetAsync("/plex/statistics?sections=music");
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(doc.RootElement.GetProperty("music").ValueKind, Is.EqualTo(JsonValueKind.Null));
        Assert.That(doc.RootElement.GetProperty("movies").ValueKind, Is.EqualTo(JsonValueKind.Null));
    }

    [Test]
    public async Task GetStatistics_PlexEnabled_ResponseHasNoFilePath()
    {
        using var client = CreateClient(await StoreAsync());

        var body = await (await client.GetAsync("/plex/statistics")).Content.ReadAsStringAsync();

        Assert.That(body, Does.Not.Contain("Secret Path"));
        Assert.That(body.ToLowerInvariant(), Does.Not.Contain("filepath"));
    }

    [Test]
    public async Task GetStatistics_PlexNotConfigured_Returns404()
    {
        using var client = CreateClient(new InMemoryPlexItemStore(), plexEnabled: false);

        var response = await client.GetAsync("/plex/statistics");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task GetStatistics_MusicEnabled_ReturnsMusicSectionWithEachFigure()
    {
        using var client = CreateClient(await StoreAsync());

        var response = await client.GetAsync("/plex/statistics");
        var body = await response.Content.ReadAsStringAsync();

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        using var doc = JsonDocument.Parse(body);
        var music = doc.RootElement.GetProperty("music");
        var coverage = music.GetProperty("coverage");
        Assert.That(coverage.GetProperty("total").GetInt32(), Is.EqualTo(3));
        Assert.That(coverage.GetProperty("onPlex").GetInt32(), Is.EqualTo(2));
        Assert.That(music.GetProperty("totalPlays").GetInt32(), Is.EqualTo(7));
        Assert.That(music.GetProperty("neverPlayed").GetInt32(), Is.EqualTo(1));
        Assert.That(music.GetProperty("mostPlayed")[0].GetProperty("title").GetString(), Is.EqualTo("Kind of Blue"));
        Assert.That(music.GetProperty("mostPlayed")[0].GetProperty("artist").GetString(), Is.EqualTo("Miles Davis"));
        Assert.That(music.GetProperty("mostPlayed")[0].GetProperty("playCount").GetInt32(), Is.EqualTo(7));
        Assert.That(music.GetProperty("recentlyPlayed")[0].GetProperty("id").GetString(), Is.EqualTo("65b000000000000000000001"));
        Assert.That(music.GetProperty("recentlyAdded").GetArrayLength(), Is.EqualTo(1));
        Assert.That(music.GetProperty("storage").GetProperty("totalBytes").GetInt64(), Is.EqualTo(6000));
        Assert.That(music.GetProperty("storage").GetProperty("averageBytes").GetInt64(), Is.EqualTo(3000));
        Assert.That(music.GetProperty("audioCodec").GetArrayLength(), Is.EqualTo(2));
        Assert.That(music.GetProperty("bitrate").GetArrayLength(), Is.EqualTo(2));
        var completeness = music.GetProperty("completeness");
        Assert.That(completeness.GetProperty("tracksPresent").GetInt32(), Is.EqualTo(11));
        Assert.That(completeness.GetProperty("tracksExpected").GetInt32(), Is.EqualTo(15));
        Assert.That(completeness.GetProperty("albumsPartial").GetInt32(), Is.EqualTo(1));
        Assert.That(body, Does.Not.Contain("Secret Path"));
    }

    [Test]
    public async Task GetStatistics_MusicEnabled_MovieAndTvSectionsAreUnchanged()
    {
        using var client = CreateClient(await StoreAsync());

        using var doc = JsonDocument.Parse(await client.GetStringAsync("/plex/statistics"));

        var movies = doc.RootElement.GetProperty("movies");
        Assert.That(movies.GetProperty("coverage").GetProperty("total").GetInt32(), Is.EqualTo(2));
        Assert.That(movies.GetProperty("coverage").GetProperty("onPlex").GetInt32(), Is.EqualTo(1));
        Assert.That(doc.RootElement.GetProperty("tv").GetProperty("units").GetInt32(), Is.EqualTo(1));
    }

    [Test]
    public async Task GetStatistics_MusicNotEnabled_OmitsMusicSection()
    {
        using var client = CreateClient(await StoreAsync(), musicEnabled: false);

        using var doc = JsonDocument.Parse(await client.GetStringAsync("/plex/statistics"));

        Assert.That(doc.RootElement.GetProperty("movies").ValueKind, Is.EqualTo(JsonValueKind.Object));
        Assert.That(doc.RootElement.TryGetProperty("music", out var music) && music.ValueKind != JsonValueKind.Null, Is.False);
    }

    [Test]
    public async Task GetStatistics_OnlyMusicEnabled_ReturnsMusicSectionWithoutMovies()
    {
        using var client = CreateClient(await StoreAsync(), moviesEnabled: false);

        var response = await client.GetAsync("/plex/statistics");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.That(doc.RootElement.GetProperty("music").GetProperty("coverage").GetProperty("onPlex").GetInt32(), Is.EqualTo(2));
        Assert.That(doc.RootElement.TryGetProperty("movies", out var movies) && movies.ValueKind != JsonValueKind.Null, Is.False);
        Assert.That(doc.RootElement.TryGetProperty("tv", out var tv) && tv.ValueKind != JsonValueKind.Null, Is.False);
    }

    [Test]
    public async Task GetStatistics_NoEntityTypeEnabled_Returns400()
    {
        using var client = CreateClient(new InMemoryPlexItemStore(), moviesEnabled: false, musicEnabled: false);

        var response = await client.GetAsync("/plex/statistics");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }
}
