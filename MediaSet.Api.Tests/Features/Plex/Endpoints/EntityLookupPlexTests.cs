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
/// The Plex data on the single movie lookup (GET /movies/{id}) over HTTP, with in-memory Plex stores and mocked
/// entity services.
/// </summary>
[TestFixture]
public class EntityLookupPlexTests
{
    private const string MovieId = "65a000000000000000000001";
    private const string TvId = "65a000000000000000000002";
    private const string BookId = "65a000000000000000000003";
    private const string MoviePath = "/data/movies/Secret Path/Movie.mkv";
    private const string EpisodePath = "/data/tv/Secret Show/S01E01.mkv";
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

    private HttpClient CreateClient(InMemoryPlexItemStore items, Mock<IEntityService<Movie>> movies, Mock<IEntityService<Book>>? books = null, bool plexEnabled = true)
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
                foreach (var d in services.Where(d => d.ServiceType == typeof(IDatabaseService)
                    || d.ServiceType == typeof(IEntityService<Movie>)
                    || d.ServiceType == typeof(IEntityService<Book>)).ToList())
                {
                    services.Remove(d);
                }
                services.AddSingleton(new Mock<IDatabaseService>().Object);
                services.AddScoped(_ => movies.Object);
                services.AddScoped(_ => (books ?? new Mock<IEntityService<Book>>()).Object);
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

