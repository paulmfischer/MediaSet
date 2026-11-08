using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediaSet.Api.Features.Plex.Models;
using MediaSet.Api.Features.Plex.Services;
using MediaSet.Api.Infrastructure.Database;
using MediaSet.Api.Infrastructure.Plex;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using Moq;
using NUnit.Framework;

namespace MediaSet.Api.Tests.Features.Plex.Services;

[TestFixture]
public class PlexSyncServiceTests
{
    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (!condition())
        {
            if (DateTime.UtcNow > deadline)
            {
                throw new TimeoutException("Condition was not met in time.");
            }
            await Task.Delay(10);
        }
    }

    private sealed class Harness
    {
        public InMemoryPlexSyncRunStore Store { get; } = new();
        public CancellationTokenSource Shutdown { get; } = new();
        public Mock<IPlexClient> Client { get; } = new();
        public PlexSyncService Service { get; }

        public Harness()
        {
            var lifetime = new Mock<IHostApplicationLifetime>();
            lifetime.SetupGet(l => l.ApplicationStopping).Returns(Shutdown.Token);
            var services = new ServiceCollection().AddSingleton(Client.Object).BuildServiceProvider();
            Service = new PlexSyncService(
                services,
                Store,
                lifetime.Object,
                Options.Create(new PlexConfiguration { Token = "secret-token" }),
                NullLogger<PlexSyncService>.Instance);
        }
    }

    [Test]
    public async Task StartAsync_TwoEntityTypes_RunOneAtATimeBehindSharedLock()
    {
        var h = new Harness();
        var gate = new TaskCompletionSource();
        var concurrent = 0;
        var maxConcurrent = 0;
        h.Client.Setup(c => c.GetLibrariesForEntityTypeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(async (string _, CancellationToken ct) =>
            {
                var now = Interlocked.Increment(ref concurrent);
                maxConcurrent = Math.Max(maxConcurrent, now);
                await gate.Task.WaitAsync(ct);
                Interlocked.Decrement(ref concurrent);
                return (IReadOnlyList<PlexLibrary>)[];
            });

        var first = await h.Service.StartAsync("movies", PlexSyncTriggers.Manual, CancellationToken.None);
        var second = await h.Service.StartAsync("shows", PlexSyncTriggers.Manual, CancellationToken.None);
        await WaitUntilAsync(() => concurrent == 1);
        var whileFirstRuns = h.Store.Runs.Select(r => r.State).ToList();
        gate.SetResult();
        await WaitUntilAsync(() => h.Store.Runs.All(r => r.State == PlexSyncRunStates.Succeeded));

        Assert.That(first.Started && second.Started, Is.True);
        Assert.That(whileFirstRuns, Is.EqualTo(new List<string> { PlexSyncRunStates.Running, PlexSyncRunStates.Queued }));
        Assert.That(maxConcurrent, Is.EqualTo(1));
    }

    [Test]
    public async Task StartAsync_ApplicationStopping_CancelsPlexCallsAndRecordsInterrupted()
    {
        var h = new Harness();
        CancellationToken seen = default;
        h.Client.Setup(c => c.GetLibrariesForEntityTypeAsync("movies", It.IsAny<CancellationToken>()))
            .Returns(async (string _, CancellationToken ct) =>
            {
                seen = ct;
                await Task.Delay(Timeout.Infinite, ct);
                return (IReadOnlyList<PlexLibrary>)[];
            });

        await h.Service.StartAsync("movies", PlexSyncTriggers.Manual, CancellationToken.None);
        await WaitUntilAsync(() => seen.CanBeCanceled);
        h.Shutdown.Cancel();
        await WaitUntilAsync(() => h.Store.Runs.Single().State == PlexSyncRunStates.Interrupted);

        Assert.That(h.Store.Runs.Single().State, Is.EqualTo(PlexSyncRunStates.Interrupted));
        Assert.That(seen.IsCancellationRequested, Is.True);
    }

    [Test]
    public async Task StartAsync_ApplicationStoppingWhileQueued_RecordsInterruptedWithoutCallingPlex()
    {
        var h = new Harness();
        h.Client.Setup(c => c.GetLibrariesForEntityTypeAsync("movies", It.IsAny<CancellationToken>()))
            .Returns(async (string _, CancellationToken ct) =>
            {
                await Task.Delay(Timeout.Infinite, ct);
                return (IReadOnlyList<PlexLibrary>)[];
            });
        await h.Service.StartAsync("movies", PlexSyncTriggers.Manual, CancellationToken.None);
        await h.Service.StartAsync("shows", PlexSyncTriggers.Manual, CancellationToken.None);

        h.Shutdown.Cancel();
        await WaitUntilAsync(() => h.Store.Runs.All(r => r.State == PlexSyncRunStates.Interrupted));

        h.Client.Verify(c => c.GetLibrariesForEntityTypeAsync("shows", It.IsAny<CancellationToken>()), Times.Never);
        Assert.That(h.Store.Runs.Single(r => r.EntityType == "shows").EndedAt, Is.Not.Null);
    }

    [Test]
    public async Task StartAsync_SameTypeWhileInProgress_ReturnsPersistedCurrentRun()
    {
        var h = new Harness();
        h.Client.Setup(c => c.GetLibrariesForEntityTypeAsync("movies", It.IsAny<CancellationToken>()))
            .Returns(async (string _, CancellationToken ct) =>
            {
                await Task.Delay(Timeout.Infinite, ct);
                return (IReadOnlyList<PlexLibrary>)[];
            });

        var results = await Task.WhenAll(Enumerable.Range(0, 8)
            .Select(_ => Task.Run(() => h.Service.StartAsync("movies", PlexSyncTriggers.Manual, CancellationToken.None))));

        Assert.That(results.Count(r => r.Started), Is.EqualTo(1));
        Assert.That(results.Select(r => r.Run.Id), Has.All.Not.Null.And.All.EqualTo(h.Store.Runs.Single().Id));
        h.Shutdown.Cancel();
    }

    [Test]
    public async Task StopAsync_RunInProgress_WaitsForOutcomeToBeRecorded()
    {
        var h = new Harness();
        h.Client.Setup(c => c.GetLibrariesForEntityTypeAsync("movies", It.IsAny<CancellationToken>()))
            .Returns(async (string _, CancellationToken ct) =>
            {
                await Task.Delay(Timeout.Infinite, ct);
                return (IReadOnlyList<PlexLibrary>)[];
            });
        await h.Service.StartAsync("movies", PlexSyncTriggers.Manual, CancellationToken.None);

        h.Shutdown.Cancel();
        await h.Service.StopAsync(CancellationToken.None);

        Assert.That(h.Store.Runs.Single().State, Is.EqualTo(PlexSyncRunStates.Interrupted));
    }

    [Test]
    public async Task StartAsync_ClientMessageContainsToken_StoredMessageIsRedacted()
    {
        var h = new Harness();
        h.Client.Setup(c => c.GetLibrariesForEntityTypeAsync("movies", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new PlexClientException(null, "Plex said secret-token"));

        await h.Service.StartAsync("movies", PlexSyncTriggers.Manual, CancellationToken.None);
        await WaitUntilAsync(() => h.Store.Runs.Single().State == PlexSyncRunStates.Failed);

        Assert.That(h.Store.Runs.Single().State, Is.EqualTo(PlexSyncRunStates.Failed));
        Assert.That(h.Store.Runs.Single().Message, Does.Not.Contain("secret-token"));
    }
}

[TestFixture]
public class PlexSyncRunStoreTests
{
    private static string RenderKeys(CreateIndexModel<PlexSyncRun> model) =>
        model.Keys.Render(new RenderArgs<PlexSyncRun>(
            MongoDB.Bson.Serialization.BsonSerializer.LookupSerializer<PlexSyncRun>(),
            MongoDB.Bson.Serialization.BsonSerializer.SerializerRegistry)).ToString();

    [Test]
    public async Task InsertAsync_IndexNotYetEnsured_CreatesIndexBeforeInserting()
    {
        var indexes = new Mock<IMongoIndexManager<PlexSyncRun>>();
        var collection = new Mock<IMongoCollection<PlexSyncRun>>();
        collection.SetupGet(c => c.Indexes).Returns(indexes.Object);
        collection.Setup(c => c.InsertOneAsync(It.IsAny<PlexSyncRun>(), It.IsAny<InsertOneOptions>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var database = new Mock<IDatabaseService>();
        database.Setup(d => d.GetCollection<PlexSyncRun>()).Returns(collection.Object);
        var store = new PlexSyncRunStore(database.Object);

        await store.InsertAsync(new PlexSyncRun(), CancellationToken.None);
        await store.InsertAsync(new PlexSyncRun(), CancellationToken.None);

        indexes.Verify(i => i.CreateOneAsync(It.IsAny<CreateIndexModel<PlexSyncRun>>(), It.IsAny<CreateOneIndexOptions>(), It.IsAny<CancellationToken>()), Times.Once);
        collection.Verify(c => c.InsertOneAsync(It.IsAny<PlexSyncRun>(), It.IsAny<InsertOneOptions>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Test]
    public async Task EnsureCreatedAsync_CreatesIndexOnEntityTypeAndStartedAtDescending()
    {
        var indexes = new Mock<IMongoIndexManager<PlexSyncRun>>();
        var collection = new Mock<IMongoCollection<PlexSyncRun>>();
        collection.SetupGet(c => c.Indexes).Returns(indexes.Object);
        var database = new Mock<IDatabaseService>();
        database.Setup(d => d.GetCollection<PlexSyncRun>()).Returns(collection.Object);
        var store = new PlexSyncRunStore(database.Object);

        await store.EnsureCreatedAsync(CancellationToken.None);

        indexes.Verify(i => i.CreateOneAsync(
            It.Is<CreateIndexModel<PlexSyncRun>>(m => m.Options.Name == PlexSyncRunStore.IndexName
                && RenderKeys(m) == "{ \"EntityType\" : 1, \"StartedAt\" : -1 }"),
            It.IsAny<CreateOneIndexOptions>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
