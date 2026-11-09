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
using MediaSet.Api.Infrastructure.DataAccess;
using MediaSet.Api.Infrastructure.Database;
using MediaSet.Api.Shared.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration.EnvironmentVariables;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;
using Moq;
using NUnit.Framework;

namespace MediaSet.Api.Tests.Features.Plex.Endpoints;

/// <summary>
/// The per-movie Plex data endpoint, the movie delete cascade and edit isolation, over HTTP with in-memory
/// stores for Plex data and runs and a mocked movie service.
/// </summary>
[TestFixture]
public class PlexItemApiTests
{
    private const string MovieId = "65a000000000000000000001";
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

    private HttpClient CreateClient(InMemoryPlexItemStore items, Mock<IEntityService<Movie>> movies, bool plexEnabled = true)
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
                builder.UseSetting("PlexConfiguration:Entities:Movies:Enabled", "true");
            }
            builder.ConfigureServices(services =>
            {
                foreach (var d in services.Where(d => d.ServiceType == typeof(IDatabaseService) || d.ServiceType == typeof(IEntityService<Movie>)).ToList())
                {
                    services.Remove(d);
                }
                services.AddSingleton(new Mock<IDatabaseService>().Object);
                services.AddScoped(_ => movies.Object);
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

    private static PlexItemData Data() => new()
    {
        Id = MovieId,
        PlexItemId = "1001",
        OnPlex = true,
        Library = "Movies",
        Resolution = "4k",
        Hdr = true,
        VideoCodec = "hevc",
        BestAudioTrack = new PlexAudioTrackData("truehd", 8, "eng"),
        SubtitleLanguages = ["eng", "fra"],
        Watched = true,
        LastWatchedAt = new DateTime(2024, 3, 9, 16, 0, 0, DateTimeKind.Utc),
        ViewCount = 2,
        DateAdded = new DateTime(2023, 11, 14, 0, 0, 0, DateTimeKind.Utc),
        DeepLink = "http://plex.local:32400/web/index.html#!/server/abc/details?key=x",
        DurationSeconds = 8160,
        Container = "mkv",
        FilePath = SecretPath,
        SizeBytes = 42_000_000_000,
        LastSyncedAt = new DateTime(2024, 4, 1, 0, 0, 0, DateTimeKind.Utc)
    };

    private static Mock<IEntityService<Movie>> MovieService()
    {
        var movie = new Movie { Id = MovieId, Title = "Dune", ExternalId = "tmdbid:438631" };
        var mock = InMemoryPlexItemStore.MovieService(movie);
        mock.Setup(s => s.GetAsync(MovieId, It.IsAny<CancellationToken>())).ReturnsAsync(movie);
        mock.Setup(s => s.RemoveAsync(MovieId, It.IsAny<CancellationToken>())).ReturnsAsync(new DeleteResult.Acknowledged(1));
        mock.Setup(s => s.UpdateAsync(MovieId, It.IsAny<Movie>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ReplaceOneResult.Acknowledged(1, 1, null));
        return mock;
    }

    [Test]
    public async Task GetItem_LinkedMovie_ReturnsAllPlexDataIncludingFilePath()
    {
        var items = new InMemoryPlexItemStore();
        await items.UpsertAsync(Data(), CancellationToken.None);
        using var client = CreateClient(items, MovieService());

        var response = await client.GetAsync($"/plex/movies/items/{MovieId}");
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var json = doc.RootElement;

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(json.GetProperty("movieId").GetString(), Is.EqualTo(MovieId));
        Assert.That(json.GetProperty("plexItemId").GetString(), Is.EqualTo("1001"));
        Assert.That(json.GetProperty("onPlex").GetBoolean(), Is.True);
        Assert.That(json.GetProperty("library").GetString(), Is.EqualTo("Movies"));
        Assert.That(json.GetProperty("resolution").GetString(), Is.EqualTo("4k"));
        Assert.That(json.GetProperty("hdr").GetBoolean(), Is.True);
        Assert.That(json.GetProperty("videoCodec").GetString(), Is.EqualTo("hevc"));
        Assert.That(json.GetProperty("bestAudioTrack").GetProperty("codec").GetString(), Is.EqualTo("truehd"));
        Assert.That(json.GetProperty("bestAudioTrack").GetProperty("channels").GetInt32(), Is.EqualTo(8));
        Assert.That(json.GetProperty("subtitleLanguages").EnumerateArray().Select(e => e.GetString()), Is.EqualTo(["eng", "fra"]));
        Assert.That(json.GetProperty("watched").GetBoolean(), Is.True);
        Assert.That(json.GetProperty("viewCount").GetInt32(), Is.EqualTo(2));
        Assert.That(json.GetProperty("lastWatchedAt").GetDateTimeOffset(), Is.EqualTo(new DateTimeOffset(2024, 3, 9, 16, 0, 0, TimeSpan.Zero)));
        Assert.That(json.GetProperty("dateAdded").ValueKind, Is.EqualTo(JsonValueKind.String));
        Assert.That(json.GetProperty("deepLink").GetString(), Does.Contain("/web/index.html"));
        Assert.That(json.GetProperty("durationSeconds").GetInt64(), Is.EqualTo(8160));
        Assert.That(json.GetProperty("container").GetString(), Is.EqualTo("mkv"));
        Assert.That(json.GetProperty("filePath").GetString(), Is.EqualTo(SecretPath));
        Assert.That(json.GetProperty("sizeBytes").GetInt64(), Is.EqualTo(42_000_000_000));
        Assert.That(json.GetProperty("lastSyncedAt").ValueKind, Is.EqualTo(JsonValueKind.String));
    }

    [Test]
    public async Task GetItem_UnlinkedMovie_ReturnsNotFound()
    {
        using var client = CreateClient(new InMemoryPlexItemStore(), MovieService());

        var response = await client.GetAsync($"/plex/movies/items/{MovieId}");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task OtherResponses_WithStoredPlexData_NeverContainTheFilePath()
    {
        var items = new InMemoryPlexItemStore();
        await items.UpsertAsync(Data(), CancellationToken.None);
        using var client = CreateClient(items, MovieService());

        var bodies = new List<string>
        {
            await (await client.GetAsync("/plex/status")).Content.ReadAsStringAsync(),
            await (await client.GetAsync("/plex/movies/status")).Content.ReadAsStringAsync(),
            await (await client.GetAsync($"/Movies/{MovieId}")).Content.ReadAsStringAsync(),
            await (await client.GetAsync("/Movies")).Content.ReadAsStringAsync()
        };

        Assert.That(bodies, Has.All.Not.Contain("Secret Path"));
    }

    [Test]
    public async Task DeleteMovie_PlexEnabled_DeletesMovieThenItsPlexDocument()
    {
        var items = new InMemoryPlexItemStore();
        await items.UpsertAsync(Data(), CancellationToken.None);
        var movies = MovieService();
        using var client = CreateClient(items, movies);

        var response = await client.DeleteAsync($"/Movies/{MovieId}");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        movies.Verify(s => s.RemoveAsync(MovieId, It.IsAny<CancellationToken>()), Times.Once);
        Assert.That(items.Items, Is.Empty);
    }

    [Test]
    public async Task DeleteMovie_PlexCleanupFails_StillSucceeds()
    {
        var items = new InMemoryPlexItemStore { DeleteFailure = new InvalidOperationException("mongo down") };
        await items.UpsertAsync(Data(), CancellationToken.None);
        var movies = MovieService();
        using var client = CreateClient(items, movies);

        var response = await client.DeleteAsync($"/Movies/{MovieId}");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        movies.Verify(s => s.RemoveAsync(MovieId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task DeleteMovie_PlexNotConfigured_DeletesMovieWithoutTouchingPlexData()
    {
        var movies = MovieService();
        using var client = CreateClient(new InMemoryPlexItemStore(), movies, plexEnabled: false);

        var response = await client.DeleteAsync($"/Movies/{MovieId}");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        movies.Verify(s => s.RemoveAsync(MovieId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task EditMovie_WithLinkedPlexData_LeavesPlexDocumentUnchanged()
    {
        var items = new InMemoryPlexItemStore();
        var original = Data();
        await items.UpsertAsync(original, CancellationToken.None);
        var before = JsonSerializer.Serialize(original);
        var movies = MovieService();
        using var client = CreateClient(items, movies);

        var response = await client.PutAsJsonAsync($"/Movies/{MovieId}", new { id = MovieId, title = "Dune Part One", externalId = "tmdbid:1" });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        movies.Verify(s => s.UpdateAsync(MovieId, It.IsAny<Movie>(), It.IsAny<CancellationToken>()), Times.Once);
        Assert.That(items.Items.Keys, Is.EqualTo([MovieId]));
        Assert.That(JsonSerializer.Serialize(items.Items[MovieId]), Is.EqualTo(before));
    }
}
