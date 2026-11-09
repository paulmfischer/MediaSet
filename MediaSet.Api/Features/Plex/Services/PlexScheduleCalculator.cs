using Cronos;

namespace MediaSet.Api.Features.Plex.Services;

/// <summary>Outcome of a schedule calculation: the next run, or no run and a warning that says why.</summary>
public sealed record PlexScheduleResult(DateTimeOffset? NextRun, string? Warning);

/// <summary>
/// Pure cron helper for the Plex sync schedule. Schedules are evaluated in the given time zone (the scheduler
/// passes the server local zone). It never throws: an invalid schedule or one with no future occurrence
/// returns no next run and a warning.
/// </summary>
public static class PlexScheduleCalculator
{
    public static PlexScheduleResult GetNextRun(string? schedule, DateTimeOffset now, TimeZoneInfo timeZone)
    {
        if (string.IsNullOrWhiteSpace(schedule))
        {
            return new PlexScheduleResult(null, "Schedule is required");
        }

        try
        {
            var cron = CronExpression.Parse(schedule);
            var next = cron.GetNextOccurrence(now, timeZone);
            return next == null
                ? new PlexScheduleResult(null, $"cron expression '{schedule}' has no next occurrence")
                : new PlexScheduleResult(next, null);
        }
        catch (CronFormatException)
        {
            return new PlexScheduleResult(null, $"'{schedule}' is not a valid cron expression");
        }
        catch (ArgumentException)
        {
            return new PlexScheduleResult(null, $"'{schedule}' is not a valid cron expression");
        }
    }
}