    private static Mock<IEntityService<Movie>> MovieService()
    {
        var mock = new Mock<IEntityService<Movie>>();
        foreach (var id in new[] { MovieId, TvId })
        {
            var movie = new Movie { Id = id, Title = "Title " + id, IsTvSeries = id == TvId };
            mock.Setup(s => s.GetAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync(movie);
        }
        mock.Setup(s => s.GetAsync("not-an-object-id", It.IsAny<CancellationToken>())).ReturnsAsync(new Movie { Id = "not-an-object-id", Title = "Odd" });
        return mock;
    }

    private static PlexItemData MovieData() => new()
    {
        Id = MovieId,
        PlexItemId = "1001",
        OnPlex = true,
        Library = "Movies",
        Resolution = "4k",
        Hdr = true,
        SubtitleLanguages = ["eng"],
        FilePath = MoviePath,
        LastSyncedAt = new DateTime(2024, 4, 1, 0, 0, 0, DateTimeKind.Utc)
    };

    private static PlexItemData TvData() => new()
    {
        Id = TvId,
        PlexItemId = "2001",
        OnPlex = true,
        Scope = PlexScope.Single(1),
        Seasons = [new PlexSeasonData { Index = 1, PlexSeasonId = "2101", EpisodeCount = 1 }],
        Episodes = [new PlexEpisodeData { SeasonNumber = 1, EpisodeNumber = 1, Title = "One", FilePath = EpisodePath }],
        FilePath = MoviePath,
        LastSyncedAt = new DateTime(2024, 4, 1, 0, 0, 0, DateTimeKind.Utc)
    };

    private static async Task<(HttpStatusCode Status, string Body, JsonElement Json)> GetAsync(HttpClient client, string url)
    {
        var response = await client.GetAsync(url);
        var body = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        return (response.StatusCode, body, doc.RootElement.Clone());
    }

    [Test]
    public async Task GetMovie_LinkedToPlex_IncludesPlexDataWithoutFilePath()
    {
        var items = new InMemoryPlexItemStore();
        await items.UpsertAsync(MovieData(), CancellationToken.None);
        using var client = CreateClient(items, MovieService());

        var (status, body, json) = await GetAsync(client, $"/movies/{MovieId}");

        Assert.That(status, Is.EqualTo(HttpStatusCode.OK));
        var plex = json.GetProperty("plex");
        Assert.That(plex.GetProperty("plexItemId").GetString(), Is.EqualTo("1001"));
        Assert.That(plex.GetProperty("resolution").GetString(), Is.EqualTo("4k"));
        Assert.That(plex.GetProperty("hdr").GetBoolean(), Is.True);
        Assert.That(plex.GetProperty("filePath").ValueKind, Is.EqualTo(JsonValueKind.Null));
        Assert.That(body, Does.Not.Contain(MoviePath));
    }

    [Test]
    public async Task GetMovie_LinkedTvEntry_OmitsFilePathsOfItemAndEpisodes()
    {
        var items = new InMemoryPlexItemStore();
        await items.UpsertAsync(TvData(), CancellationToken.None);
        using var client = CreateClient(items, MovieService());

        var (_, body, json) = await GetAsync(client, $"/movies/{TvId}");

        var episodes = json.GetProperty("plex").GetProperty("episodes");
        Assert.That(episodes.GetArrayLength(), Is.EqualTo(1));
        Assert.That(episodes[0].GetProperty("title").GetString(), Is.EqualTo("One"));
        Assert.That(episodes[0].GetProperty("filePath").ValueKind, Is.EqualTo(JsonValueKind.Null));
        Assert.That(body, Does.Not.Contain(EpisodePath));
        Assert.That(body, Does.Not.Contain(MoviePath));
    }

    [Test]
    public async Task GetMovie_NotLinkedToPlex_HasNoPlexProperty()
    {
        using var client = CreateClient(new InMemoryPlexItemStore(), MovieService());

        var (status, _, json) = await GetAsync(client, $"/movies/{MovieId}");

        Assert.That(status, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(json.TryGetProperty("plex", out _), Is.False);
    }

    [Test]
    public async Task GetMovie_NonObjectId_HasNoPlexProperty()
    {
        using var client = CreateClient(new InMemoryPlexItemStore(), MovieService());

        var (status, _, json) = await GetAsync(client, "/movies/not-an-object-id");

        Assert.That(status, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(json.TryGetProperty("plex", out _), Is.False);
    }

    [Test]
    public async Task GetMovie_PlexNotConfigured_ResponseUnchanged()
    {
        var items = new InMemoryPlexItemStore();
        await items.UpsertAsync(MovieData(), CancellationToken.None);
        using var client = CreateClient(items, MovieService(), plexEnabled: false);

        var (status, _, json) = await GetAsync(client, $"/movies/{MovieId}");

        Assert.That(status, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(json.GetProperty("title").GetString(), Is.EqualTo("Title " + MovieId));
        Assert.That(json.TryGetProperty("plex", out _), Is.False);
    }

    [Test]
    public async Task GetPlexItem_LinkedMovie_StillReturnsFilePath()
    {
        var items = new InMemoryPlexItemStore();
        await items.UpsertAsync(MovieData(), CancellationToken.None);
        await items.UpsertAsync(TvData(), CancellationToken.None);
        using var client = CreateClient(items, MovieService());

        var (_, _, movie) = await GetAsync(client, $"/plex/movies/items/{MovieId}");
        var (_, _, tv) = await GetAsync(client, $"/plex/movies/items/{TvId}");

        Assert.That(movie.GetProperty("filePath").GetString(), Is.EqualTo(MoviePath));
        Assert.That(tv.GetProperty("episodes")[0].GetProperty("filePath").GetString(), Is.EqualTo(EpisodePath));
    }

    [Test]
    public async Task GetBook_PlexEnabled_IsUnaffected()
    {
        var items = new InMemoryPlexItemStore();
        await items.UpsertAsync(new PlexItemData { Id = BookId, PlexItemId = "9", LastSyncedAt = DateTime.UtcNow }, CancellationToken.None);
        var books = new Mock<IEntityService<Book>>();
        books.Setup(s => s.GetAsync(BookId, It.IsAny<CancellationToken>())).ReturnsAsync(new Book { Id = BookId, Title = "A book" });
        using var client = CreateClient(items, MovieService(), books);

        var (status, _, json) = await GetAsync(client, $"/books/{BookId}");

        Assert.That(status, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(json.GetProperty("title").GetString(), Is.EqualTo("A book"));
        Assert.That(json.TryGetProperty("plex", out _), Is.False);
    }

    [Test]
    public async Task UpdateMovie_BodyWithPlex_DropsPlexBeforeSaving()
    {
        var movies = MovieService();
        Movie? saved = null;
        movies.Setup(s => s.UpdateAsync(MovieId, It.IsAny<Movie>(), It.IsAny<CancellationToken>()))
            .Callback<string, Movie, CancellationToken>((_, m, _) => saved = m)
            .ReturnsAsync(new ReplaceOneResult.Acknowledged(1, 1, null));
        using var client = CreateClient(new InMemoryPlexItemStore(), movies);

        var response = await client.PutAsJsonAsync($"/movies/{MovieId}", new
        {
            id = MovieId,
            title = "Dune",
            plex = new { movieId = MovieId, plexItemId = "evil", resolution = "8k" }
        });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(saved, Is.Not.Null);
        Assert.That(saved!.Plex, Is.Null);
    }
}
