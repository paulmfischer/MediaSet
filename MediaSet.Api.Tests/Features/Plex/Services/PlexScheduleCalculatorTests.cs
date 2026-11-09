using System;
using MediaSet.Api.Features.Plex.Services;
using MediaSet.Api.Infrastructure.Plex;
using NUnit.Framework;

namespace MediaSet.Api.Tests.Features.Plex.Services;

[TestFixture]
public class PlexScheduleCalculatorTests
{
    // UTC+5 without DST, so local-time expectations are the same on every machine
    private static readonly TimeZoneInfo Plus5 = TimeZoneInfo.CreateCustomTimeZone("Test+5", TimeSpan.FromHours(5), "Test+5", "Test+5");

    [Test]
    public void GetNextRun_DefaultSchedule_IsNextSundayAtThreeLocal()
    {
        // Monday 2026-11-09 12:00 UTC
        var now = new DateTimeOffset(2026, 11, 9, 12, 0, 0, TimeSpan.Zero);

        var result = PlexScheduleCalculator.GetNextRun(PlexConfiguration.DefaultSchedule, now, TimeZoneInfo.Utc);

        Assert.That(result.Warning, Is.Null);
        Assert.That(result.NextRun, Is.EqualTo(new DateTimeOffset(2026, 11, 15, 3, 0, 0, TimeSpan.Zero)));
        Assert.That(result.NextRun!.Value.DayOfWeek, Is.EqualTo(DayOfWeek.Sunday));
    }

    [Test]
    public void GetNextRun_LocalZone_ReturnsLocalWallClockTime()
    {
        var now = new DateTimeOffset(2026, 11, 9, 12, 0, 0, TimeSpan.Zero);

        var result = PlexScheduleCalculator.GetNextRun(PlexConfiguration.DefaultSchedule, now, Plus5);

        // Sunday 03:00 at +05:00 is Saturday 22:00 UTC
        Assert.That(result.NextRun, Is.EqualTo(new DateTimeOffset(2026, 11, 14, 22, 0, 0, TimeSpan.Zero)));
        Assert.That(result.NextRun!.Value.Offset, Is.EqualTo(TimeSpan.FromHours(5)));
        Assert.That(result.NextRun.Value.Hour, Is.EqualTo(3));
    }

    [Test]
    public void GetNextRun_AtExactOccurrence_ReturnsFollowingOccurrence()
    {
        var now = new DateTimeOffset(2026, 11, 15, 3, 0, 0, TimeSpan.Zero);

        var result = PlexScheduleCalculator.GetNextRun(PlexConfiguration.DefaultSchedule, now, TimeZoneInfo.Utc);

        Assert.That(result.NextRun, Is.EqualTo(new DateTimeOffset(2026, 11, 22, 3, 0, 0, TimeSpan.Zero)));
    }

    [TestCase("not a cron")]
    [TestCase("99 99 * * *")]
    [TestCase("")]
    [TestCase("   ")]
    [TestCase(null)]
    public void GetNextRun_InvalidSchedule_ReturnsNoRunAndWarningWithoutThrowing(string? schedule)
    {
        var result = PlexScheduleCalculator.GetNextRun(schedule, DateTimeOffset.UtcNow, TimeZoneInfo.Utc);

        Assert.That(result.NextRun, Is.Null);
        Assert.That(result.Warning, Is.Not.Null.And.Not.Empty);
    }

    [Test]
    public void GetNextRun_NoFutureOccurrence_ReturnsNoRunAndWarning()
    {
        // 31 February never occurs
        var result = PlexScheduleCalculator.GetNextRun("0 3 31 2 *", DateTimeOffset.UtcNow, TimeZoneInfo.Utc);

        Assert.That(result.NextRun, Is.Null);
        Assert.That(result.Warning, Does.Contain("no next occurrence"));
    }
}
