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
            PlexItemData data;
            if (stored.Scope is { } storedScope)
            {
                // As in a full sync: an automatic link re-reads the scope from the title (keeping the stored one when the
                // title no longer says); a manual link keeps its scope
                var scope = storedScope;
                if (stored.LinkSource != PlexLinkSources.Manual)
                {
                    using var movieScope = services.CreateScope();
                    var movie = await movieScope.ServiceProvider.GetRequiredService<IEntityService<Movie>>().GetAsync(movieId, cancellationToken);
                    var parsed = PlexTvTitleParser.ParseScope(movie?.Title);
                    scope = parsed.IsReadable ? parsed : storedScope;
                }
                // Reads first (seasons, then the episodes of the covered seasons), so an outage fails before the write
                var seasons = await client.GetSeasonsAsync(item.RatingKey, cancellationToken);
                var episodes = await PlexEpisodeLoader.LoadAsync(client, seasons, scope, null, PlexMatching.KnownEpisodes(stored), cancellationToken);
                data = PlexMatching.BuildShow(movieId, item, seasons, scope, DateTime.UtcNow, stored.LinkSource, episodes, stored);
            }
            else
            {
                // PlexUpdatedAt comes from the single-item endpoint here; a mismatch with the listing only costs a full read next sync
                data = PlexMatching.Build(movieId, item, DateTime.UtcNow, stored.LinkSource);
            }
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
                    await TransitionAsync(run, r => r.State = PlexSyncRunStates.Running);
                }

                logger.LogInformation("Plex sync for {EntityType} started ({Trigger}, run {RunId})", run.EntityType, run.Trigger, run.Id);
                var scan = await ScanAsync(run.EntityType, token);
                logger.LogInformation(
                    "Plex sync for {EntityType} finished: {Scanned} entries processed, {Matched} matched, {Unmatched} unmatched, {Errored} errored, {Backfilled} ExternalIds backfilled",
                    run.EntityType, scan.Scanned, scan.Matched, scan.Unmatched, scan.Errored, scan.Backfilled);
                await TransitionAsync(run, r =>
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

    private sealed record MoviePlan(Movie Movie, PlexItemData? Stored, bool Manual, PlexItem? Item, PlexScope? Scope = null, IReadOnlyList<PlexSeason>? Seasons = null, IReadOnlyDictionary<int, IReadOnlyList<PlexEpisode>>? Episodes = null, PlexItemData? Stale = null);

    /// <summary>Consecutive runs an auto-linked item may be missing before its link is dropped.</summary>
    private const int AutoLinkDropMisses = PlexMatching.AutoLinkDropMisses;

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
        var itemStore = services.GetRequiredService<IPlexItemStore>();
        // The listing makes no per-item detail request. Movies unchanged since the last run are flagged so they keep their
        // stored streams; the others are read in full only if an entry links to them (after matching).
        var knownMovies = PlexMatching.KnownMovies(await itemStore.ListMoviesAsync(token));
        foreach (var library in await client.GetLibrariesForEntityTypeAsync(entityType, token))
        {
            var libraryItems = await client.GetItemListingAsync(library, token, knownMovies);
            items.AddRange(libraryItems.Where(i => itemTypes.Contains(i.Type, StringComparer.OrdinalIgnoreCase)));
        }

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

        // Episodes are read per season, once per run however many entries cover it, and only for linked shows
        var episodesBySeason = new Dictionary<string, IReadOnlyList<PlexEpisode>>(StringComparer.Ordinal);

        // Items the listing came from, to tell them apart from an item fetched by id (already read in full)
        var listedItems = new HashSet<PlexItem>(items, ReferenceEqualityComparer.Instance);

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

                // A TV series entry links to a Plex show, anything else to a movie. A stored link of the other kind (after IsTvSeries was toggled) is stale:
                // an automatic one is replaced by a match or dropped, a manual one is flagged broken and never overwritten.
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
                IReadOnlyDictionary<int, IReadOnlyList<PlexEpisode>>? episodes = null;
                if (isTv)
                {
                    // A manual link keeps its scope. An automatic one reads it from the title, and keeps its stored scope
                    // when the title no longer says. No readable scope means no match (and no guessing).
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
                        else
                        {
                            var known = PlexMatching.KnownEpisodes(storedUsable ? stored : null);
                            // Linked: read the episodes of the covered seasons now, so an outage fails the run before any write
                            episodes = await PlexEpisodeLoader.LoadAsync(client, seasons, entryScope, episodesBySeason, known, token);
                        }
                    }
                }

                plans.Add(new MoviePlan(movie, storedUsable ? stored : null, manual && storedUsable, item, entryScope, seasons, episodes, stored != null && !storedUsable ? stored : null));
            }
            catch (Exception ex) when (ex is not OperationCanceledException and not PlexClientException)
            {
                // One failing movie must not fail the run. Log the id and exception type only, never the
                // exception itself, so a token in a message can never reach the log.
                errored++;
                logger.LogWarning("Plex sync could not process movie {MovieId} ({ExceptionType})", movie.Id, ex.GetType().Name);
            }
        }

        // Still phase 1 (reads only): the listing has no streams, so read the detail of the linked movies now, and only those
        plans = await ReadLinkedDetailsAsync(client, plans, listedItems, token);

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
                if (plan.Item == null && logger.IsEnabled(LogLevel.Debug))
                {
                    var reason = movie.IsTvSeries && plan.Scope == null ? "no season scope can be read from the title"
                        : !plan.Manual && string.IsNullOrWhiteSpace(movie.ExternalId) ? "it has no ExternalId"
                        : movie.IsTvSeries ? "no Plex show with that id has the entry's seasons"
                        : "no Plex movie in the scanned libraries has that id";
                    logger.LogDebug("Plex sync found no match for {MovieId} ({Title}): {Reason}", movie.Id, movie.Title, reason);
                }

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
                        await itemStore.UpsertAsync(PlexMatching.BuildShow(
                            movie.Id!, item, plan.Seasons ?? [], plan.Scope, syncedAt, source, plan.Episodes ?? new Dictionary<int, IReadOnlyList<PlexEpisode>>(), stored, countMisses: true), token);
                    }
                    else
                    {
                        if (!linkedBy.TryAdd(item.RatingKey, movie.Id!))
                        {
                            // Duplicates are allowed; the owner may want to know
                            logger.LogWarning("Plex item {RatingKey} is linked by movies {FirstMovieId} and {MovieId}", item.RatingKey, linkedBy[item.RatingKey], movie.Id);
                        }

                        // Build resets MissCount and Broken: a found item recovers the link
                        await itemStore.UpsertAsync(PlexMatching.Build(movie.Id!, item, syncedAt, source, stored), token);
                    }
                    matched++;
                }
                else if (plan.Stale is { } stale)
                {
                    // A link of the other kind than the entry has no match to replace it
                    if (stale.LinkSource == PlexLinkSources.Manual)
                    {
                        // Never dropped or overwritten: flag it for the user
                        await FlagManualBrokenAsync(itemStore, stale, stale.MissCount + 1, syncedAt, token);
                    }
                    else
                    {
                        // Dropped at once, not after AutoLinkDropMisses: the data describes the wrong kind of item,
                        // so keeping it as "last known" would show it for the wrong entry
                        await itemStore.DeleteAsync(movie.Id!, token);
                    }
                }
                else if (stored != null)
                {
                    // Linked earlier but gone from Plex while the library is reachable
                    var misses = stored.MissCount + 1;
                    if (plan.Manual)
                    {
                        // A manual link is never dropped; flag it for the user
                        await FlagManualBrokenAsync(itemStore, stored, misses, syncedAt, token);
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

        return new ScanResult(movies.Count, matched, movies.Count - matched - errored, errored, backfill.Filled, backfill.Skipped);
    }

    /// <summary>
    /// Reads, with at most DetailConcurrency requests at once, the streams of the Plex movies the plans link to, one read per
    /// rating key however many movies link to it. A listed item needs it unless it was flagged unchanged and the movie's own
    /// stored data can supply its streams. A failed read keeps the listing data, flags the streams unavailable and is retried
    /// next sync. Items with no files and items already read in full need none.
    /// </summary>
    private async Task<List<MoviePlan>> ReadLinkedDetailsAsync(IPlexClient client, List<MoviePlan> plans, HashSet<PlexItem> listedItems, CancellationToken token)
    {
        var needed = new Dictionary<string, PlexItem>(StringComparer.Ordinal);
        foreach (var plan in plans)
        {
            if (NeedsDetail(plan, listedItems))
            {
                needed.TryAdd(plan.Item!.RatingKey, plan.Item);
            }
        }
        if (needed.Count == 0)
        {
            return plans;
        }

        var full = new System.Collections.Concurrent.ConcurrentDictionary<string, PlexItem>(StringComparer.Ordinal);
        var parallel = new ParallelOptions { MaxDegreeOfParallelism = Math.Max(1, options.Value.DetailConcurrency), CancellationToken = token };
        await Parallel.ForEachAsync(needed.Values, parallel, async (listed, ct) =>
        {
            try
            {
                var read = await client.GetItemAsync(listed.RatingKey, true, ct);
                full[listed.RatingKey] = read != null && read.Versions.Count > 0
                    ? listed with { Versions = read.Versions, StreamsReused = false, StreamsAvailable = true }
                    : listed with { StreamsReused = false, StreamsAvailable = false };
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Like a failed per-item lookup: keep the listing data and read it again next sync
                full[listed.RatingKey] = listed with { StreamsReused = false, StreamsAvailable = false };
            }
        });

        return [.. plans.Select(p => NeedsDetail(p, listedItems) ? p with { Item = full[p.Item!.RatingKey] } : p)];
    }

    private static bool NeedsDetail(MoviePlan plan, HashSet<PlexItem> listedItems) =>
        plan.Scope == null
        && plan.Item is { Versions.Count: > 0 } item
        && listedItems.Contains(item)
        && (!item.StreamsReused || !PlexMatching.CanReuseStreams(item, plan.Stored));

    /// <summary>Keeps a manual link's last data and flags it off Plex and broken.</summary>
    private static Task FlagManualBrokenAsync(IPlexItemStore itemStore, PlexItemData link, int misses, DateTime syncedAt, CancellationToken token)
    {
        link.OnPlex = false;
        link.Broken = true;
        link.MissCount = misses;
        link.LastSyncedAt = syncedAt;
        return itemStore.UpsertAsync(link, token);
    }

    private Task Finish(PlexSyncRun run, string state, string message) =>
        TransitionAsync(run, r =>
        {
            r.State = state;
            r.Message = message;
            r.EndedAt = DateTime.UtcNow;
        });

    private async Task TransitionAsync(PlexSyncRun run, Action<PlexSyncRun> change)
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
