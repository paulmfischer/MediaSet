using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Cronos;
using MediaSet.Api.Features.Images.Models;
using MediaSet.Api.Features.Images.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using NUnit.Framework;

namespace MediaSet.Api.Tests.Features.Images.Services;

[TestFixture]
public class BackgroundImageLookupSchedulingTests
{
    private static readonly TimeZoneInfo NewYork = TimeZoneInfo.FindSystemTimeZoneById("America/New_York");

    private static DateTimeOffset Utc(int y, int m, int d, int h = 0, int min = 0) =>
        new(y, m, d, h, min, 0, TimeSpan.Zero);

    // --- Next occurrence ---

    [Test]
    public void GetNextOccurrence_DailyScheduleBeforeRunTime_ReturnsSameDayInUtc()
    {
        var cron = CronExpression.Parse("0 2 * * *");

        var next = cron.GetNextOccurrence(Utc(2026, 5, 1, 1, 30), TimeZoneInfo.Utc);

        Assert.That(next, Is.EqualTo(Utc(2026, 5, 1, 2)));
    }

    [Test]
    public void GetNextOccurrence_DailyScheduleExactlyAtRunTime_ReturnsNextDay()
    {
        var cron = CronExpression.Parse("0 2 * * *");

        var next = cron.GetNextOccurrence(Utc(2026, 5, 1, 2), TimeZoneInfo.Utc);

        Assert.That(next, Is.EqualTo(Utc(2026, 5, 2, 2)));
    }

    [Test]
    public void GetNextOccurrence_ScheduleInNewYorkSummer_UsesDaylightOffset()
    {
        var cron = CronExpression.Parse("0 2 * * *");

        var next = cron.GetNextOccurrence(Utc(2026, 7, 1, 12), NewYork);

        Assert.That(next, Is.EqualTo(Utc(2026, 7, 2, 6)));
        Assert.That(next!.Value.Offset, Is.EqualTo(TimeSpan.FromHours(-4)));
    }

    [Test]
    public void GetNextOccurrence_ScheduleInNewYorkWinter_UsesStandardOffset()
    {
        var cron = CronExpression.Parse("0 2 * * *");

        var next = cron.GetNextOccurrence(Utc(2026, 1, 15, 12), NewYork);

        Assert.That(next, Is.EqualTo(Utc(2026, 1, 16, 7)));
    }

    [Test]
    public void GetNextOccurrence_TimeSkippedBySpringForward_RunsOnceAtEndOfGap()
    {
        // 2026-03-08 02:30 America/New_York does not exist (clocks jump 02:00 -> 03:00).
        var cron = CronExpression.Parse("30 2 * * *");

        var next = cron.GetNextOccurrence(Utc(2026, 3, 8, 5), NewYork);

        Assert.That(next, Is.EqualTo(Utc(2026, 3, 8, 7)));
    }

    [Test]
    public void GetNextOccurrence_TimeRepeatedByFallBack_RunsOnceAtFirstOccurrence()
    {
        // 2026-11-01 01:30 America/New_York occurs twice (05:30Z EDT and 06:30Z EST).
        var cron = CronExpression.Parse("30 1 * * *");

        var first = cron.GetNextOccurrence(Utc(2026, 11, 1, 4), NewYork);
        var second = cron.GetNextOccurrence(first!.Value, NewYork);

        Assert.That(first, Is.EqualTo(Utc(2026, 11, 1, 5, 30)));
        Assert.That(second, Is.EqualTo(Utc(2026, 11, 2, 6, 30)));
    }

    [Test]
    public void GetNextOccurrence_ScheduleWithNoOccurrence_ReturnsNull()
    {
        var cron = CronExpression.Parse("0 0 31 2 *");

        var next = cron.GetNextOccurrence(Utc(2026, 5, 1), TimeZoneInfo.Utc);

        Assert.That(next, Is.Null);
    }

    // --- Service scheduling with a deterministic clock ---

    private static (BackgroundImageLookupService Service, List<DateTimeOffset> Runs, CancellationTokenSource Cts)
        CreateService(string schedule, FakeTimeProvider time, int stopAfterRuns)
    {
        var runs = new List<DateTimeOffset>();
        var cts = new CancellationTokenSource();
        var scopeFactory = new Mock<IServiceScopeFactory>();
        scopeFactory.Setup(f => f.CreateScope()).Returns(() =>
        {
            runs.Add(time.GetUtcNow());
            if (runs.Count >= stopAfterRuns)
            {
                // Last expected run: stop the service as a shutdown during the run would.
                cts.Cancel();
                cts.Token.ThrowIfCancellationRequested();
            }
            throw new InvalidOperationException("Simulated run failure");
        });
        var options = Options.Create(new BackgroundImageLookupConfiguration { Schedule = schedule });
        var service = new BackgroundImageLookupService(scopeFactory.Object, options,
            NullLogger<BackgroundImageLookupService>.Instance, time);
        return (service, runs, cts);
    }

