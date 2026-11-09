using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MediaSet.Api.Tests.Features.Plex;

/// <summary>Manually advanced clock with a configurable local time zone; timers fire only when time is advanced past them.</summary>
public sealed class FakeTimeProvider(DateTimeOffset start, TimeZoneInfo? zone = null) : TimeProvider
{
    private readonly object _gate = new();
    private readonly List<FakeTimer> _timers = [];
    private DateTimeOffset _now = start;

    public override TimeZoneInfo LocalTimeZone => zone ?? TimeZoneInfo.Utc;

    public override DateTimeOffset GetUtcNow()
    {
        lock (_gate)
        {
            return _now;
        }
    }

    public int ActiveTimers
    {
        get
        {
            lock (_gate)
            {
                return _timers.Count(t => t.DueAt != null);
            }
        }
    }

    public async Task WaitForTimerAsync(int count = 1)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (ActiveTimers < count)
        {
            if (DateTime.UtcNow > deadline)
            {
                throw new TimeoutException("Timed out waiting for a timer.");
            }
            await Task.Delay(5);
        }
    }

    /// <summary>Advances to the target in steps of at most a day, letting waiting loops re-arm between steps.</summary>
    public async Task AdvanceToAsync(DateTimeOffset target, int timers = 1)
    {
        while (GetUtcNow() < target)
        {
            await WaitForTimerAsync(timers);
            var step = target - GetUtcNow();
            Advance(step > TimeSpan.FromDays(1) ? TimeSpan.FromDays(1) : step);
            await Task.Delay(20);
        }
    }

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        var timer = new FakeTimer(this, callback, state);
        lock (_gate)
        {
            _timers.Add(timer);
            timer.Schedule(_now, dueTime);
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
                    timer.Clear();
                }
            }
        }
        due.ForEach(t => t.Fire());
    }

    private sealed class FakeTimer(FakeTimeProvider owner, TimerCallback callback, object? state) : ITimer
    {
        public DateTimeOffset? DueAt { get; private set; }

        // Callers hold owner._gate.
        public void Schedule(DateTimeOffset now, TimeSpan dueTime) =>
            DueAt = dueTime == Timeout.InfiniteTimeSpan ? null : now + dueTime;

        public void Clear() => DueAt = null;

        public void Fire() => callback(state);

        public bool Change(TimeSpan dueTime, TimeSpan period)
        {
            lock (owner._gate)
            {
                Schedule(owner._now, dueTime);
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

        public ValueTask DisposeAsync()
        {
            Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
