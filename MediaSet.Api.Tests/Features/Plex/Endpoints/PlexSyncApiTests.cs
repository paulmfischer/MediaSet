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
using MediaSet.Api.Shared.Models;
using MediaSet.Api.Infrastructure.Plex;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration.EnvironmentVariables;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;

namespace MediaSet.Api.Tests.Features.Plex.Endpoints;

/// <summary>
/// Sync start, conflict, status and failure over HTTP. Plex is served from fixtures by a fake handler and the
/// PlexSyncRuns collection is replaced by an in-memory store.
/// </summary>
[TestFixture]
public class PlexSyncApiTests
{
    private const string Token = "super-secret-token-value";
    private readonly List<WebApplicationFactory<Program>> _factories = [];
    private CapturingLoggerProvider _logs = new();

    [SetUp]
    public void SetUp() => _logs = new CapturingLoggerProvider();

    [TearDown]
    public void TearDown()
    {
        _logs.Dispose();
        foreach (var factory in _factories)
        {
            factory.Dispose();
        }
        _factories.Clear();
    }

    private HttpClient CreateClient(FakePlexHandler handler, InMemoryPlexSyncRunStore store, InMemoryPlexItemStore? items = null, params Movie[] movies)
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
            builder.UseSetting("PlexConfiguration:Entities:Movies:Enabled", "true");
            builder.ConfigureServices(services =>
            {
                foreach (var d in services.Where(d => d.ServiceType == typeof(IDatabaseService)).ToList())
                {
                    services.Remove(d);
                }
                services.AddSingleton(new Mock<IDatabaseService>().Object);
                InMemoryPlexSyncRunStore.Replace(services, store);
                InMemoryPlexItemStore.Replace(services, items ?? new InMemoryPlexItemStore());
                foreach (var d in services.Where(d => d.ServiceType == typeof(IEntityService<Movie>)).ToList())
                {
                    services.Remove(d);
                }
                services.AddScoped(_ => InMemoryPlexItemStore.MovieService(movies).Object);
                // The app logs through Serilog, which ignores extra providers: give the sync service a capturing logger
                services.AddSingleton<ILogger<PlexSyncService>>(new Logger<PlexSyncService>(LoggerFactory.Create(b => b.SetMinimumLevel(LogLevel.Trace).AddProvider(_logs))));
                services.AddHttpClient(nameof(IPlexClient)).ConfigurePrimaryHttpMessageHandler(() => handler);
            });
        });
        _factories.Add(factory);
        return factory.CreateClient();
    }

    private static FakePlexHandler ServerWithMovies() => FakePlexHandler.WithServer()
        .Route("/library/sections/1/all", "movie.json")
        .Route("/library/sections/3/all", "subtitles_audio.json")
        .Route("/library/sections/2/all", "show.json");

    private static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response)
    {
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return doc.RootElement.Clone();
    }

    /// <summary>Polls the entity status until the last run has left the in-progress states.</summary>
    private static async Task<JsonElement> WaitForFinishedRunAsync(HttpClient client)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (DateTime.UtcNow < deadline)
        {
            var status = await ReadJsonAsync(await client.GetAsync("/plex/movies/status"));
            var last = status.GetProperty("lastRun");
            if (last.ValueKind == JsonValueKind.Object && last.GetProperty("state").GetString() is not ("running" or "queued"))
            {
                return last;
            }
            await Task.Delay(20);
        }

        throw new TimeoutException("The Plex sync run did not finish in time.");
    }

    [Test]
    public async Task StartSync_EnabledEntityType_ReturnsAcceptedWithRunAndStoresIt()
    {
        var store = new InMemoryPlexSyncRunStore();
        using var client = CreateClient(ServerWithMovies(), store);

        var response = await client.PostAsync("/plex/movies/sync", null);
        var run = await ReadJsonAsync(response);
        await WaitForFinishedRunAsync(client);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Accepted));
        Assert.That(run.GetProperty("entityType").GetString(), Is.EqualTo("movies"));
        Assert.That(run.GetProperty("trigger").GetString(), Is.EqualTo("manual"));
        Assert.That(run.GetProperty("state").GetString(), Is.EqualTo("running"));
        Assert.That(run.GetProperty("startedAt").ValueKind, Is.EqualTo(JsonValueKind.String));
        Assert.That(run.GetProperty("endedAt").ValueKind, Is.EqualTo(JsonValueKind.Null));
        var stored = store.Runs.Single();
        Assert.That(stored.Id, Is.EqualTo(run.GetProperty("id").GetString()));
        Assert.That(stored.EntityType, Is.EqualTo("movies"));
    }

    [Test]
    public async Task StartSync_RunSucceeds_StatusShowsLastRunWithOutcomeAndItemsScanned()
    {
        var store = new InMemoryPlexSyncRunStore();
        // Three entries against the fixture library's items: the count follows the entries, not the Plex items
        using var client = CreateClient(ServerWithMovies(), store, null,
            new Movie { Id = "a", Title = "A" }, new Movie { Id = "b", Title = "B" }, new Movie { Id = "c", Title = "C" });

        await client.PostAsync("/plex/movies/sync", null);
        var last = await WaitForFinishedRunAsync(client);
        var all = await ReadJsonAsync(await client.GetAsync("/plex/status"));

        Assert.That(last.GetProperty("state").GetString(), Is.EqualTo("succeeded"));
        Assert.That(last.GetProperty("itemsScanned").GetInt32(), Is.EqualTo(3));
        Assert.That(last.GetProperty("endedAt").ValueKind, Is.EqualTo(JsonValueKind.String));
        Assert.That(all.EnumerateArray().Single().GetProperty("lastRun").GetProperty("state").GetString(), Is.EqualTo("succeeded"));
        Assert.That(store.Runs.Single().ItemsScanned, Is.EqualTo(3));
    }

    [Test]
    public async Task StartSync_RunInProgress_ReturnsConflictWithCurrentRun()
    {
        var gate = new TaskCompletionSource();
        var handler = ServerWithMovies().Route("/library/sections/1/all", async (_, ct) =>
        {
            await gate.Task.WaitAsync(ct);
            return FakePlexHandler.Json(FakePlexHandler.Fixture("movie.json"));
        });
        var store = new InMemoryPlexSyncRunStore();
        using var client = CreateClient(handler, store);
        var first = await ReadJsonAsync(await client.PostAsync("/plex/movies/sync", null));

        var second = await client.PostAsync("/plex/movies/sync", null);
        var body = await ReadJsonAsync(second);
        gate.SetResult();
        await WaitForFinishedRunAsync(client);

        Assert.That(second.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
        Assert.That(body.GetProperty("run").GetProperty("id").GetString(), Is.EqualTo(first.GetProperty("id").GetString()));
        Assert.That(body.GetProperty("run").GetProperty("state").GetString(), Is.EqualTo("running"));
        Assert.That(store.Runs, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task StartSync_AfterRunFinished_StartsNewRun()
    {
        var store = new InMemoryPlexSyncRunStore();
        using var client = CreateClient(ServerWithMovies(), store);
        await client.PostAsync("/plex/movies/sync", null);
        await WaitForFinishedRunAsync(client);

        var response = await client.PostAsync("/plex/movies/sync", null);
        await WaitForFinishedRunAsync(client);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Accepted));
        Assert.That(store.Runs, Has.Count.EqualTo(2));
    }

    [Test]
    public async Task StartSync_PlexUnreachable_RecordsFailedRunWithoutToken()
    {
        var handler = FakePlexHandler.WithServer()
            .Route("/library/sections", _ => throw new HttpRequestException($"connection refused for token {Token}"));
        var store = new InMemoryPlexSyncRunStore();
        using var client = CreateClient(handler, store);

        await client.PostAsync("/plex/movies/sync", null);
        var last = await WaitForFinishedRunAsync(client);
        var body = await (await client.GetAsync("/plex/movies/status")).Content.ReadAsStringAsync();

        Assert.That(last.GetProperty("state").GetString(), Is.EqualTo("failed"));
        Assert.That(last.GetProperty("message").GetString(), Is.Not.Empty);
        Assert.That(body, Does.Not.Contain(Token));
        Assert.That(store.Runs.Single().Message, Does.Not.Contain(Token));
        Assert.That(store.Runs.Single().EndedAt, Is.Not.Null);
    }

    [Test]
    public async Task StartSync_UnexpectedFailure_RecordsFailedRunWithGenericMessage()
    {
        var handler = FakePlexHandler.WithServer()
            .Route("/library/sections", _ => throw new InvalidOperationException($"boom {Token}"));
        var store = new InMemoryPlexSyncRunStore();
        using var client = CreateClient(handler, store);

        await client.PostAsync("/plex/movies/sync", null);
        var last = await WaitForFinishedRunAsync(client);

        Assert.That(last.GetProperty("state").GetString(), Is.EqualTo("failed"));
        Assert.That(store.Runs.Single().Message, Does.Not.Contain(Token));
    }

    [Test]
    public async Task StartSync_UnknownEntityType_ReturnsNotFoundAndStartsNoRun()
    {
        var store = new InMemoryPlexSyncRunStore();
        using var client = CreateClient(ServerWithMovies(), store);

        var response = await client.PostAsync("/plex/music/sync", null);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        Assert.That(store.Runs, Is.Empty);
    }

    [Test]
    public async Task Startup_RunLeftInProgress_IsClosedAsInterruptedAndStatusShowsIt()
    {
        var store = new InMemoryPlexSyncRunStore();
        await store.InsertAsync(new PlexSyncRun
        {
            EntityType = "movies",
            State = PlexSyncRunStates.Running,
            StartedAt = DateTime.UtcNow.AddMinutes(-5)
        }, CancellationToken.None);
        using var client = CreateClient(ServerWithMovies(), store);

        var status = await ReadJsonAsync(await client.GetAsync("/plex/movies/status"));

        Assert.That(store.EnsureCreatedCalled, Is.True);
        Assert.That(status.GetProperty("lastRun").GetProperty("state").GetString(), Is.EqualTo("interrupted"));
        Assert.That(status.GetProperty("lastRun").GetProperty("endedAt").ValueKind, Is.EqualTo(JsonValueKind.String));
    }

    private static Movie MovieFor(string id, string? externalId) => new() { Id = id, Title = id, ExternalId = externalId };

    /// <summary>Starts a run and waits until the store holds the given number of runs, all finished.</summary>
    private static async Task<PlexSyncRun> RunSyncAsync(HttpClient client, InMemoryPlexSyncRunStore store, int runNumber)
    {
        await client.PostAsync("/plex/movies/sync", null);
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (DateTime.UtcNow < deadline)
        {
            if (store.Runs.Count == runNumber && !PlexSyncRunStates.IsInProgress(store.Runs[^1].State))
            {
                return store.Runs[^1];
            }
            await Task.Delay(20);
        }

        throw new TimeoutException("The Plex sync run did not finish in time.");
    }

    private static PlexItemData Linked(string id, string plexItemId, string source = PlexLinkSources.Auto) =>
        new() { Id = id, PlexItemId = plexItemId, LinkSource = source, OnPlex = true, Resolution = "1080" };

    [TestCase("500")]
    [TestCase("401")]
    [TestCase("timeout")]
    public async Task StartSync_PlexFailsDuringLinkedItemRead_FailsRunAndLeavesStoredDataUntouched(string failure)
    {
        var handler = ServerWithMovies().Route("/library/metadata/1002", (_, _) => failure == "timeout"
            ? throw new TaskCanceledException("timed out")
            : Task.FromResult(FakePlexHandler.Json("{}", failure == "401" ? HttpStatusCode.Unauthorized : HttpStatusCode.InternalServerError)));
        var items = new InMemoryPlexItemStore();
        await items.UpsertAsync(Linked("gone-auto", "7777"), CancellationToken.None);
        await items.UpsertAsync(Linked("manual", "1002", PlexLinkSources.Manual), CancellationToken.None);
        await items.UpsertAsync(Linked("deleted-movie", "1001"), CancellationToken.None);
        var store = new InMemoryPlexSyncRunStore();
        using var client = CreateClient(handler, store, items, MovieFor("gone-auto", null), MovieFor("manual", null));

        var run = await RunSyncAsync(client, store, 1);

        Assert.That(run.State, Is.EqualTo(PlexSyncRunStates.Failed));
        Assert.That(run.Message, Does.Not.Contain(Token));
        Assert.That(items.Items.Keys, Is.EquivalentTo(new[] { "gone-auto", "manual", "deleted-movie" }));
        Assert.That(items.Items.Values, Has.All.Matches<PlexItemData?>(d => d != null && d.OnPlex && d.MissCount == 0 && !d.Broken && d.Resolution == "1080"), "stored data must be untouched");
    }

    [Test]
    public async Task StartSync_AutoLinkedItemGone_FlagsOffPlexKeepsDataAndCountsMiss()
    {
        var items = new InMemoryPlexItemStore();
        await items.UpsertAsync(Linked("a", "7777"), CancellationToken.None);
        var store = new InMemoryPlexSyncRunStore();
        using var client = CreateClient(ServerWithMovies(), store, items, MovieFor("a", null));

        var run = await RunSyncAsync(client, store, 1);

        var data = items.Items["a"];
        Assert.That(run.State, Is.EqualTo(PlexSyncRunStates.Succeeded));
        Assert.That(data.OnPlex, Is.False);
        Assert.That(data.MissCount, Is.EqualTo(1));
        Assert.That(data.Broken, Is.False);
        Assert.That(data.Resolution, Is.EqualTo("1080"));
        Assert.That(data.LastSyncedAt, Is.Not.EqualTo(default(DateTime)));
    }

    [Test]
    public async Task StartSync_AutoLinkedItemGoneTwice_DropsTheLink()
    {
        var items = new InMemoryPlexItemStore();
        await items.UpsertAsync(Linked("a", "7777"), CancellationToken.None);
        var store = new InMemoryPlexSyncRunStore();
        using var client = CreateClient(ServerWithMovies(), store, items, MovieFor("a", null));

        await RunSyncAsync(client, store, 1);
        var afterFirst = items.Items.ContainsKey("a");
        var second = await RunSyncAsync(client, store, 2);

        Assert.That(afterFirst, Is.True);
        Assert.That(second.State, Is.EqualTo(PlexSyncRunStates.Succeeded));
        Assert.That(items.Items, Does.Not.ContainKey("a"));
        Assert.That(second.UnmatchedCount, Is.EqualTo(1));
    }

    [Test]
    public async Task StartSync_AutoLinkedItemMissingOnceThenBack_ResetsMissCount()
    {
        var handler = ServerWithMovies();
        var items = new InMemoryPlexItemStore();
        await items.UpsertAsync(Linked("a", "1001"), CancellationToken.None);
        var store = new InMemoryPlexSyncRunStore();
        using var client = CreateClient(handler, store, items, MovieFor("a", "tmdbid:603"));
        items.Items["a"].MissCount = 1;
        items.Items["a"].OnPlex = false;

        await RunSyncAsync(client, store, 1);

        Assert.That(items.Items["a"].OnPlex, Is.True);
        Assert.That(items.Items["a"].MissCount, Is.EqualTo(0));
    }

    [Test]
    public async Task StartSync_ManualLinkedItemGone_IsFlaggedBrokenNeverDroppedAndRecovers()
    {
        var handler = ServerWithMovies();
        var items = new InMemoryPlexItemStore();
        await items.UpsertAsync(Linked("m", "1002", PlexLinkSources.Manual), CancellationToken.None);
        var store = new InMemoryPlexSyncRunStore();
        using var client = CreateClient(handler, store, items, MovieFor("m", null));

        await RunSyncAsync(client, store, 1);
        await RunSyncAsync(client, store, 2);
        var third = await RunSyncAsync(client, store, 3);
        var whileGone = items.Items["m"];

        Assert.That(whileGone.Broken, Is.True);
        Assert.That(whileGone.OnPlex, Is.False);
        Assert.That(whileGone.MissCount, Is.EqualTo(3));
        Assert.That(whileGone.LinkSource, Is.EqualTo(PlexLinkSources.Manual));
        Assert.That(whileGone.Resolution, Is.EqualTo("1080"));
        Assert.That(third.State, Is.EqualTo(PlexSyncRunStates.Succeeded));

        handler.Route("/library/metadata/1002", "metadata_1002.json");
        await RunSyncAsync(client, store, 4);

        var recovered = items.Items["m"];
        Assert.That(recovered.Broken, Is.False);
        Assert.That(recovered.OnPlex, Is.True);
        Assert.That(recovered.MissCount, Is.EqualTo(0));
        Assert.That(recovered.LinkSource, Is.EqualTo(PlexLinkSources.Manual));
    }

    [Test]
    public async Task StartSync_OneMovieErrors_IsCountedRunContinuesAndLogHasNoToken()
    {
        var items = new InMemoryPlexItemStore { FailUpsertFor = "bad", FailUpsertMessage = $"write rejected for {Token}" };
        var store = new InMemoryPlexSyncRunStore();
        using var client = CreateClient(ServerWithMovies(), store, items, MovieFor("bad", "tmdbid:603"), MovieFor("good", "tmdbid:194"));

        var run = await RunSyncAsync(client, store, 1);
        var last = await ReadJsonAsync(await client.GetAsync("/plex/movies/status"));

        Assert.That(run.State, Is.EqualTo(PlexSyncRunStates.Succeeded));
        Assert.That(run.ErroredCount, Is.EqualTo(1));
        Assert.That(last.GetProperty("lastRun").GetProperty("erroredCount").GetInt32(), Is.EqualTo(1));
        Assert.That(items.Items.Keys, Is.EqualTo(["good"]));
        var lines = _logs.Lines.ToList();
        Assert.That(lines, Has.Some.EqualTo($"{typeof(PlexSyncService).FullName} Plex sync could not process movie bad (InvalidOperationException) "));
        Assert.That(lines, Has.None.Contains(Token));
    }

    [Test]
    public async Task StartSync_TwoMoviesLinkToSamePlexItem_AllowsBothAndLogsWarning()
    {
        var items = new InMemoryPlexItemStore();
        var store = new InMemoryPlexSyncRunStore();
        using var client = CreateClient(ServerWithMovies(), store, items, MovieFor("first", "tmdbid:603"), MovieFor("second", "tmdbid:603"));

        var run = await RunSyncAsync(client, store, 1);

        Assert.That(run.MatchedCount, Is.EqualTo(2));
        Assert.That(items.Items["first"].PlexItemId, Is.EqualTo("1001"));
        Assert.That(items.Items["second"].PlexItemId, Is.EqualTo("1001"));
        var lines = _logs.Lines.ToList();
        Assert.That(lines, Has.Some.Contains("1001").And.Contains("first").And.Contains("second"));
        Assert.That(lines, Has.None.Contains(Token));
    }
}
