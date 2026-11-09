using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediaSet.Api.Features.Plex.Models;
using MediaSet.Api.Features.Plex.Services;
using MediaSet.Api.Infrastructure.Plex;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using NUnit.Framework;

namespace MediaSet.Api.Tests.Features.Plex.Services;

[TestFixture]
public class PlexSyncSchedulerTests
{
    private static readonly TimeZoneInfo Plus5 = TimeZoneInfo.CreateCustomTimeZone("Test+5", TimeSpan.FromHours(5), "Test+5", "Test+5");

    // Monday 2026-11-09 12:00 UTC; the next default run is Sunday 03:00 at +05:00 = 2026-11-14 22:00 UTC
    private static readonly DateTimeOffset Start = new(2026, 11, 9, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset FirstRun = new(2026, 11, 14, 22, 0, 0, TimeSpan.Zero);

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

    private static PlexConfiguration Config(params (string Type, bool Enabled, string Schedule)[] entities)
    {
        var config = new PlexConfiguration();
        foreach (var (type, enabled, schedule) in entities)
        {
            config.Entities[type] = new PlexEntityConfiguration { Enabled = enabled, Schedule = schedule };
        }
        return config;
    }

    private sealed class Harness
    {
        public FakeTimeProvider Time { get; } = new(Start, Plus5);
        public PlexScheduleInfo Info { get; } = new();
        public CapturingLoggerProvider Logs { get; } = new();
        public Mock<IPlexSyncService> Sync { get; } = new();
        public List<(string Type, string Trigger)> Starts { get; } = [];
        public PlexSyncScheduler Scheduler { get; private set; } = null!;

        public Harness(PlexConfiguration config, string[] enabledTypes, IPlexSyncService? sync = null)
        {
            Sync.Setup(s => s.StartAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((string type, string trigger, CancellationToken _) =>
                {
                    lock (Starts)
                    {
                        Starts.Add((type, trigger));
                    }
                    return new PlexSyncStartResult(new PlexSyncRun { Id = "r1", EntityType = type, State = PlexSyncRunStates.Running }, true);
                });
            Build(config, enabledTypes, sync ?? Sync.Object);
        }

        public void Build(PlexConfiguration config, string[] enabledTypes, IPlexSyncService sync)
        {
            var factory = LoggerFactory.Create(b => b.AddProvider(Logs));
            Scheduler = new PlexSyncScheduler(
                sync,
                new PlexEnablement(enabledTypes),
                Options.Create(config),
                Info,
                factory.CreateLogger<PlexSyncScheduler>(),
                Time);
        }

        /// <summary>Waits until the scheduler has published the next run of an entity type.</summary>
        public async Task<DateTimeOffset> NextRunAsync(string entityType)
        {
            await WaitUntilAsync(() => Info.GetNextRun(entityType) != null);
            return Info.GetNextRun(entityType)!.Value;
        }

        public int StartCount { get { lock (Starts) { return Starts.Count; } } }
    }

    [Test]
    public async Task ExecuteAsync_EnabledType_ReportsNextRunInLocalTimeBeforeFiring()
    {
        var h = new Harness(Config(("movies", true, PlexConfiguration.DefaultSchedule)), ["movies"]);

        await h.Scheduler.StartAsync(CancellationToken.None);
        var next = await h.NextRunAsync("movies");
        await h.Scheduler.StopAsync(CancellationToken.None);

        Assert.That(next, Is.EqualTo(FirstRun));
        Assert.That(next.Offset, Is.EqualTo(TimeSpan.FromHours(5)));
        Assert.That(h.StartCount, Is.EqualTo(0));
    }

    [Test]
    public async Task ExecuteAsync_ScheduleReached_StartsScheduledRunAndReportsFollowingRun()
    {
        var h = new Harness(Config(("movies", true, PlexConfiguration.DefaultSchedule)), ["movies"]);

        await h.Scheduler.StartAsync(CancellationToken.None);
        await h.Time.AdvanceToAsync(FirstRun);
        await WaitUntilAsync(() => h.StartCount == 1);
        await WaitUntilAsync(() => h.Info.GetNextRun("movies") == FirstRun.AddDays(7));
        await h.Scheduler.StopAsync(CancellationToken.None);

        Assert.That(h.Starts.Single(), Is.EqualTo(("movies", PlexSyncTriggers.Scheduled)));
    }

    [Test]
    public async Task ExecuteAsync_MissingScheduleKey_UsesDefaultSundayThreeAm()
    {
        // The entity configuration's own default, when no schedule is set
        var config = new PlexConfiguration();
        config.Entities["movies"] = new PlexEntityConfiguration { Enabled = true };
        var h = new Harness(config, ["movies"]);

        await h.Scheduler.StartAsync(CancellationToken.None);
        var next = await h.NextRunAsync("movies");
        await h.Scheduler.StopAsync(CancellationToken.None);

        Assert.That(next.DayOfWeek, Is.EqualTo(DayOfWeek.Sunday));
        Assert.That(next.Hour, Is.EqualTo(3));
    }

    [Test]
    public async Task ExecuteAsync_TwoTypesWithOwnSchedules_EachRunsOnItsOwnSchedule()
    {
        // movies: default Sunday; shows: every Wednesday at 04:00 local
        var h = new Harness(
            Config(("movies", true, PlexConfiguration.DefaultSchedule), ("shows", true, "0 4 * * 3")),
            ["movies", "shows"]);

        await h.Scheduler.StartAsync(CancellationToken.None);
        var showsNext = await h.NextRunAsync("shows");
        await h.Time.AdvanceToAsync(showsNext, timers: 2);
        await WaitUntilAsync(() => h.StartCount == 1);
        var moviesNext = h.Info.GetNextRun("movies");
        await h.Scheduler.StopAsync(CancellationToken.None);

        Assert.That(showsNext.DayOfWeek, Is.EqualTo(DayOfWeek.Wednesday));
        Assert.That(h.Starts.Single(), Is.EqualTo(("shows", PlexSyncTriggers.Scheduled)));
        Assert.That(moviesNext, Is.EqualTo(FirstRun));
    }

    [Test]
    public async Task ExecuteAsync_InvalidScheduleForOneType_DisablesOnlyThatTypeAndLogsWarning()
    {
        var h = new Harness(
            Config(("movies", true, "not a cron"), ("shows", true, PlexConfiguration.DefaultSchedule)),
            ["movies", "shows"]);

        await h.Scheduler.StartAsync(CancellationToken.None);
        await h.NextRunAsync("shows");
        await h.Time.AdvanceToAsync(FirstRun);
        await WaitUntilAsync(() => h.StartCount == 1);
        await h.Scheduler.StopAsync(CancellationToken.None);

        Assert.That(h.Starts.Single().Type, Is.EqualTo("shows"));
        Assert.That(h.Info.GetNextRun("movies"), Is.Null);
        Assert.That(h.Logs.Lines, Has.Some.Contain("'movies' has an invalid schedule").And.Contain("not a cron"));
    }

    [Test]
    public async Task ExecuteAsync_DisabledOrUnconfiguredType_SchedulesNothing()
    {
        // "movies" is enabled in PlexEnablement but its configuration is disabled; "shows" has no configuration
        var h = new Harness(Config(("movies", false, PlexConfiguration.DefaultSchedule)), ["movies", "shows"]);

        await h.Scheduler.StartAsync(CancellationToken.None);
        await h.Scheduler.StopAsync(CancellationToken.None);

        Assert.That(h.Time.ActiveTimers, Is.EqualTo(0));
        Assert.That(h.Info.GetNextRun("movies"), Is.Null);
        Assert.That(h.Info.GetNextRun("shows"), Is.Null);
        Assert.That(h.StartCount, Is.EqualTo(0));
    }

    [Test]
    public async Task ExecuteAsync_NothingEnabled_SchedulesNothing()
    {
        var h = new Harness(Config(("movies", true, PlexConfiguration.DefaultSchedule)), []);

        await h.Scheduler.StartAsync(CancellationToken.None);
        await h.Scheduler.StopAsync(CancellationToken.None);

        Assert.That(h.Time.ActiveTimers, Is.EqualTo(0));
        Assert.That(h.StartCount, Is.EqualTo(0));
    }

    [Test]
    public async Task ExecuteAsync_StartAsyncThrows_LogsAndKeepsSchedule()
    {
        var h = new Harness(Config(("movies", true, PlexConfiguration.DefaultSchedule)), ["movies"]);
        h.Sync.Setup(s => s.StartAsync("movies", PlexSyncTriggers.Scheduled, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("db down"));

        await h.Scheduler.StartAsync(CancellationToken.None);
        await h.Time.AdvanceToAsync(FirstRun);
        await WaitUntilAsync(() => h.Info.GetNextRun("movies") == FirstRun.AddDays(7));
        await h.Scheduler.StopAsync(CancellationToken.None);

        Assert.That(h.Logs.Lines, Has.Some.Contain("could not start").And.Contain("db down"));
    }

    [Test]
    public async Task StopAsync_WhileWaiting_StopsCleanlyAndClearsNextRun()
    {
        var h = new Harness(Config(("movies", true, PlexConfiguration.DefaultSchedule)), ["movies"]);

        await h.Scheduler.StartAsync(CancellationToken.None);
        await h.Time.WaitForTimerAsync();
        await h.Scheduler.StopAsync(CancellationToken.None);

        Assert.That(h.Scheduler.ExecuteTask?.IsCompletedSuccessfully, Is.True);
        Assert.That(h.Info.GetNextRun("movies"), Is.Null);
    }

    [Test]
    public async Task ExecuteAsync_SameTypeAlreadyActive_SkipsAndLogs()
    {
        var h = new Harness(Config(("movies", true, PlexConfiguration.DefaultSchedule)), ["movies"]);
        h.Sync.Setup(s => s.StartAsync("movies", PlexSyncTriggers.Scheduled, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PlexSyncStartResult(new PlexSyncRun { Id = "active", EntityType = "movies", State = PlexSyncRunStates.Running }, false));

        await h.Scheduler.StartAsync(CancellationToken.None);
        await h.Time.AdvanceToAsync(FirstRun);
        await WaitUntilAsync(() => h.Info.GetNextRun("movies") == FirstRun.AddDays(7));
        await h.Scheduler.StopAsync(CancellationToken.None);

        Assert.That(h.Logs.Lines, Has.Some.Contain("skipped").And.Contain("already running"));
    }

    private sealed class RealSyncHarness
    {
        public InMemoryPlexSyncRunStore Store { get; } = new();
        public Mock<IPlexClient> Client { get; } = new();
        public PlexSyncService Service { get; }

        public RealSyncHarness()
        {
            var lifetime = new Mock<IHostApplicationLifetime>();
            lifetime.SetupGet(l => l.ApplicationStopping).Returns(CancellationToken.None);
            var items = new InMemoryPlexItemStore();
            var movies = InMemoryPlexItemStore.MovieService();
            var services = new ServiceCollection()
                .AddSingleton(Client.Object)
                .AddSingleton<IPlexItemStore>(items)
                .AddScoped(_ => movies.Object)
                .BuildServiceProvider();
            Service = new PlexSyncService(
                services,
                Store,
                lifetime.Object,
                Options.Create(new PlexConfiguration { Token = "secret-token" }),
                Microsoft.Extensions.Logging.Abstractions.NullLogger<PlexSyncService>.Instance);
        }
    }

    [Test]
    public async Task ExecuteAsync_LockHeldByManualRun_ScheduledRunQueuesNeverParallelAndSameTypeIsSkipped()
    {
        var real = new RealSyncHarness();
        var gate = new TaskCompletionSource();
        var concurrent = 0;
        var maxConcurrent = 0;
        real.Client.Setup(c => c.GetLibrariesForEntityTypeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(async (string _, CancellationToken ct) =>
            {
                var now = Interlocked.Increment(ref concurrent);
                maxConcurrent = Math.Max(maxConcurrent, now);
                await gate.Task.WaitAsync(ct);
                Interlocked.Decrement(ref concurrent);
                return (IReadOnlyList<PlexLibrary>)[];
            });
        var h = new Harness(
            Config(("movies", true, PlexConfiguration.DefaultSchedule), ("shows", true, PlexConfiguration.DefaultSchedule)),
            ["movies", "shows"],
            real.Service);

        // A manual movies run holds the lock when both schedules fire at the same moment
        await real.Service.StartAsync("movies", PlexSyncTriggers.Manual, CancellationToken.None);
        await WaitUntilAsync(() => concurrent == 1);
        await h.Scheduler.StartAsync(CancellationToken.None);
        await h.NextRunAsync("shows");
        await h.Time.AdvanceToAsync(FirstRun, timers: 2);
        await WaitUntilAsync(() => real.Store.Runs.Count == 2);
        var whileHeld = real.Store.Runs.Select(r => (r.EntityType, r.Trigger, r.State)).ToList();
        gate.SetResult();
        await WaitUntilAsync(() => real.Store.Runs.All(r => r.State == PlexSyncRunStates.Succeeded));
        await h.Scheduler.StopAsync(CancellationToken.None);

        Assert.That(whileHeld, Is.EquivalentTo(new[]
        {
            ("movies", PlexSyncTriggers.Manual, PlexSyncRunStates.Running),
            ("shows", PlexSyncTriggers.Scheduled, PlexSyncRunStates.Queued)
        }));
        Assert.That(real.Store.Runs, Has.Count.EqualTo(2), "the scheduled movies run is skipped, not duplicated");
        Assert.That(maxConcurrent, Is.EqualTo(1));
        Assert.That(h.Logs.Lines, Has.Some.Contain("'shows' queued behind"));
        Assert.That(h.Logs.Lines, Has.Some.Contain("'movies' skipped"));
    }
}
