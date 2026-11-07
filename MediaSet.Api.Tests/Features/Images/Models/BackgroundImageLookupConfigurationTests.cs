using System;
using MediaSet.Api.Features.Images.Models;
using NUnit.Framework;

namespace MediaSet.Api.Tests.Features.Images.Models;

[TestFixture]
public class BackgroundImageLookupConfigurationTests
{
    private static DateTimeOffset Utc(int y, int m, int d, int h = 0, int min = 0) =>
        new(y, m, d, h, min, 0, TimeSpan.Zero);

    private sealed class FakeTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

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
}
