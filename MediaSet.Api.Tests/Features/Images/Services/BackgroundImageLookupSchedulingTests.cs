using System;
using System.Collections.Generic;
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

    // --- Configuration validation ---

    [TestCase("0 2 * * *")]
    [TestCase("0 */6 * * *")]
    [TestCase("0 * * * *")]
    [TestCase("30 3 * * 0")]
    public void IsValid_ValidHourlyOrSlowerSchedule_ReturnsTrue(string schedule)
    {
        var config = new BackgroundImageLookupConfiguration { Schedule = schedule };

        var valid = config.IsValid(out var error, new FakeTimeProvider(Utc(2026, 5, 1, 12)));

        Assert.That(valid, Is.True);
        Assert.That(error, Is.Null);
    }

    [TestCase("*/30 * * * *")]
    [TestCase("* * * * *")]
    public void IsValid_ScheduleMoreFrequentThanHourly_ReturnsFalseWithIntervalMessage(string schedule)
    {
        var config = new BackgroundImageLookupConfiguration { Schedule = schedule };

        var valid = config.IsValid(out var error, new FakeTimeProvider(Utc(2026, 5, 1, 12)));

        Assert.That(valid, Is.False);
        Assert.That(error, Does.StartWith("Schedule interval must be at least 1 hour."));
    }

    [TestCase("")]
    [TestCase("   ")]
    public void IsValid_BlankSchedule_ReturnsFalseWithRequiredMessage(string schedule)
    {
        var config = new BackgroundImageLookupConfiguration { Schedule = schedule };

        var valid = config.IsValid(out var error, new FakeTimeProvider(Utc(2026, 5, 1)));

        Assert.That(valid, Is.False);
        Assert.That(error, Is.EqualTo("Schedule is required"));
    }

    [TestCase("not a cron")]
    [TestCase("0 25 * * *")]
    [TestCase("0 2 * *")]
    [TestCase("0 0 2 * * *")]
    [TestCase("H 2 * * *")]
    public void IsValid_UnparseableExpression_ReturnsFalseWithParseMessage(string schedule)
    {
        var config = new BackgroundImageLookupConfiguration { Schedule = schedule };

        var valid = config.IsValid(out var error, new FakeTimeProvider(Utc(2026, 5, 1)));

        Assert.That(valid, Is.False);
        Assert.That(error, Does.StartWith($"Failed to parse cron expression '{schedule}':"));
    }

    [Test]
    public void IsValid_ScheduleWithNoOccurrence_ReturnsFalseWithNoOccurrenceMessage()
    {
        var config = new BackgroundImageLookupConfiguration { Schedule = "0 0 31 2 *" };

        var valid = config.IsValid(out var error, new FakeTimeProvider(Utc(2026, 5, 1)));

        Assert.That(valid, Is.False);
        Assert.That(error, Is.EqualTo("Invalid cron expression '0 0 31 2 *': no next occurrence found"));
    }

    [Test]
    public void IsValid_NonPositiveSettings_ReturnsFalse()
    {
        var config = new BackgroundImageLookupConfiguration { BatchSize = 0 };

        var valid = config.IsValid(out var error);

        Assert.That(valid, Is.False);
        Assert.That(error, Does.StartWith("Retry delays, strategy version"));
    }

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
        scopeFactory.Setup(f => f.CreateScope()).Callback(() =>
        {
            runs.Add(time.GetUtcNow());
            if (runs.Count >= stopAfterRuns)
            {
                cts.Cancel();
            }
        }).Throws(new OperationCanceledException(cts.Token));
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

        await service.StartAsync(cts.Token);
        await time.WaitForTimerAsync();
        time.Advance(TimeSpan.FromHours(18));
        await service.ExecuteTask!.WaitAsync(TimeSpan.FromSeconds(10));

        Assert.That(runs, Is.EqualTo([Utc(2026, 7, 2, 6)]));
    }

    [Test]
    public async Task ExecuteAsync_CancelledWhileWaiting_StopsWithoutRunning()
    {
        var time = new FakeTimeProvider(Utc(2026, 5, 1, 1), TimeZoneInfo.Utc);
        var (service, runs, cts) = CreateService("0 2 * * *", time, stopAfterRuns: 1);

        await service.StartAsync(CancellationToken.None);
        await time.WaitForTimerAsync();
        await service.StopAsync(CancellationToken.None);

        Assert.That(service.ExecuteTask!.IsCompletedSuccessfully, Is.True);
        Assert.That(runs, Is.Empty);
        cts.Dispose();
    }

    [Test]
    public async Task ExecuteAsync_ScheduleWithNoOccurrence_StopsWithoutRunning()
    {
        var time = new FakeTimeProvider(Utc(2026, 5, 1, 1), TimeZoneInfo.Utc);
        var (service, runs, cts) = CreateService("0 0 31 2 *", time, stopAfterRuns: 1);

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

        await service.StartAsync(cts.Token);

        await Assert.ThrowsAsync<CronFormatException>(async () => await service.ExecuteTask!);
        Assert.That(runs, Is.Empty);
    }

    /// <summary>Manually advanced clock whose timers fire only when <see cref="Advance"/> passes them.</summary>
    private sealed class FakeTimeProvider(DateTimeOffset start, TimeZoneInfo? zone = null) : TimeProvider
    {
        private readonly object _gate = new();
        private readonly List<FakeTimer> _timers = [];
        private DateTimeOffset _now = start;
        private readonly TaskCompletionSource _timerCreated = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override TimeZoneInfo LocalTimeZone => zone ?? TimeZoneInfo.Utc;

        public override DateTimeOffset GetUtcNow() { lock (_gate) { return _now; } }

        public int ActiveTimers { get { lock (_gate) { return _timers.Count; } } }

        public Task WaitForTimerAsync() => _timerCreated.Task.WaitAsync(TimeSpan.FromSeconds(10));

        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            var timer = new FakeTimer(this, callback, state, _now + dueTime);
            lock (_gate)
            {
                _timers.Add(timer);
            }
            _timerCreated.TrySetResult();
            return timer;
        }

        public void Advance(TimeSpan by)
        {
            List<FakeTimer> due;
            lock (_gate)
            {
                _now += by;
                due = _timers.FindAll(t => t.DueAt <= _now);
                _timers.RemoveAll(t => t.DueAt <= _now);
            }
            due.ForEach(t => t.Fire());
        }

        private void Remove(FakeTimer timer)
        {
            lock (_gate)
            {
                _timers.Remove(timer);
            }
        }

        private sealed class FakeTimer(FakeTimeProvider owner, TimerCallback callback, object? state, DateTimeOffset dueAt) : ITimer
        {
            public DateTimeOffset DueAt { get; } = dueAt;
            public void Fire() => callback(state);
            public bool Change(TimeSpan dueTime, TimeSpan period) => false;
            public void Dispose() => owner.Remove(this);
            public ValueTask DisposeAsync() { Dispose(); return ValueTask.CompletedTask; }
        }
    }
}
