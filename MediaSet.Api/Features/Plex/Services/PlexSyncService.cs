using MediaSet.Api.Features.Plex.Models;
using MediaSet.Api.Infrastructure.DataAccess;
using MediaSet.Api.Infrastructure.Plex;
using MediaSet.Api.Shared.Models;
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
    ILogger<PlexSyncService> logger) : IPlexSyncService, IHostedService
{
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly SemaphoreSlim _startGate = new(1, 1);
    private volatile bool _stopping;
    private readonly object _gate = new();
    private readonly System.Collections.Concurrent.ConcurrentDictionary<Task, byte> _tasks = new();
    private readonly Dictionary<string, PlexSyncRun> _active = new(StringComparer.OrdinalIgnoreCase);

    public async Task<PlexSyncStartResult> StartAsync(string entityType, string trigger, CancellationToken cancellationToken)
    {
        // Serializes check, lock acquisition and insert, so _active only ever holds persisted runs
        // and a conflict always carries a run with an id
        await _startGate.WaitAsync(cancellationToken);
        PlexSyncRun run;
        bool holdsLock;
        try
        {
            if (_stopping)
            {
                throw new InvalidOperationException("The application is stopping; no new Plex sync can start.");
            }

            lock (_gate)
            {
                if (_active.TryGetValue(entityType, out var current))
                {
                    return new PlexSyncStartResult(current.Clone(), false);
                }
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

            try
            {
                await store.InsertAsync(run, cancellationToken);
            }
            catch
            {
                if (holdsLock)
                {
                    _lock.Release();
                }
                throw;
            }

            lock (_gate)
            {
                _active[entityType] = run;
            }
        }
        finally
        {
            _startGate.Release();
        }

        // The run's token is linked to application shutdown, not to the request that started it
        var cts = CancellationTokenSource.CreateLinkedTokenSource(lifetime.ApplicationStopping);
        var snapshot = Snapshot(run);
        var task = Task.Run(() => ExecuteAsync(run, holdsLock, cts));
        _tasks[task] = 0;
        _ = task.ContinueWith(t => _tasks.TryRemove(t, out _), TaskScheduler.Default);
        return new PlexSyncStartResult(snapshot, true);
    }

    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>Waits for runs to record their outcome (they are cancelled by ApplicationStopping first).</summary>
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        // New starts are refused from here on. Passing through the start gate drains a start that is
        // mid-insert, so every run it created is tracked before the snapshot below.
        _stopping = true;
        try
        {
            await _startGate.WaitAsync(cancellationToken);
            _startGate.Release();
            await Task.WhenAll(_tasks.Keys).WaitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // Shutdown grace period over: the startup sweep closes anything left in progress
        }
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

                var scan = await ScanAsync(run.EntityType, token);
                await Transition(run, r =>
                {
                    r.State = PlexSyncRunStates.Succeeded;
                    r.ItemsScanned = scan.Scanned;
                    r.MatchedCount = scan.Matched;
                    r.UnmatchedCount = scan.Unmatched;
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

    private sealed record ScanResult(int Scanned, int Matched, int Unmatched);

    /// <summary>Scan the Plex libraries, match movies to the items, store the Plex data. Writes nothing to Plex.</summary>
    private async Task<ScanResult> ScanAsync(string entityType, CancellationToken token)
    {
        // The typed client is transient: resolve one per run so its handler is not held for the app's lifetime
        var client = services.GetRequiredService<IPlexClient>();
        var itemType = PlexEntityTypes.LibraryType(entityType);
        var items = new List<PlexItem>();
        foreach (var library in await client.GetLibrariesForEntityTypeAsync(entityType, token))
        {
            var libraryItems = await client.GetItemsAsync(library, null, true, token);
            items.AddRange(libraryItems.Where(i => string.Equals(i.Type, itemType, StringComparison.OrdinalIgnoreCase)));
        }

        var itemStore = services.GetRequiredService<IPlexItemStore>();
        using var scope = services.CreateScope();
        var movies = (await scope.ServiceProvider.GetRequiredService<IEntityService<Movie>>().GetListAsync(token))
            .Where(m => !string.IsNullOrEmpty(m.Id))
            .ToList();

        var byRatingKey = new Dictionary<string, PlexItem>(StringComparer.Ordinal);
        foreach (var item in items)
        {
            byRatingKey.TryAdd(item.RatingKey, item);
        }

        var matched = 0;
        var syncedAt = DateTime.UtcNow;
        foreach (var movie in movies)
        {
            token.ThrowIfCancellationRequested();
            try
            {
                var stored = await itemStore.GetAsync(movie.Id!, token);

                // The stored Plex item id wins; the ExternalId is only used until the movie is linked
                PlexItem? item = null;
                if (stored != null && byRatingKey.TryGetValue(stored.PlexItemId, out var linked))
                {
                    item = linked;
                }
                item ??= items.FirstOrDefault(i => PlexMatching.MatchesExternalId(movie.ExternalId, i.Guids));

                if (item != null)
                {
                    await itemStore.UpsertAsync(PlexMatching.Build(movie.Id!, item, syncedAt), token);
                    matched++;
                }
                else if (stored is { OnPlex: true })
                {
                    // Linked earlier but gone from Plex now: keep the last data, flag it
                    stored.OnPlex = false;
                    stored.LastSyncedAt = syncedAt;
                    await itemStore.UpsertAsync(stored, token);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // One failing movie must not fail the run
                logger.LogWarning(ex, "Plex sync skipped movie {MovieId}", movie.Id);
            }
        }

        // Purge the Plex data of movies deleted since the last run
        try
        {
            var live = movies.Select(m => m.Id!).ToHashSet(StringComparer.Ordinal);
            foreach (var id in await itemStore.ListIdsAsync(token))
            {
                if (!live.Contains(id))
                {
                    await itemStore.DeleteAsync(id, token);
                }
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Plex sync could not purge Plex data of deleted movies");
        }

        return new ScanResult(items.Count, matched, movies.Count - matched);
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
