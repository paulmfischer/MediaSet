using MediaSet.Api.Features.Plex.Models;
using MediaSet.Api.Infrastructure.Plex;
using Microsoft.Extensions.Options;

namespace MediaSet.Api.Features.Plex.Services;

public record PlexSyncStartResult(PlexSyncRun Run, bool Started);

public interface IPlexSyncService
{
    /// <summary>
    /// Creates a run record and starts the run in the background. When a run for the entity type is already
    /// in progress nothing is started and that run is returned with Started false.
    /// </summary>
    Task<PlexSyncStartResult> StartAsync(string entityType, string trigger, CancellationToken cancellationToken);
}

/// <summary>
/// Runs Plex syncs one at a time. A single lock is shared by every entity type and trigger, so a manual
/// run and any future scheduled run never overlap. The service is a singleton and the store is too, so
/// the background task needs no scope.
/// </summary>
public sealed class PlexSyncService(
    IServiceProvider services,
    IPlexSyncRunStore store,
    IHostApplicationLifetime lifetime,
    IOptions<PlexConfiguration> options,
    ILogger<PlexSyncService> logger) : IPlexSyncService
{
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly object _gate = new();
    private readonly Dictionary<string, PlexSyncRun> _active = new(StringComparer.OrdinalIgnoreCase);

    public async Task<PlexSyncStartResult> StartAsync(string entityType, string trigger, CancellationToken cancellationToken)
    {
        PlexSyncRun run;
        bool holdsLock;
        lock (_gate)
        {
            if (_active.TryGetValue(entityType, out var current))
            {
                return new PlexSyncStartResult(current.Clone(), false);
            }

            // Take the shared lock now when it is free, otherwise the run waits for it as queued
            holdsLock = _lock.Wait(0);
            run = new PlexSyncRun
            {
                EntityType = entityType,
                Trigger = trigger,
                State = holdsLock ? PlexSyncRunStates.Running : PlexSyncRunStates.Queued,
                StartedAt = DateTime.UtcNow
            };
            _active[entityType] = run;
        }

        try
        {
            await store.InsertAsync(run, cancellationToken);
        }
        catch
        {
            lock (_gate)
            {
                _active.Remove(entityType);
            }
            if (holdsLock)
            {
                _lock.Release();
            }
            throw;
        }

        // The run's token is linked to application shutdown, not to the request that started it
        var cts = CancellationTokenSource.CreateLinkedTokenSource(lifetime.ApplicationStopping);
        var snapshot = Snapshot(run);
        _ = Task.Run(() => ExecuteAsync(run, holdsLock, cts));
        return new PlexSyncStartResult(snapshot, true);
    }

    private PlexSyncRun Snapshot(PlexSyncRun run)
    {
        lock (_gate)
        {
            return run.Clone();
        }
    }

    private async Task ExecuteAsync(PlexSyncRun run, bool holdsLock, CancellationTokenSource cts)
    {
        var token = cts.Token;
        try
        {
            try
            {
                if (!holdsLock)
                {
                    await _lock.WaitAsync(token);
                    holdsLock = true;
                    await Transition(run, r => r.State = PlexSyncRunStates.Running);
                }

                var scanned = await ScanAsync(run.EntityType, token);
                await Transition(run, r =>
                {
                    r.State = PlexSyncRunStates.Succeeded;
                    r.ItemsScanned = scanned;
                    r.EndedAt = DateTime.UtcNow;
                });
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                await Finish(run, PlexSyncRunStates.Interrupted, "The application stopped while the run was in progress.");
            }
            catch (PlexClientException ex)
            {
                // Client messages are fixed text and a status code: no URL, no token
                await Finish(run, PlexSyncRunStates.Failed, Sanitize(ex.Message));
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Plex sync for {EntityType} failed unexpectedly", run.EntityType);
                await Finish(run, PlexSyncRunStates.Failed, "The Plex sync failed unexpectedly.");
            }
        }
        catch (Exception ex)
        {
            // Persisting the outcome failed. Log without losing the process.
            logger.LogError(ex, "Could not record the outcome of the Plex sync for {EntityType}", run.EntityType);
        }
        finally
        {
            lock (_gate)
            {
                _active.Remove(run.EntityType);
            }
            if (holdsLock)
            {
                _lock.Release();
            }
            cts.Dispose();
        }
    }

    private async Task<int> ScanAsync(string entityType, CancellationToken token)
    {
        // The typed client is transient: resolve one per run so its handler is not held for the app's lifetime
        var client = services.GetRequiredService<IPlexClient>();
        var itemType = PlexEntityTypes.LibraryType(entityType);
        var scanned = 0;
        foreach (var library in await client.GetLibrariesForEntityTypeAsync(entityType, token))
        {
            var items = await client.GetItemsAsync(library, null, false, token);
            scanned += items.Count(i => string.Equals(i.Type, itemType, StringComparison.OrdinalIgnoreCase));
        }

        return scanned;
    }

    private Task Finish(PlexSyncRun run, string state, string message) =>
        Transition(run, r =>
        {
            r.State = state;
            r.Message = message;
            r.EndedAt = DateTime.UtcNow;
        });

    private async Task Transition(PlexSyncRun run, Action<PlexSyncRun> change)
    {
        PlexSyncRun copy;
        lock (_gate)
        {
            change(run);
            copy = run.Clone();
        }

        // The outcome must be stored even when the run's token is cancelled
        await store.UpdateAsync(copy, CancellationToken.None);
    }

    private string Sanitize(string message)
    {
        var token = options.Value.Token;
        return string.IsNullOrEmpty(token) ? message : message.Replace(token, "[redacted]", StringComparison.Ordinal);
    }
}