    [Test]
    public async Task ExecuteAsync_DailySchedule_StartsRunOnlyWhenNextOccurrenceIsReached()
    {
        var time = new FakeTimeProvider(Utc(2026, 5, 1, 1), TimeZoneInfo.Utc);
        var (service, runs, cts) = CreateService("0 2 * * *", time, stopAfterRuns: 1);
        using var _ = cts;

        await service.StartAsync(cts.Token);
        await time.WaitForTimerAsync();
        time.Advance(TimeSpan.FromMinutes(59));
        Assert.That(runs, Is.Empty);
        time.Advance(TimeSpan.FromMinutes(1));
        await service.ExecuteTask!.WaitAsync(TimeSpan.FromSeconds(10));

        Assert.That(runs, Is.EqualTo([Utc(2026, 5, 1, 2)]));
    }

    [Test]
    public async Task ExecuteAsync_ScheduleInLocalTimeZone_UsesProviderTimeZoneForRunTime()
    {
        var time = new FakeTimeProvider(Utc(2026, 7, 1, 12), NewYork);
        var (service, runs, cts) = CreateService("0 2 * * *", time, stopAfterRuns: 1);
        using var _ = cts;

        await service.StartAsync(cts.Token);
        await time.WaitForTimerAsync();
        time.Advance(TimeSpan.FromHours(18));
        await service.ExecuteTask!.WaitAsync(TimeSpan.FromSeconds(10));

        Assert.That(runs, Is.EqualTo([Utc(2026, 7, 2, 6)]));
    }

    [Test]
    public async Task ExecuteAsync_TimeSkippedBySpringForward_RunsOnceAtEndOfGap()
    {
        var time = new FakeTimeProvider(Utc(2026, 3, 8, 5), NewYork);
        var (service, runs, cts) = CreateService("30 2 * * *", time, stopAfterRuns: 1);
        using var _ = cts;

        await service.StartAsync(cts.Token);
        await time.WaitForTimerAsync();
        time.Advance(TimeSpan.FromHours(1) + TimeSpan.FromMinutes(59));
        Assert.That(runs, Is.Empty);
        time.Advance(TimeSpan.FromMinutes(1));
        await service.ExecuteTask!.WaitAsync(TimeSpan.FromSeconds(10));

        Assert.That(runs, Is.EqualTo([Utc(2026, 3, 8, 7)]));
    }

    [Test]
    public async Task ExecuteAsync_TimeRepeatedByFallBack_RunsOncePerDayAtFirstOccurrence()
    {
        var time = new FakeTimeProvider(Utc(2026, 11, 1, 4), NewYork);
        var (service, runs, cts) = CreateService("30 1 * * *", time, stopAfterRuns: 2);
        using var _ = cts;

        await service.StartAsync(cts.Token);
        await time.WaitForTimerAsync();
        time.Advance(TimeSpan.FromHours(1.5)); // 05:30Z, first 01:30 (EDT); run fails
        await time.WaitForTimerAsync();        // one-minute retry delay
        time.Advance(TimeSpan.FromMinutes(1));
        await time.WaitForTimerAsync();        // next occurrence, not the repeated 01:30 EST
        time.Advance(TimeSpan.FromHours(24) + TimeSpan.FromMinutes(59));
        await service.ExecuteTask!.WaitAsync(TimeSpan.FromSeconds(10));

        Assert.That(runs, Is.EqualTo([Utc(2026, 11, 1, 5, 30), Utc(2026, 11, 2, 6, 30)]));
    }

    [Test]
    public async Task ExecuteAsync_RunFails_RetriesAfterOneMinuteOnTheProvidedClock()
    {
        var time = new FakeTimeProvider(Utc(2026, 5, 1, 1), TimeZoneInfo.Utc);
        var (service, runs, cts) = CreateService("0 2 * * *", time, stopAfterRuns: 2);
        using var _ = cts;

        await service.StartAsync(cts.Token);
        await time.WaitForTimerAsync();
        time.Advance(TimeSpan.FromHours(1));
        await time.WaitForTimerAsync(); // retry delay timer
        Assert.That(runs, Has.Count.EqualTo(1));
        time.Advance(TimeSpan.FromSeconds(59));
        Assert.That(time.ActiveTimers, Is.EqualTo(1));
        time.Advance(TimeSpan.FromSeconds(1));
        await time.WaitForTimerAsync(); // rescheduled for the next day
        time.Advance(TimeSpan.FromHours(23) + TimeSpan.FromMinutes(59));
        await service.ExecuteTask!.WaitAsync(TimeSpan.FromSeconds(10));

        Assert.That(runs, Is.EqualTo([Utc(2026, 5, 1, 2), Utc(2026, 5, 2, 2)]));
    }

