namespace MediaSet.Api.Features.Plex.Models;

/// <summary>Values of <see cref="PlexEntityStatusDto.SchedulePolicy"/>.</summary>
public static class PlexSchedulePolicies
{
    /// <summary>
    /// A scheduled run that finds the shared sync lock held is queued and starts when the lock frees. It never
    /// runs in parallel. If a run for the same entity type is already queued or running, the scheduled run is skipped.
    /// </summary>
    public const string Queue = "queue";
}

/// <summary>
/// Sync status of one entity type. NextRun is the next scheduled run (server local time zone offset), null when
/// nothing is scheduled. SchedulePolicy describes what a scheduled run does when the shared lock is held.
/// </summary>
public record PlexEntityStatusDto(
    string EntityType,
    bool Enabled,
    PlexSyncRunDto? LastRun,
    DateTimeOffset? NextRun,
    string SchedulePolicy = PlexSchedulePolicies.Queue);
