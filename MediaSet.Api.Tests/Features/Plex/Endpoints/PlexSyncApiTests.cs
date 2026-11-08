using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MediaSet.Api.Features.Plex.Models;
using MediaSet.Api.Infrastructure.Database;
using MediaSet.Api.Infrastructure.Plex;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration.EnvironmentVariables;
using Microsoft.Extensions.DependencyInjection;
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

    [TearDown]
    public void TearDown()
    {
        foreach (var factory in _factories)
        {
            factory.Dispose();
        }
        _factories.Clear();
    }

    private HttpClient CreateClient(FakePlexHandler handler, InMemoryPlexSyncRunStore store)
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
                services.AddHttpClient(nameof(IPlexClient)).ConfigurePrimaryHttpMessageHandler(() => handler);
            });
        });
        _factories.Add(factory);
        return factory.CreateClient();
    }

    private static FakePlexHandler ServerWithMovies() => FakePlexHandler.WithServer()
        .Route("/library/sections/1/all", "movie.json")
        .Route("/library/sections/3/all", "subtitles_audio.json");

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
        using var client = CreateClient(ServerWithMovies(), store);

        await client.PostAsync("/plex/movies/sync", null);
        var last = await WaitForFinishedRunAsync(client);
        var all = await ReadJsonAsync(await client.GetAsync("/plex/status"));

        Assert.That(last.GetProperty("state").GetString(), Is.EqualTo("succeeded"));
        Assert.That(last.GetProperty("itemsScanned").GetInt32(), Is.EqualTo(2));
        Assert.That(last.GetProperty("endedAt").ValueKind, Is.EqualTo(JsonValueKind.String));
        Assert.That(all.EnumerateArray().Single().GetProperty("lastRun").GetProperty("state").GetString(), Is.EqualTo("succeeded"));
        Assert.That(store.Runs.Single().ItemsScanned, Is.EqualTo(2));
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
}
