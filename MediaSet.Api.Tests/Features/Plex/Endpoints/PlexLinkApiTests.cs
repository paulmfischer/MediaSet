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
using MediaSet.Api.Infrastructure.Plex;
using MediaSet.Api.Shared.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration.EnvironmentVariables;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using NUnit.Framework;

namespace MediaSet.Api.Tests.Features.Plex.Endpoints;

/// <summary>Manual link, unlink and single-item refresh over HTTP, with Plex served from fixtures.</summary>
[TestFixture]
public class PlexLinkApiTests
{
    private const string Token = "super-secret-token-value";
    private const string MovieId = "65a000000000000000000001";
    private const string TvId = "65a000000000000000000002";
    private const string TvNoScopeId = "65a000000000000000000003";
    private const string MissingMovieId = "65a0000000000000000000ff";
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

    private HttpClient CreateClient(FakePlexHandler handler, InMemoryPlexItemStore items, bool moviesEnabled = true)
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
            builder.UseSetting("PlexConfiguration:BaseUrl", "http://plex.local:32400");
            builder.UseSetting("PlexConfiguration:Token", Token);
            builder.UseSetting("PlexConfiguration:Entities:Movies:Enabled", moviesEnabled.ToString());
            builder.ConfigureServices(services =>
            {
                foreach (var d in services.Where(d => d.ServiceType == typeof(IDatabaseService) || d.ServiceType == typeof(IEntityService<Movie>)).ToList())
                {
                    services.Remove(d);
                }
                services.AddSingleton(new Mock<IDatabaseService>().Object);
                var movie = new Movie { Id = MovieId, Title = "Dune", ExternalId = "tmdbid:1" };
                var tv = new Movie { Id = TvId, Title = "The Matrix Show: Season 2", IsTvSeries = true };
                var tvNoScope = new Movie { Id = TvNoScopeId, Title = "The Matrix Show", IsTvSeries = true };
                var movies = InMemoryPlexItemStore.MovieService(movie, tv, tvNoScope);
                movies.Setup(s => s.GetAsync(MovieId, It.IsAny<CancellationToken>())).ReturnsAsync(movie);
                movies.Setup(s => s.GetAsync(TvId, It.IsAny<CancellationToken>())).ReturnsAsync(tv);
                movies.Setup(s => s.GetAsync(TvNoScopeId, It.IsAny<CancellationToken>())).ReturnsAsync(tvNoScope);
                services.AddScoped(_ => movies.Object);
                InMemoryPlexSyncRunStore.Replace(services);
                InMemoryPlexItemStore.Replace(services, items);
                services.AddHttpClient(nameof(IPlexClient)).ConfigurePrimaryHttpMessageHandler(() => handler);
            });
        });
        _factories.Add(factory);
        return factory.CreateClient();
    }

    private static FakePlexHandler Server() => FakePlexHandler.WithServer()
        .Route("/library/metadata/1001", "metadata_1001.json")
        .Route("/library/sections/1/all", "movie.json")
        .Route("/library/sections/3/all", "subtitles_audio.json");

    private static FakePlexHandler ServerWithShow() => Server()
        .Route("/library/metadata/2001", "show.json")
        .Route("/library/metadata/2001/children", "seasons.json");

    private static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response)
    {
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return doc.RootElement.Clone();
    }

    private static Task<HttpResponseMessage> LinkAsync(HttpClient client, string id, object body, string type = "movies") =>
        client.PutAsJsonAsync($"/plex/{type}/items/{id}/link", body);

    private static PlexItemData Linked(string source = PlexLinkSources.Manual) => new()
    {
        Id = MovieId,
        PlexItemId = "1001",
        OnPlex = true,
        LinkSource = source,
        Resolution = "old",
        LastSyncedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)
    };

    [Test]
    public async Task Link_ExistingPlexItem_StoresManualLinkAndReturnsItsData()
    {
        var items = new InMemoryPlexItemStore();
        using var client = CreateClient(Server(), items);

        var response = await LinkAsync(client, MovieId, new { plexItemId = "1001" });
        var json = await ReadJsonAsync(response);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(json.GetProperty("plexItemId").GetString(), Is.EqualTo("1001"));
        Assert.That(json.GetProperty("linkSource").GetString(), Is.EqualTo("manual"));
        Assert.That(json.GetProperty("onPlex").GetBoolean(), Is.True);
        Assert.That(json.GetProperty("resolution").GetString(), Is.EqualTo("1080"));
        Assert.That(items.Items[MovieId].LinkSource, Is.EqualTo(PlexLinkSources.Manual));
        Assert.That(items.Items[MovieId].Resolution, Is.EqualTo("1080"));
    }

    [Test]
    public async Task Link_MovieAlreadyLinked_ReplacesTheLink()
    {
        var items = new InMemoryPlexItemStore();
        await items.UpsertAsync(new PlexItemData { Id = MovieId, PlexItemId = "999", OnPlex = true }, CancellationToken.None);
        using var client = CreateClient(Server(), items);

        var response = await LinkAsync(client, MovieId, new { plexItemId = "1001" });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(items.Items[MovieId].PlexItemId, Is.EqualTo("1001"));
        Assert.That(items.Items[MovieId].LinkSource, Is.EqualTo(PlexLinkSources.Manual));
    }

    [Test]
    public async Task Link_PlexItemDoesNotExist_ReturnsNotFoundAndStoresNothing()
    {
        var items = new InMemoryPlexItemStore();
        using var client = CreateClient(Server(), items);

        var response = await LinkAsync(client, MovieId, new { plexItemId = "424242" });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        Assert.That(response.Content.Headers.ContentType?.MediaType, Is.EqualTo("application/problem+json"));
        Assert.That(items.Items, Is.Empty);
    }

    [Test]
    public async Task Link_PlexItemIsNotAMovie_ReturnsNotFound()
    {
        var handler = Server().Route("/library/metadata/2001", "show.json");
        using var client = CreateClient(handler, new InMemoryPlexItemStore());

        var response = await LinkAsync(client, MovieId, new { plexItemId = "2001" });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task Link_MovieDoesNotExist_ReturnsNotFoundWithoutCallingPlex()
    {
        var handler = Server();
        using var client = CreateClient(handler, new InMemoryPlexItemStore());

        var response = await LinkAsync(client, MissingMovieId, new { plexItemId = "1001" });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        Assert.That(handler.Requests, Is.Empty);
    }

    [Test]
    public async Task Link_InvalidMovieId_ReturnsNotFound()
    {
        using var client = CreateClient(Server(), new InMemoryPlexItemStore());

        var response = await LinkAsync(client, "not-an-object-id", new { plexItemId = "1001" });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [TestCase("")]
    [TestCase("  ")]
    public async Task Link_BlankPlexItemId_ReturnsBadRequest(string plexItemId)
    {
        using var client = CreateClient(Server(), new InMemoryPlexItemStore());

        var response = await LinkAsync(client, MovieId, new { plexItemId });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task Link_EntityTypeDisabled_ReturnsBadRequestWithoutCallingPlex()
    {
        var handler = Server();
        using var client = CreateClient(handler, new InMemoryPlexItemStore(), moviesEnabled: false);

        var response = await LinkAsync(client, MovieId, new { plexItemId = "1001" });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That(handler.Requests, Is.Empty);
    }

    [Test]
    public async Task Link_PlexUnreachable_ReturnsBadGatewayWithoutTokenOrUrl()
    {
        var handler = new FakePlexHandler().Route("/identity",
            _ => throw new HttpRequestException($"refused http://plex.local:32400/?X-Plex-Token={Token}"));
        using var client = CreateClient(handler, new InMemoryPlexItemStore());

        var response = await LinkAsync(client, MovieId, new { plexItemId = "1001" });
        var body = await response.Content.ReadAsStringAsync();

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadGateway));
        Assert.That(body, Does.Not.Contain(Token).And.Not.Contain("plex.local"));
    }

    [Test]
    public async Task Unlink_LinkedMovie_DeletesStoredDataAndReturnsNoContent()
    {
        var items = new InMemoryPlexItemStore();
        await items.UpsertAsync(Linked(), CancellationToken.None);
        using var client = CreateClient(Server(), items);

        var response = await client.DeleteAsync($"/plex/movies/items/{MovieId}/link");
        var after = await client.GetAsync($"/plex/movies/items/{MovieId}");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
        Assert.That(items.Items, Is.Empty);
        Assert.That(after.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task Unlink_UnlinkedMovie_ReturnsNotFound()
    {
        using var client = CreateClient(Server(), new InMemoryPlexItemStore());

        var response = await client.DeleteAsync($"/plex/movies/items/{MovieId}/link");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task Unlink_MovieDoesNotExist_ReturnsNotFound()
    {
        var items = new InMemoryPlexItemStore();
        await items.UpsertAsync(new PlexItemData { Id = MissingMovieId, PlexItemId = "1" }, CancellationToken.None);
        using var client = CreateClient(Server(), items);

        var response = await client.DeleteAsync($"/plex/movies/items/{MissingMovieId}/link");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        Assert.That(items.Items, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task Unlink_EntityTypeDisabled_ReturnsBadRequestAndKeepsData()
    {
        var items = new InMemoryPlexItemStore();
        await items.UpsertAsync(Linked(), CancellationToken.None);
        using var client = CreateClient(Server(), items, moviesEnabled: false);

        var response = await client.DeleteAsync($"/plex/movies/items/{MovieId}/link");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That(items.Items, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task Refresh_LinkedMovie_ReloadsDataFromItsPlexItemAndKeepsManualSource()
    {
        var items = new InMemoryPlexItemStore();
        await items.UpsertAsync(Linked(), CancellationToken.None);
        using var client = CreateClient(Server(), items);

        var response = await client.PostAsync($"/plex/movies/items/{MovieId}/refresh", null);
        var json = await ReadJsonAsync(response);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(json.GetProperty("resolution").GetString(), Is.EqualTo("1080"));
        Assert.That(json.GetProperty("linkSource").GetString(), Is.EqualTo("manual"));
        Assert.That(items.Items[MovieId].Resolution, Is.EqualTo("1080"));
        Assert.That(items.Items[MovieId].LastSyncedAt, Is.GreaterThan(new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)));
    }

    [Test]
    public async Task Refresh_UnlinkedMovie_ReturnsBadRequest()
    {
        var handler = Server();
        using var client = CreateClient(handler, new InMemoryPlexItemStore());

        var response = await client.PostAsync($"/plex/movies/items/{MovieId}/refresh", null);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That(response.Content.Headers.ContentType?.MediaType, Is.EqualTo("application/problem+json"));
        Assert.That(handler.Requests, Is.Empty);
    }

    [Test]
    public async Task Refresh_MovieDoesNotExist_ReturnsNotFound()
    {
        using var client = CreateClient(Server(), new InMemoryPlexItemStore());

        var response = await client.PostAsync($"/plex/movies/items/{MissingMovieId}/refresh", null);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task Refresh_EntityTypeDisabled_ReturnsBadRequest()
    {
        using var client = CreateClient(Server(), new InMemoryPlexItemStore(), moviesEnabled: false);

        var response = await client.PostAsync($"/plex/movies/items/{MovieId}/refresh", null);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task Refresh_PlexItemGone_ReturnsNotFoundAndFlagsDataNotOnPlex()
    {
        var items = new InMemoryPlexItemStore();
        var data = Linked();
        data.PlexItemId = "424242";
        await items.UpsertAsync(data, CancellationToken.None);
        using var client = CreateClient(Server(), items);

        var response = await client.PostAsync($"/plex/movies/items/{MovieId}/refresh", null);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        Assert.That(items.Items[MovieId].OnPlex, Is.False);
        Assert.That(items.Items[MovieId].LinkSource, Is.EqualTo(PlexLinkSources.Manual));
    }

    [Test]
    public async Task Refresh_FullSyncRunning_ReturnsConflictWithCurrentRunAndDoesNotQueue()
    {
        var gate = new TaskCompletionSource();
        var handler = Server().Route("/library/sections/1/all", async (_, ct) =>
        {
            await gate.Task.WaitAsync(ct);
            return FakePlexHandler.Json(FakePlexHandler.Fixture("movie.json"));
        });
        var items = new InMemoryPlexItemStore();
        await items.UpsertAsync(Linked(), CancellationToken.None);
        using var client = CreateClient(handler, items);
        var run = await ReadJsonAsync(await client.PostAsync("/plex/movies/sync", null));

        var response = await client.PostAsync($"/plex/movies/items/{MovieId}/refresh", null);
        var body = await ReadJsonAsync(response);
        gate.SetResult();

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
        Assert.That(body.GetProperty("run").GetProperty("id").GetString(), Is.EqualTo(run.GetProperty("id").GetString()));
        Assert.That(items.Items[MovieId].Resolution, Is.EqualTo("old"));
    }

    [Test]
    public async Task Link_TvEntrySingleSeasonScope_StoresShowScopeAndSelectedSeasonOnly()
    {
        var items = new InMemoryPlexItemStore();
        using var client = CreateClient(ServerWithShow(), items);

        var response = await LinkAsync(client, TvId, new { plexItemId = "2001", scope = new { kind = "season", season = 2 } });
        var json = await ReadJsonAsync(response);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(json.GetProperty("linkSource").GetString(), Is.EqualTo("manual"));
        Assert.That(json.GetProperty("plexItemId").GetString(), Is.EqualTo("2001"));
        Assert.That(json.GetProperty("scope").GetProperty("kind").GetString(), Is.EqualTo("season"));
        Assert.That(json.GetProperty("scope").GetProperty("from").GetInt32(), Is.EqualTo(2));
        var seasons = json.GetProperty("seasons").EnumerateArray().ToList();
        Assert.That(seasons, Has.Count.EqualTo(1));
        Assert.That(seasons[0].GetProperty("index").GetInt32(), Is.EqualTo(2));
        Assert.That(seasons[0].GetProperty("episodeCount").GetInt32(), Is.EqualTo(13));
        Assert.That(items.Items[TvId].Scope, Is.EqualTo(PlexScope.Single(2)));
    }

    [Test]
    public async Task Link_TvEntryRangeScope_StoresTheSeasonsInTheRange()
    {
        var items = new InMemoryPlexItemStore();
        using var client = CreateClient(ServerWithShow(), items);

        var response = await LinkAsync(client, TvId, new { plexItemId = "2001", scope = new { kind = "range", from = 1, to = 2 } });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(items.Items[TvId].Scope, Is.EqualTo(new PlexScope(PlexScopeKinds.Range, 1, 2)));
        Assert.That(items.Items[TvId].Seasons.Select(s => s.Index), Is.EqualTo([1, 2]));
    }

    [Test]
    public async Task Link_TvEntryAllScope_StoresEverySeasonIncludingSpecials()
    {
        var items = new InMemoryPlexItemStore();
        using var client = CreateClient(ServerWithShow(), items);

        var response = await LinkAsync(client, TvNoScopeId, new { plexItemId = "2001", scope = new { kind = "all" } });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(items.Items[TvNoScopeId].Scope, Is.EqualTo(PlexScope.AllSeasons));
        Assert.That(items.Items[TvNoScopeId].Seasons.Select(s => s.Index), Is.EqualTo([0, 1, 2, 3]));
    }

    [Test]
    public async Task Link_TvEntryWithoutScopeAndReadableTitle_UsesTheTitleScope()
    {
        var items = new InMemoryPlexItemStore();
        using var client = CreateClient(ServerWithShow(), items);

        var response = await LinkAsync(client, TvId, new { plexItemId = "2001" });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(items.Items[TvId].Scope, Is.EqualTo(PlexScope.Single(2)));
    }

    [Test]
    public async Task Link_TvEntryWithoutScopeAndUnreadableTitle_ReturnsBadRequestAndStoresNothing()
    {
        var items = new InMemoryPlexItemStore();
        var handler = ServerWithShow();
        using var client = CreateClient(handler, items);

        var response = await LinkAsync(client, TvNoScopeId, new { plexItemId = "2001" });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That(items.Items, Is.Empty);
        Assert.That(handler.Requests, Is.Empty);
    }

    [Test]
    public async Task Link_TvEntryScopeSeasonNotOnPlex_StillLinksWithNoSeasons()
    {
        var items = new InMemoryPlexItemStore();
        using var client = CreateClient(ServerWithShow(), items);

        var response = await LinkAsync(client, TvId, new { plexItemId = "2001", scope = new { kind = "season", season = 9 } });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(items.Items[TvId].Seasons, Is.Empty);
        Assert.That(items.Items[TvId].Scope, Is.EqualTo(PlexScope.Single(9)));
    }

    [Test]
    public async Task Link_TwoTvEntriesToOneShow_EachKeepsItsOwnScope()
    {
        var items = new InMemoryPlexItemStore();
        using var client = CreateClient(ServerWithShow(), items);

        await LinkAsync(client, TvId, new { plexItemId = "2001", scope = new { kind = "season", season = 2 } });
        await LinkAsync(client, TvNoScopeId, new { plexItemId = "2001", scope = new { kind = "range", from = 1, to = 3 } });

        Assert.That(items.Items[TvId].PlexItemId, Is.EqualTo(items.Items[TvNoScopeId].PlexItemId));
        Assert.That(items.Items[TvId].Seasons.Select(s => s.Index), Is.EqualTo([2]));
        Assert.That(items.Items[TvNoScopeId].Seasons.Select(s => s.Index), Is.EqualTo([1, 2, 3]));
    }

    [TestCase("{\"plexItemId\":\"2001\",\"scope\":{\"kind\":\"season\"}}")]
    [TestCase("{\"plexItemId\":\"2001\",\"scope\":{\"kind\":\"season\",\"season\":-1}}")]
    [TestCase("{\"plexItemId\":\"2001\",\"scope\":{\"kind\":\"season\",\"season\":100}}")]
    [TestCase("{\"plexItemId\":\"2001\",\"scope\":{\"kind\":\"range\",\"from\":3,\"to\":1}}")]
    [TestCase("{\"plexItemId\":\"2001\",\"scope\":{\"kind\":\"range\",\"from\":2,\"to\":2}}")]
    [TestCase("{\"plexItemId\":\"2001\",\"scope\":{\"kind\":\"range\",\"from\":1}}")]
    [TestCase("{\"plexItemId\":\"2001\",\"scope\":{\"kind\":\"everything\"}}")]
    [TestCase("{\"plexItemId\":\"2001\",\"scope\":{}}")]
    public async Task Link_TvEntryInvalidScope_ReturnsBadRequest(string body)
    {
        var items = new InMemoryPlexItemStore();
        using var client = CreateClient(ServerWithShow(), items);

        var response = await client.PutAsync($"/plex/movies/items/{TvId}/link", new StringContent(body, System.Text.Encoding.UTF8, "application/json"));

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That(items.Items, Is.Empty);
    }

    [Test]
    public async Task Link_MovieWithScope_ReturnsBadRequest()
    {
        var items = new InMemoryPlexItemStore();
        using var client = CreateClient(ServerWithShow(), items);

        var response = await LinkAsync(client, MovieId, new { plexItemId = "1001", scope = new { kind = "all" } });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That(items.Items, Is.Empty);
    }

    [Test]
    public async Task Link_TvEntryToAMovieItem_ReturnsNotFound()
    {
        var items = new InMemoryPlexItemStore();
        using var client = CreateClient(ServerWithShow(), items);

        var response = await LinkAsync(client, TvId, new { plexItemId = "1001", scope = new { kind = "all" } });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        Assert.That(items.Items, Is.Empty);
    }

    [Test]
    public async Task Refresh_LinkedTvEntry_RereadsSeasonsKeepingScopeAndLink()
    {
        var items = new InMemoryPlexItemStore();
        await items.UpsertAsync(new PlexItemData
        {
            Id = TvId,
            PlexItemId = "2001",
            OnPlex = true,
            LinkSource = PlexLinkSources.Manual,
            Scope = PlexScope.Single(2),
            Seasons = [new PlexSeasonData { Index = 2, PlexSeasonId = "2102", EpisodeCount = 1 }]
        }, CancellationToken.None);
        using var client = CreateClient(ServerWithShow(), items);

        var response = await client.PostAsync($"/plex/movies/items/{TvId}/refresh", null);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(items.Items[TvId].LinkSource, Is.EqualTo(PlexLinkSources.Manual));
        Assert.That(items.Items[TvId].Scope, Is.EqualTo(PlexScope.Single(2)));
        Assert.That(items.Items[TvId].Seasons.Single().EpisodeCount, Is.EqualTo(13));
    }
}
