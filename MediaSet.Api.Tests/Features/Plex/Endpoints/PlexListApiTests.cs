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
/// The Plex summary and the <c>plex</c> filter on the paged movie search over HTTP, with in-memory Plex stores and a
/// movie service that applies the id filter the endpoint passes it.
/// </summary>
[TestFixture]
public class PlexListApiTests
{
    private const string OnPlexId = "65a000000000000000000001";
    private const string WatchedId = "65a000000000000000000002";
    private const string BrokenId = "65a000000000000000000003";
    private const string GoneId = "65a000000000000000000004";
    private const string UnlinkedId = "65a000000000000000000005";
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

    private HttpClient CreateClient(InMemoryPlexItemStore items, Mock<IEntityService<Movie>> movies, bool plexEnabled = true, bool moviesEnabled = true)
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

    private static readonly string[] AllIds = [OnPlexId, WatchedId, BrokenId, GoneId, UnlinkedId];

    /// <summary>A movie service whose paged search applies the id filter like the real one does.</summary>
    private static Mock<IEntityService<Movie>> MovieService()
    {
        var mock = new Mock<IEntityService<Movie>>();
        mock.Setup(s => s.PagedSearchAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<IdFilter?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string _, string _, int page, int pageSize, IdFilter? filter, CancellationToken _) =>
            {
                var movies = AllIds
                    .Where(id => filter == null || filter.Ids.Contains(id) != filter.Exclude)
                    .Select(id => new Movie { Id = id, Title = "Title " + id })
                    .ToList();
                return new PagedResult<Movie>(movies, movies.Count, page, pageSize);
            });
        return mock;
    }

    private static InMemoryPlexItemStore Store()
    {
        var items = new InMemoryPlexItemStore();
        var synced = new DateTime(2024, 4, 1, 0, 0, 0, DateTimeKind.Utc);
        items.UpsertAsync(new PlexItemData { Id = OnPlexId, PlexItemId = "1", OnPlex = true, Resolution = "1080", FilePath = SecretPath, LastSyncedAt = synced }, CancellationToken.None).Wait();
        items.UpsertAsync(new PlexItemData { Id = WatchedId, PlexItemId = "2", OnPlex = true, Watched = true, Resolution = "4k", FilePath = SecretPath, LastSyncedAt = synced }, CancellationToken.None).Wait();
        items.UpsertAsync(new PlexItemData { Id = BrokenId, PlexItemId = "3", OnPlex = false, Broken = true, LinkSource = PlexLinkSources.Manual, LastSyncedAt = synced }, CancellationToken.None).Wait();
        items.UpsertAsync(new PlexItemData { Id = GoneId, PlexItemId = "4", OnPlex = false, MissCount = 1, LastSyncedAt = synced }, CancellationToken.None).Wait();
        return items;
    }

    private static async Task<(HttpStatusCode Status, string Body, JsonElement Json)> GetAsync(HttpClient client, string query)
    {
        var response = await client.GetAsync("/movies/pagedsearch?searchText=&orderBy=title:asc" + query);
        var body = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        return (response.StatusCode, body, doc.RootElement.Clone());
    }

    private static string[] Ids(JsonElement json) =>
        [.. json.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("id").GetString()!)];

    private static JsonElement Item(JsonElement json, string id) =>
        json.GetProperty("items").EnumerateArray().Single(i => i.GetProperty("id").GetString() == id);

    [Test]
    public async Task PagedSearch_PlexEnabled_AttachesSummaryToLinkedMoviesOnly()
    {
        using var client = CreateClient(Store(), MovieService());

        var (status, _, json) = await GetAsync(client, "");

        Assert.That(status, Is.EqualTo(HttpStatusCode.OK));
        var onPlex = Item(json, OnPlexId).GetProperty("plex");
        Assert.That(onPlex.GetProperty("onPlex").GetBoolean(), Is.True);
        Assert.That(onPlex.GetProperty("resolution").GetString(), Is.EqualTo("1080"));
        Assert.That(onPlex.GetProperty("watched").GetBoolean(), Is.False);
        Assert.That(onPlex.GetProperty("broken").GetBoolean(), Is.False);
        Assert.That(Item(json, WatchedId).GetProperty("plex").GetProperty("watched").GetBoolean(), Is.True);
        Assert.That(Item(json, BrokenId).GetProperty("plex").GetProperty("broken").GetBoolean(), Is.True);
        Assert.That(Item(json, GoneId).GetProperty("plex").GetProperty("onPlex").GetBoolean(), Is.False);
        Assert.That(Item(json, UnlinkedId).TryGetProperty("plex", out _), Is.False);
    }

    [Test]
    public async Task PagedSearch_PlexEnabled_NeverExposesFilePaths()
    {
        using var client = CreateClient(Store(), MovieService());

        var (_, body, json) = await GetAsync(client, "");

        Assert.That(body, Does.Not.Contain(SecretPath));
        Assert.That(Item(json, OnPlexId).GetProperty("plex").TryGetProperty("filePath", out _), Is.False);
    }

    [Test]
    public async Task PagedSearch_PlexEnabled_JoinsTheWholePageInOneQuery()
    {
        var items = Store();
        using var client = CreateClient(items, MovieService());

        await GetAsync(client, "");

        Assert.That(items.SummaryReads, Is.EqualTo(1));
    }

    [Test]
    public async Task PagedSearch_PlexNotConfigured_HasNoPlexProperty()
    {
        using var client = CreateClient(Store(), MovieService(), plexEnabled: false);

        var (status, _, json) = await GetAsync(client, "");

        Assert.That(status, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(json.GetProperty("items").EnumerateArray().Any(i => i.TryGetProperty("plex", out _)), Is.False);
    }

    [Test]
    public async Task PagedSearch_MoviesNotEnabledForPlex_HasNoPlexProperty()
    {
        using var client = CreateClient(Store(), MovieService(), moviesEnabled: false);

        var (_, _, json) = await GetAsync(client, "");

        Assert.That(json.GetProperty("items").EnumerateArray().Any(i => i.TryGetProperty("plex", out _)), Is.False);
    }

    [Test]
    public async Task PagedSearch_SummaryReadFails_ReturnsListWithoutSummaries()
    {
        var items = Store();
        items.SummariesFailure = new InvalidOperationException("mongo down");
        using var client = CreateClient(items, MovieService());

        var (status, _, json) = await GetAsync(client, "");

        Assert.That(status, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(Ids(json), Has.Length.EqualTo(AllIds.Length));
        Assert.That(json.GetProperty("items").EnumerateArray().Any(i => i.TryGetProperty("plex", out _)), Is.False);
    }

    [TestCase("onPlex", new[] { OnPlexId, WatchedId })]
    [TestCase("notOnPlex", new[] { BrokenId, GoneId, UnlinkedId })]
    [TestCase("unwatched", new[] { OnPlexId })]
    [TestCase("broken", new[] { BrokenId })]
    [TestCase("ONPLEX", new[] { OnPlexId, WatchedId })]
    public async Task PagedSearch_PlexFilter_ReturnsOnlyMatchingMovies(string filter, string[] expected)
    {
        using var client = CreateClient(Store(), MovieService());

        var (status, _, json) = await GetAsync(client, $"&plex={filter}");

        Assert.That(status, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(Ids(json), Is.EquivalentTo(expected));
    }

    [Test]
    public async Task PagedSearch_PlexFilterWithSearchTextAndPaging_PassesThemThrough()
    {
        var movies = MovieService();
        using var client = CreateClient(Store(), movies);

        var response = await client.GetAsync("/movies/pagedsearch?searchText=Title&orderBy=title:desc&plex=onPlex&page=2&pageSize=10");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        movies.Verify(s => s.PagedSearchAsync("Title", "title:desc", 2, 10,
            It.Is<IdFilter?>(f => f != null && !f.Exclude && f.Ids.Count == 2), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task PagedSearch_UnknownPlexFilter_ReturnsBadRequest()
    {
        using var client = CreateClient(Store(), MovieService());

        var response = await client.GetAsync("/movies/pagedsearch?searchText=&orderBy=title:asc&plex=bogus");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task PagedSearch_PlexFilterButPlexNotConfigured_IsIgnored()
    {
        using var client = CreateClient(Store(), MovieService(), plexEnabled: false);

        var (status, _, json) = await GetAsync(client, "&plex=onPlex");

        Assert.That(status, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(Ids(json), Has.Length.EqualTo(AllIds.Length));
    }
}
