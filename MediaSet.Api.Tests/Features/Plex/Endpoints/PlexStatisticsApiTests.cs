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

    private HttpClient CreateClient(InMemoryPlexItemStore items, bool plexEnabled = true, bool moviesEnabled = true)
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
                builder.UseSetting("PlexConfiguration:Entities:Music:Enabled", "true");
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
                services.AddScoped(_ => new Mock<IEntityService<Music>>().Object);
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
    public async Task GetStatistics_MoviesNotEnabled_Returns400()
    {
        using var client = CreateClient(new InMemoryPlexItemStore(), moviesEnabled: false);

        var response = await client.GetAsync("/plex/statistics");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }
}
