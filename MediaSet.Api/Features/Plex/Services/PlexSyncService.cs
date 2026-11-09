using MediaSet.Api.Features.Plex.Models;
using MediaSet.Api.Infrastructure.DataAccess;
using MediaSet.Api.Infrastructure.Plex;
using MediaSet.Api.Shared.Models;
using Microsoft.Extensions.Options;

namespace MediaSet.Api.Features.Plex.Services;

public record PlexSyncStartResult(PlexSyncRun Run, bool Started);

public enum PlexRefreshStatus { Refreshed, Busy, NotLinked, PlexItemMissing }

/// <summary>Outcome of a single-item refresh. Run is the sync in progress when Busy (null when only another refresh holds the lock).</summary>
public record PlexRefreshResult(PlexRefreshStatus Status, PlexItemData? Data = null, PlexSyncRun? Run = null);

public interface IPlexSyncService
{
    /// <summary>
    /// Re-reads one linked movie from its own Plex item, under the same lock as full syncs. Does not queue:
    /// when a sync or another refresh holds the lock it returns Busy at once. Runs inline and records no run.
    /// </summary>
    Task<PlexRefreshResult> RefreshItemAsync(string entityType, string movieId, CancellationToken cancellationToken);

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

            // The run's token is linked to application shutdown, not to the request that started it.
            // The task is tracked before the start gate is released, so StopAsync, which passes through
            // the gate, always sees every run a start created.
            var cts = CancellationTokenSource.CreateLinkedTokenSource(lifetime.ApplicationStopping);
            var snapshot = Snapshot(run);
            var task = Task.Run(() => ExecuteAsync(run, holdsLock, cts));
            _tasks[task] = 0;
            _ = task.ContinueWith(t => _tasks.TryRemove(t, out _), TaskScheduler.Default);
            return new PlexSyncStartResult(snapshot, true);
        }
        finally
        {
            _startGate.Release();
        }
    }

    public async Task<PlexRefreshResult> RefreshItemAsync(string entityType, string movieId, CancellationToken cancellationToken)
    {
        // Never queue: a run holding the lock (or another refresh) makes this a conflict at once
        if (!_lock.Wait(0))
        {
            return new PlexRefreshResult(PlexRefreshStatus.Busy, null, ActiveRun(entityType));
        }

        try
        {
            var itemStore = services.GetRequiredService<IPlexItemStore>();
            var stored = await itemStore.GetAsync(movieId, cancellationToken);
            if (stored == null)
            {
                return new PlexRefreshResult(PlexRefreshStatus.NotLinked);
            }

            var client = services.GetRequiredService<IPlexClient>();
            // A stored scope marks a TV entry, linked to a show
            var itemType = PlexEntityTypes.ItemType(stored.Scope != null);
            var item = await FetchLinkedItemAsync(client, itemType, stored.PlexItemId, cancellationToken);
            if (item == null)
            {
                // Keep the last data, flag it, as a full sync does
                if (stored.OnPlex)
                {
                    stored.OnPlex = false;
                    stored.LastSyncedAt = DateTime.UtcNow;
                    await itemStore.UpsertAsync(stored, cancellationToken);
                }
                return new PlexRefreshResult(PlexRefreshStatus.PlexItemMissing, stored);
            }

            // A refresh keeps the link as it was made (auto stays auto, manual stays manual)
            var data = stored.Scope is { } scope
                ? PlexMatching.BuildShow(movieId, item, await client.GetSeasonsAsync(item.RatingKey, cancellationToken), scope, DateTime.UtcNow, stored.LinkSource)
                : PlexMatching.Build(movieId, item, DateTime.UtcNow, stored.LinkSource);
            await itemStore.UpsertAsync(data, cancellationToken);
            return new PlexRefreshResult(PlexRefreshStatus.Refreshed, data);
        }
        finally
        {
            _lock.Release();
        }
    }

    private PlexSyncRun? ActiveRun(string entityType)
    {
        lock (_gate)
        {
            return (_active.TryGetValue(entityType, out var current) ? current : _active.Values.FirstOrDefault())?.Clone();
        }
    }

    /// <summary>The Plex item with the rating key when it exists and is of the wanted Plex type (movie or show).</summary>
    private static async Task<PlexItem?> FetchLinkedItemAsync(IPlexClient client, string itemType, string ratingKey, CancellationToken token)
    {
        var item = await client.GetItemAsync(ratingKey, true, token);
        return item != null && string.Equals(item.Type, itemType, StringComparison.OrdinalIgnoreCase) ? item : null;
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
                    r.ErroredCount = scan.Errored;
                    r.ExternalIdsBackfilled = scan.Backfilled;
                    r.BackfillSkipped = scan.BackfillSkipped;
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

    private sealed record ScanResult(int Scanned, int Matched, int Unmatched, int Errored, int Backfilled, int BackfillSkipped);

    private sealed record MoviePlan(Movie Movie, PlexItemData? Stored, bool Manual, PlexItem? Item, PlexScope? Scope = null, IReadOnlyList<PlexSeason>? Seasons = null);

    /// <summary>Consecutive runs an auto-linked item may be missing before its link is dropped.</summary>
    private const int AutoLinkDropMisses = 2;

    /// <summary>Scan the Plex libraries, match movies to the items, store the Plex data. Writes nothing to Plex.</summary>
    private async Task<ScanResult> ScanAsync(string entityType, CancellationToken token)
    {
        // First step: fill missing ExternalIds from TMDB so the movies can match in this same run. It never
        // fails the run (only shutdown cancels it).
        var backfill = new ExternalIdBackfillResult(0, 0);
        try
        {
            using var backfillScope = services.CreateScope();
            backfill = await backfillScope.ServiceProvider.GetRequiredService<IExternalIdBackfillService>().RunAsync(token);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning("Plex sync ExternalId backfill failed ({ExceptionType}); continuing the sync", ex.GetType().Name);
        }

        // The typed client is transient: resolve one per run so its handler is not held for the app's lifetime
        var client = services.GetRequiredService<IPlexClient>();
        var itemTypes = PlexEntityTypes.LibraryTypes(entityType);
        var items = new List<PlexItem>();
        foreach (var library in await client.GetLibrariesForEntityTypeAsync(entityType, token))
        {
            var libraryItems = await client.GetItemsAsync(library, null, true, token);
            items.AddRange(libraryItems.Where(i => itemTypes.Contains(i.Type, StringComparer.OrdinalIgnoreCase)));
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

        // GUID lookup per Plex type (a movie and a show may share an id number), first scanned item wins for a shared GUID
        var byGuid = new Dictionary<(string Type, string Source, string Value), PlexItem>();
        foreach (var item in items)
        {
            foreach (var (source, value) in PlexMatching.GuidKeys(item.Guids))
            {
                byGuid.TryAdd((item.Type.ToLowerInvariant(), source, value), item);
            }
        }

        // Seasons of a show are read once per run, however many entries link to it
        var seasonsByShow = new Dictionary<string, IReadOnlyList<PlexSeason>>(StringComparer.Ordinal);
        async Task<IReadOnlyList<PlexSeason>> SeasonsOfAsync(string showKey)
        {
            if (!seasonsByShow.TryGetValue(showKey, out var seasons))
            {
                seasons = await client.GetSeasonsAsync(showKey, token);
                seasonsByShow[showKey] = seasons;
            }
            return seasons;
        }

        // Phase 1 reads only: decide each movie's Plex item. A PlexClientException (outage, rejected token,
        // timeout) is rethrown so the run fails before anything is written, and nothing is marked gone.
        var plans = new List<MoviePlan>();
        var errored = 0;
        foreach (var movie in movies)
        {
            token.ThrowIfCancellationRequested();
            try
            {
                var stored = await itemStore.GetAsync(movie.Id!, token);
                var manual = stored is { LinkSource: PlexLinkSources.Manual };

                // A TV series entry links to a Plex show, anything else to a movie. A stored link of the other kind is ignored.
                var isTv = movie.IsTvSeries;
                var wantedType = PlexEntityTypes.ItemType(isTv);
                var storedIsTv = stored?.Scope != null;
                var storedUsable = stored != null && storedIsTv == isTv;

                // The stored Plex item id wins; the ExternalId is only used until the entry is linked
                PlexItem? item = null;
                if (storedUsable && byRatingKey.TryGetValue(stored!.PlexItemId, out var linked)
                    && string.Equals(linked.Type, wantedType, StringComparison.OrdinalIgnoreCase))
                {
                    item = linked;
                }
                if (item == null && manual && storedUsable)
                {
                    // A manual link may point outside the scanned libraries: ask Plex for that item itself
                    item = await FetchLinkedItemAsync(client, wantedType, stored!.PlexItemId, token);
                }
                if (item == null && !manual && PlexMatching.ExternalIdKey(movie.ExternalId) is { } externalKey
                    && byGuid.TryGetValue((wantedType, externalKey.Source, externalKey.Value), out var byId))
                {
                    item = byId;
                }

                PlexScope? entryScope = null;
                IReadOnlyList<PlexSeason>? seasons = null;
                if (isTv)
                {
                    // A manual link keeps its entryScope. An automatic one reads it from the title, and keeps its stored entryScope
                    // when the title no longer says. No readable entryScope means no match (and no guessing).
                    var parsed = PlexTvTitleParser.ParseScope(movie.Title);
                    entryScope = manual && storedUsable ? stored!.Scope : parsed.IsReadable ? parsed : storedUsable ? stored!.Scope : null;
                    if (entryScope == null || item == null)
                    {
                        item = null;
                    }
                    else
                    {
                        seasons = await SeasonsOfAsync(item.RatingKey);
                        // Automatic only: a show that has none of the entry's seasons is not a match
                        if (!manual && !PlexMatching.ScopeAvailable(seasons, entryScope))
                        {
                            item = null;
                        }
                    }
                }

                plans.Add(new MoviePlan(movie, storedUsable ? stored : null, manual && storedUsable, item, entryScope, seasons));
            }
            catch (Exception ex) when (ex is not OperationCanceledException and not PlexClientException)
            {
                // One failing movie must not fail the run. Log the id and exception type only, never the
                // exception itself, so a token in a message can never reach the log.
                errored++;
                logger.LogWarning("Plex sync could not process movie {MovieId} ({ExceptionType})", movie.Id, ex.GetType().Name);
            }
        }

        // Phase 2 writes
        var matched = 0;
        var syncedAt = DateTime.UtcNow;
        var linkedBy = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var plan in plans)
        {
            token.ThrowIfCancellationRequested();
            var movie = plan.Movie;
            var stored = plan.Stored;
            try
            {
                if (plan.Item is { } item)
                {
                    // A manual link may have been made since the read above; it must never be replaced
                    if (!plan.Manual && await itemStore.GetAsync(movie.Id!, token) is { LinkSource: PlexLinkSources.Manual })
                    {
                        matched++;
                        continue;
                    }

                    var source = plan.Manual ? PlexLinkSources.Manual : PlexLinkSources.Auto;
                    if (plan.Scope != null)
                    {
                        // Several TV entries may link to one show, each with its own scope: expected, not logged.
                        // Build resets MissCount and Broken: a found show recovers the link.
                        await itemStore.UpsertAsync(PlexMatching.BuildShow(movie.Id!, item, plan.Seasons ?? [], plan.Scope, syncedAt, source), token);
                    }
                    else
                    {
                        if (!linkedBy.TryAdd(item.RatingKey, movie.Id!))
                        {
                            // Duplicates are allowed; the owner may want to know
                            logger.LogWarning("Plex item {RatingKey} is linked by movies {FirstMovieId} and {MovieId}", item.RatingKey, linkedBy[item.RatingKey], movie.Id);
                        }

                        // Build resets MissCount and Broken: a found item recovers the link
                        await itemStore.UpsertAsync(PlexMatching.Build(movie.Id!, item, syncedAt, source), token);
                    }
                    matched++;
                }
                else if (stored != null)
                {
                    // Linked earlier but gone from Plex while the library is reachable
                    var misses = stored.MissCount + 1;
                    if (plan.Manual)
                    {
                        // A manual link is never dropped; flag it for the user
                        stored.OnPlex = false;
                        stored.Broken = true;
                        stored.MissCount = misses;
                        stored.LastSyncedAt = syncedAt;
                        await itemStore.UpsertAsync(stored, token);
                    }
                    else if (misses >= AutoLinkDropMisses)
                    {
                        await itemStore.DeleteAsync(movie.Id!, token);
                    }
                    else
                    {
                        // Keep the last data, flag it
                        stored.OnPlex = false;
                        stored.MissCount = misses;
                        stored.LastSyncedAt = syncedAt;
                        await itemStore.UpsertAsync(stored, token);
                    }
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                errored++;
                logger.LogWarning("Plex sync could not process movie {MovieId} ({ExceptionType})", movie.Id, ex.GetType().Name);
            }
        }

        // Purge the Plex data of movies deleted since the last run. Only a missing movie qualifies, so a
        // manual link is never purged for being unmatched or gone from Plex.
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
            logger.LogWarning("Plex sync could not purge Plex data of deleted movies ({ExceptionType})", ex.GetType().Name);
        }

        return new ScanResult(items.Count, matched, movies.Count - matched - errored, errored, backfill.Filled, backfill.Skipped);
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
