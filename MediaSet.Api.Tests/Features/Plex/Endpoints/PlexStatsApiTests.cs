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

/// <summary>
/// The Plex statistics on <c>GET /stats</c> over HTTP, with the real stats service, in-memory Plex stores and mocked
/// entity services.
/// </summary>
[TestFixture]
public class PlexStatsApiTests
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

    private HttpClient CreateClient(InMemoryPlexItemStore items, int movieCount, bool plexEnabled = true, bool moviesEnabled = true)
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
            }
            builder.ConfigureServices(services =>
            {
                var replaced = new[] { typeof(IDatabaseService), typeof(IEntityService<Book>), typeof(IEntityService<Movie>), typeof(IEntityService<Game>), typeof(IEntityService<Music>) };
                foreach (var d in services.Where(d => replaced.Contains(d.ServiceType)).ToList())
                {
                    services.Remove(d);
                }
                services.AddSingleton(new Mock<IDatabaseService>().Object);
                services.AddScoped(_ => ListService<Book>([]));
                services.AddScoped(_ => ListService<Game>([]));
                services.AddScoped(_ => ListService<Music>([]));
                services.AddScoped(_ => ListService(Enumerable.Range(1, movieCount).Select(i => new Movie { Id = $"65a{i:D21}", Title = "Movie " + i }).ToList()));
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

    private static IEntityService<T> ListService<T>(List<T> list) where T : class, IEntity
    {
        var mock = new Mock<IEntityService<T>>();
        mock.Setup(s => s.GetListAsync(It.IsAny<CancellationToken>())).ReturnsAsync(list);
        return mock.Object;
    }

    private static PlexItemData Doc(string id, bool onPlex, string? resolution = null, bool watched = false, bool broken = false) => new()
    {
        Id = id,
        PlexItemId = "k" + id,
        OnPlex = onPlex,
        Resolution = resolution,
        Watched = watched,
        Broken = broken,
        FilePath = SecretPath,
        LastSyncedAt = new DateTime(2024, 4, 1, 0, 0, 0, DateTimeKind.Utc)
    };

    /// <summary>Ten movies: 4 on Plex (2 watched; 1080, 1080, 4k, no resolution), 1 gone, 1 broken link, 4 unlinked.</summary>
    private static async Task<InMemoryPlexItemStore> StoreAsync()
    {
        var items = new InMemoryPlexItemStore();
        var docs = new[]
        {
            Doc("65a000000000000000000001", true, "1080", watched: true),
            Doc("65a000000000000000000002", true, "1080"),
            Doc("65a000000000000000000003", true, "4k", watched: true),
            Doc("65a000000000000000000004", true),
            Doc("65a000000000000000000005", false, "720"),
            Doc("65a000000000000000000006", false, broken: true),
        };
        foreach (var d in docs)
        {
            await items.UpsertAsync(d, CancellationToken.None);
        }
        return items;
    }

    private static async Task<(HttpStatusCode Status, string Body, JsonElement Json)> GetStatsAsync(HttpClient client)
    {
        var response = await client.GetAsync("/stats");
        var body = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        return (response.StatusCode, body, doc.RootElement.Clone());
    }

    [Test]
    public async Task GetStats_PlexEnabled_ReturnsPlexCounts()
    {
        using var client = CreateClient(await StoreAsync(), movieCount: 10);

        var (status, _, json) = await GetStatsAsync(client);

        Assert.That(status, Is.EqualTo(HttpStatusCode.OK));
        var plex = json.GetProperty("plexStats");
        Assert.That(plex.GetProperty("onPlex").GetInt32(), Is.EqualTo(4));
        Assert.That(plex.GetProperty("notOnPlex").GetInt32(), Is.EqualTo(6));
        Assert.That(plex.GetProperty("watched").GetInt32(), Is.EqualTo(2));
        Assert.That(plex.GetProperty("unwatched").GetInt32(), Is.EqualTo(2));
        // 4 unlinked plus the broken manual link; the gone auto link is still linked
        Assert.That(plex.GetProperty("needsLinking").GetInt32(), Is.EqualTo(5));
    }

    [Test]
    public async Task GetStats_PlexEnabled_ReturnsResolutionBreakdownOfOnPlexMovies()
    {
        using var client = CreateClient(await StoreAsync(), movieCount: 10);

        var (_, _, json) = await GetStatsAsync(client);

        var breakdown = json.GetProperty("plexStats").GetProperty("resolutionBreakdown");
        Assert.That(breakdown.GetProperty("1080").GetInt32(), Is.EqualTo(2));
        Assert.That(breakdown.GetProperty("4k").GetInt32(), Is.EqualTo(1));
        Assert.That(breakdown.GetProperty("unknown").GetInt32(), Is.EqualTo(1));
        Assert.That(breakdown.TryGetProperty("720", out _), Is.False);
    }

    [Test]
    public async Task GetStats_PlexEnabledWithNoPlexData_ReturnsZeroCountsAndAllNeedLinking()
    {
        using var client = CreateClient(new InMemoryPlexItemStore(), movieCount: 3);

        var (_, _, json) = await GetStatsAsync(client);

        var plex = json.GetProperty("plexStats");
        Assert.That(plex.GetProperty("onPlex").GetInt32(), Is.EqualTo(0));
        Assert.That(plex.GetProperty("notOnPlex").GetInt32(), Is.EqualTo(3));
        Assert.That(plex.GetProperty("needsLinking").GetInt32(), Is.EqualTo(3));
        Assert.That(plex.GetProperty("resolutionBreakdown").EnumerateObject().Count(), Is.EqualTo(0));
    }

    [Test]
    public async Task GetStats_PlexEnabled_NeverReturnsFilePaths()
    {
        using var client = CreateClient(await StoreAsync(), movieCount: 10);

        var (_, body, _) = await GetStatsAsync(client);

        Assert.That(body, Does.Not.Contain("Secret Path"));
        Assert.That(body.ToLowerInvariant(), Does.Not.Contain("filepath"));
    }

    [Test]
    public async Task GetStats_PlexNotConfigured_OmitsPlexStats()
    {
        using var client = CreateClient(new InMemoryPlexItemStore(), movieCount: 3, plexEnabled: false);

        var (status, _, json) = await GetStatsAsync(client);

        Assert.That(status, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(json.TryGetProperty("movieStats", out _), Is.True);
        Assert.That(!json.TryGetProperty("plexStats", out var plex) || plex.ValueKind == JsonValueKind.Null, Is.True);
    }

    [Test]
    public async Task GetStats_PlexEnabledWithoutMovies_OmitsPlexStats()
    {
        using var client = CreateClient(await StoreAsync(), movieCount: 10, moviesEnabled: false);

        var (_, _, json) = await GetStatsAsync(client);

        Assert.That(!json.TryGetProperty("plexStats", out var plex) || plex.ValueKind == JsonValueKind.Null, Is.True);
    }

    [Test]
    public async Task GetStats_PlexStoreFails_StillReturnsStatsWithoutPlex()
    {
        var items = await StoreAsync();
        items.StatsFailure = new InvalidOperationException("mongo down");
        using var client = CreateClient(items, movieCount: 10);

        var (status, _, json) = await GetStatsAsync(client);

        Assert.That(status, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(json.GetProperty("movieStats").GetProperty("total").GetInt32(), Is.EqualTo(10));
        Assert.That(!json.TryGetProperty("plexStats", out var plex) || plex.ValueKind == JsonValueKind.Null, Is.True);
    }

    [Test]
    public async Task GetStats_PlexDataChangesBetweenRequests_ReflectsNewDataDespiteCache()
    {
        var items = await StoreAsync();
        using var client = CreateClient(items, movieCount: 10);
        await GetStatsAsync(client);

        await items.UpsertAsync(Doc("65a000000000000000000007", true, "1080"), CancellationToken.None);
        var (_, _, json) = await GetStatsAsync(client);

        Assert.That(json.GetProperty("plexStats").GetProperty("onPlex").GetInt32(), Is.EqualTo(5));
    }
}
