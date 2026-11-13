using MediaSet.Api.Features.Plex.Models;
using MediaSet.Api.Infrastructure.Plex;
using MediaSet.Api.Shared.Extensions;
using Microsoft.Extensions.Options;

namespace MediaSet.Api.Features.Plex.Services;

/// <summary>
/// Starts a Plex sync for each enabled entity type on its own cron schedule
/// (<c>PlexConfiguration:Entities:{type}:Schedule</c>, default Sunday 03:00), evaluated in the server local time
/// zone. Scheduled runs go through <see cref="IPlexSyncService.StartAsync"/>, so they share the sync lock and the run
/// records with manual runs: when the lock is held the run is queued behind it (never parallel), and when the same
/// entity type already has a queued or running run the scheduled run is skipped.
/// An invalid schedule disables only that entity type.
/// </summary>
public sealed class PlexSyncScheduler(
    IPlexSyncService sync,
    PlexEnablement enablement,
    IOptions<PlexConfiguration> options,
    IPlexScheduleInfo scheduleInfo,
    ILogger<PlexSyncScheduler> logger,
    TimeProvider? timeProvider = null) : BackgroundService
{
    // Task.Delay cannot wait longer than about 49 days, so long waits are split
    private static readonly TimeSpan _maxWait = TimeSpan.FromDays(1);
    private readonly TimeProvider _time = timeProvider ?? TimeProvider.System;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // The schedule's own events are processor events too, like the scheduled runs' (see PlexSyncService.RunScope)
        using var processorScope = logger.BeginScope(new Dictionary<string, object?>
        {
            [LogScopes.ApplicationProperty] = LogScopes.PlexSyncProcessorApplication,
            [LogScopes.ComponentProperty] = LogComponents.Plex,
            ["Trigger"] = PlexSyncTriggers.Scheduled
        });
        var loops = new List<Task>();
        // Only types the startup validation enabled are scheduled, each from its own configuration
        foreach (var (key, entity) in options.Value.Entities)
        {
            if (!enablement.IsEnabled(key))
            {
                continue;
            }

            var entityType = PlexEntityTypes.Normalize(key) ?? key.Trim().ToLowerInvariant();
            if (!entity.Enabled)
            {
                logger.LogWarning("Plex scheduler: entity type '{EntityType}' is not enabled in its configuration. Nothing is scheduled for it.", entityType);
                continue;
            }

            var first = PlexScheduleCalculator.GetNextRun(entity.Schedule, _time.GetUtcNow(), _time.LocalTimeZone);
            if (first.NextRun == null)
            {
                logger.LogWarning(
                    "Plex scheduler: entity type '{EntityType}' has an invalid schedule: {Warning}. It is not scheduled; other entity types are unaffected.",
                    entityType, first.Warning);
                continue;
            }

            // The first next run is published before this method yields, so status is correct right after startup
            scheduleInfo.SetNextRun(entityType, first.NextRun);
            loops.Add(RunLoopAsync(entityType, entity.Schedule, first.NextRun.Value, stoppingToken));
        }

        await Task.WhenAll(loops);
    }

    private async Task RunLoopAsync(string entityType, string schedule, DateTimeOffset next, CancellationToken stoppingToken)
    {
        logger.LogInformation(
            "Plex sync for '{EntityType}' scheduled for {NextRun} ({TimeZone})", entityType, next, _time.LocalTimeZone.Id);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                while (_time.GetUtcNow() < next)
                {
                    var remaining = next - _time.GetUtcNow();
                    await Task.Delay(remaining > _maxWait ? _maxWait : remaining, _time, stoppingToken);
                }

                await StartScheduledAsync(entityType, stoppingToken);

                var result = PlexScheduleCalculator.GetNextRun(schedule, _time.GetUtcNow(), _time.LocalTimeZone);
                if (result.NextRun == null)
                {
                    scheduleInfo.SetNextRun(entityType, null);
                    logger.LogWarning("Plex scheduler: entity type '{EntityType}' has no further run: {Warning}", entityType, result.Warning);
                    return;
                }

                next = result.NextRun.Value;
                scheduleInfo.SetNextRun(entityType, next);
                logger.LogInformation("Next Plex sync for '{EntityType}' scheduled for {NextRun}", entityType, next);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }

        scheduleInfo.SetNextRun(entityType, null);
    }

    private async Task StartScheduledAsync(string entityType, CancellationToken stoppingToken)
    {
        try
        {
            var result = await sync.StartAsync(entityType, PlexSyncTriggers.Scheduled, stoppingToken);
            if (!result.Started)
            {
                logger.LogInformation(
                    "Scheduled Plex sync for '{EntityType}' skipped: a run is already {State} (run {RunId})",
                    entityType, result.Run.State, result.Run.Id);
            }
            else if (result.Run.State == PlexSyncRunStates.Queued)
            {
                logger.LogInformation(
                    "Scheduled Plex sync for '{EntityType}' queued behind the running sync (run {RunId})", entityType, result.Run.Id);
            }
            else
            {
                logger.LogInformation("Scheduled Plex sync for '{EntityType}' started (run {RunId})", entityType, result.Run.Id);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            throw;
        }
        catch (InvalidOperationException) when (stoppingToken.IsCancellationRequested)
        {
            // The sync service refuses new runs while the application stops; expected at shutdown
            logger.LogDebug("Scheduled Plex sync for '{EntityType}' not started: the application is stopping", entityType);
        }
        catch (Exception ex)
        {
            // A failed start must not stop the schedule; the next occurrence tries again
            logger.LogError(ex, "Scheduled Plex sync for '{EntityType}' could not start", entityType);
        }
    }
}