    [Test]
    public async Task ExecuteAsync_CancelledWhileWaiting_StopsWithoutRunning()
    {
        var time = new FakeTimeProvider(Utc(2026, 5, 1, 1), TimeZoneInfo.Utc);
        var (service, runs, cts) = CreateService("0 2 * * *", time, stopAfterRuns: 1);
        using var _ = cts;

        await service.StartAsync(CancellationToken.None);
        await time.WaitForTimerAsync();
        await service.StopAsync(CancellationToken.None);

        Assert.That(service.ExecuteTask!.IsCompletedSuccessfully, Is.True);
        Assert.That(runs, Is.Empty);
    }

    [Test]
    public async Task ExecuteAsync_ScheduleWithNoOccurrence_StopsWithoutRunning()
    {
        var time = new FakeTimeProvider(Utc(2026, 5, 1, 1), TimeZoneInfo.Utc);
        var (service, runs, cts) = CreateService("0 0 31 2 *", time, stopAfterRuns: 1);
        using var _ = cts;

        await service.StartAsync(cts.Token);
        await service.ExecuteTask!.WaitAsync(TimeSpan.FromSeconds(10));

        Assert.That(runs, Is.Empty);
        Assert.That(time.ActiveTimers, Is.Zero);
    }

    [Test]
    public async Task ExecuteAsync_InvalidExpression_FaultsBeforeScheduling()
    {
        var time = new FakeTimeProvider(Utc(2026, 5, 1, 1), TimeZoneInfo.Utc);
        var (service, runs, cts) = CreateService("not a cron", time, stopAfterRuns: 1);
        using var _ = cts;

        await service.StartAsync(cts.Token);

        await Assert.ThrowsAsync<CronFormatException>(async () => await service.ExecuteTask!);
        Assert.That(runs, Is.Empty);
    }

    /// <summary>Manually advanced clock; timers fire (and repeat per their period) only when <see cref="Advance"/> passes them.</summary>
    private sealed class FakeTimeProvider(DateTimeOffset start, TimeZoneInfo? zone = null) : TimeProvider
    {
        private readonly object _gate = new();
        private readonly List<FakeTimer> _timers = [];
        private DateTimeOffset _now = start;

        public override TimeZoneInfo LocalTimeZone => zone ?? TimeZoneInfo.Utc;

        public override DateTimeOffset GetUtcNow() { lock (_gate) { return _now; } }

        public int ActiveTimers { get { lock (_gate) { return _timers.Count(t => t.DueAt != null); } } }

        /// <summary>Waits until a timer is pending, i.e. the service is blocked on the clock.</summary>
        public async Task WaitForTimerAsync()
        {
            var deadline = DateTime.UtcNow.AddSeconds(10);
            while (ActiveTimers == 0)
            {
                Assert.That(DateTime.UtcNow, Is.LessThan(deadline), "Timed out waiting for a timer");
                await Task.Delay(5);
            }
        }

        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            var timer = new FakeTimer(this, callback, state);
            lock (_gate)
            {
                _timers.Add(timer);
                timer.Schedule(_now, dueTime, period);
            }
            return timer;
        }

        public void Advance(TimeSpan by)
        {
            List<FakeTimer> due = [];
            lock (_gate)
            {
                _now += by;
                foreach (var timer in _timers)
                {
                    if (timer.DueAt <= _now)
                    {
                        due.Add(timer);
                        timer.Reschedule();
                    }
                }
            }
            due.ForEach(t => t.Fire());
        }

        private sealed class FakeTimer(FakeTimeProvider owner, TimerCallback callback, object? state) : ITimer
        {
            private TimeSpan _period;
            public DateTimeOffset? DueAt { get; private set; }

            // Callers hold owner._gate.
            public void Schedule(DateTimeOffset now, TimeSpan dueTime, TimeSpan period)
            {
                DueAt = dueTime == Timeout.InfiniteTimeSpan ? null : now + dueTime;
                _period = period == Timeout.InfiniteTimeSpan ? TimeSpan.Zero : period;
            }

            public void Reschedule() => DueAt = _period > TimeSpan.Zero ? DueAt + _period : null;

            public void Fire() => callback(state);

            public bool Change(TimeSpan dueTime, TimeSpan period)
            {
                lock (owner._gate)
                {
                    Schedule(owner._now, dueTime, period);
                }
                return true;
            }

            public void Dispose()
            {
                lock (owner._gate)
                {
                    owner._timers.Remove(this);
                }
            }

            public ValueTask DisposeAsync() { Dispose(); return ValueTask.CompletedTask; }
        }
    }
}
