# MediaSet architecture

## Project Structure

- `MediaSet.Api/` — .NET backend API
  - `Features/` — Config, Entities, Health, Images, Lookup, Metadata, and Statistics endpoints/services
  - `Infrastructure/` — Caching, DataAccess, Database, Lookup clients/strategies, Serialization, and Storage
  - `Program.cs` — Dependency injection and application startup
- `MediaSet.Api.Analyzers/` — Custom backend analyzers
- `MediaSet.Api.Tests/` — Backend unit tests (NUnit)
- `MediaSet.Remix/` — Remix 3 frontend UI (`app/routes.ts`, `app/actions/`, `app/ui/`, `app/api/`, `server.ts`)
- `Development/` — Development documentation
- `Setup/` — Production setup and integration guides
- `docker-compose.dev.yml` — MongoDB-only local development service

## Backend (.NET 10 Minimal API)

- Minimal API endpoints are organized with `MapGroup()`: `/Books`, `/Movies`, `/Games`, `/Musics`, plus
  `/health`, `/stats`, `/metadata`, `/lookup`, `/config` and `/images`. Dependency injection is in `Program.cs`.
- Key services: `IEntityService<TEntity>` (CRUD for all entity types), `IMetadataService` (lookup
  orchestration), `IImageService` (upload, download, storage), `ICacheService` (MemoryCache) and
  `IImageLookupService` / `BackgroundImageLookupService` (background image retrieval).
- Entities implement `IEntity` and are stored in MongoDB with `[BsonId]`; custom JSON converters handle
  specific types (runtime, boolean).

**Lookup strategy pattern:** `ILookupStrategy<T>` has a strategy per media type (`BookLookupStrategy`,
`MovieLookupStrategy`, `GameLookupStrategy`, `MusicLookupStrategy`), chosen at runtime by
`LookupStrategyFactory`. Movies and games look up in two stages (UPCitemdb, then TMDB/IGDB); books use
OpenLibrary and music uses MusicBrainz directly.

## Frontend (Remix 3)

Only consult the Remix guides when doing frontend work that touches framework APIs; backend, docs and
tooling changes do not need them. The guides match the installed version and are in
`MediaSet.Remix/node_modules/remix/guides/` (start with `01-start-here.md`).

- **Request flow:** `app/routes.ts` declares routes, with a nested route map per page area. A controller in
  `app/actions/<area>/controller.tsx` handles the area's routes (registered in `app/router.ts`), fetches
  data and renders a page component from `app/ui/<area>/` inside the shared `Document` layout with
  `context.render()`. Pages render to HTML on the server.
- **Browser code:** interactive pieces are client entries under `public/` folders. Only `app/**/public/**` and
  a few shared files are served to the browser (see `app/assets.ts`).
- **Data access:** server-side functions in `app/api/` call the API from actions, using `apiUrl`. Browser API and
  image requests use `clientApiUrl` (`/dev-api` through the dev server proxy in development).
- **Mutations** are plain HTML forms handled by POST actions that redirect on success. The Add Book form
  (`app/actions/entity/add/`) is the template for the other forms; the form rules are in
  `.github/instructions/frontend.instructions.md`.
- **State:** server state is fetched per request in actions, client state is local to client-entry components,
  and there is no global state library.

## External API integrations

Integrations are optional. Use .NET User Secrets for local API credentials and environment variables for
production containers. The UI adapts using `/config/integrations` and `/config/lookup-capabilities`. Setup
guides are in `Setup/`.

| Provider | Used for | Config prefix | Client |
| --- | --- | --- | --- |
| OpenLibrary | Book metadata (ISBN) | `OpenLibraryConfiguration__` (`ContactEmail`, `BaseUrl`) | `IOpenLibraryClient` |
| TMDB | Movie metadata | `TmdbConfiguration__` (`BearerToken`) | `ITmdbClient` |
| IGDB | Game metadata | `IgdbConfiguration__` (`ClientId`, `ClientSecret`) | `IIgdbClient` |
| MusicBrainz | Music album metadata | `MusicBrainzConfiguration__` (`UserAgent`) | `IMusicBrainzClient` |
| UPCitemdb | Barcode to product (movies/games) | `UpcItemDbConfiguration__` (`ApiKey`) | `IUpcItemDbClient` |
| Plex | Optional read-only library sync (movies) | `PlexConfiguration__` (`BaseUrl`, `Token`, `DetailConcurrency`, `Entities__Movies__Enabled`, `Entities__Movies__Schedule`, `Backfill__RequestsPerMinute`, `Backfill__BatchSize`) | `IPlexClient` (GET only; token sent in the `X-Plex-Token` header, never in URLs); `/plex/status` reports state and `/plex/{entityType}/search?query=` finds titles |

### Plex overview

Plex data is stored apart from movies, one lock serializes every sync, and Plex is only ever read (GET). Matching order and
stored data are described in the sections below; setup and operation are in the [Plex setup guide](../Setup/PLEX_SETUP.md).

### Plex sync runs

`POST /plex/{entityType}/sync` records a run in the `PlexSyncRuns` collection (state `running`, or `queued` while another
run holds the lock), starts it in the background and returns 202 with the run, or 409 with the current run when that entity
type is already in progress. One lock in the singleton `PlexSyncService` serializes runs across all entity types and triggers.
Each run first backfills missing movie ExternalIds from TMDB (below), then lists the Plex libraries for the type (no per-item detail requests), matches movies to Plex items, reads stream detail only for the items entries link to (movies unchanged since the last run reuse their stored streams) and records `succeeded` with `ItemsScanned` (the MediaSet entries processed), `MatchedCount` and `UnmatchedCount`, `ExternalIdsBackfilled` and `BackfillSkipped`, `failed` with a message that never
holds the token, or `interrupted` (application shutdown, or a restart: `PlexSyncInitializer` closes leftover runs and creates
the `(EntityType, StartedAt desc)` index at startup). The run's cancellation token is linked to application shutdown and passed
to every Plex call; there is no cancel endpoint. `/plex/status` and `/plex/{entityType}/status` report the last run per type.

### Plex ExternalId backfill

The first step of every run is `ExternalIdBackfillService` (`Features/Plex/Services`), so ids it fills can match Plex in the same
run. It selects movies whose `ExternalId` is empty and whose release date has a parseable year (a set id, even one entered by hand, is never selected or changed; a movie without a year is not a candidate and is not counted), searches TMDB
through `ITmdbClient.SearchMovieAsync` by title, and saves `tmdbid:<id>` only when exactly one result has the same title
(case-insensitive) and the same release year as the movie. A lone result that differs in title or year, several matching results,
or no results leave the field blank and count as `BackfillSkipped`. Only `ExternalId` is written. The time of each movie's last search is kept in the `PlexBackfillAttempts` collection (`_id` = movie id, written through `IExternalIdBackfillStore`, purged for deleted movies and movies that got an id), so movies that never resolve rotate behind untried ones instead of using every batch.
It is throttled by `PlexConfiguration__Backfill__RequestsPerMinute` (default 30, requests at least 60/N seconds apart) and
`PlexConfiguration__Backfill__BatchSize` (default 50 movies searched per run, never searched or least recently searched first), both validated at startup (at least 1), and never
reads the background image lookup configuration. `ITmdbClient` exists only when the TMDB section is configured; without it the step
is skipped with a log line and the sync continues. A TMDB error on one movie counts as skipped, a 429 stops the backfill early,
and neither fails the run.

### Plex cover fallback

When an entry is paired with a Plex item (a sync match or a manual link) and has no cover, `PlexCoverService`
(`Features/Plex/Services`) takes the item's poster: `IPlexClient.GetPosterAsync` reads `library/metadata/{ratingKey}/thumb`
(the stored poster, not a resized copy; GET only, token in the header) and `IImageService.SaveImageBytesAsync` stores it like any
other cover (the URL download is not used: it blocks private addresses such as the Plex server). A poster narrower than
`PlexCoverService.MinWidth` (400 px), larger than 10 MB, not JPEG/PNG, or missing is skipped. A cover is never replaced, and
any failure is logged (type only) and never fails the link or the sync run.

### Plex sync schedule

`PlexSyncScheduler` (a hosted service registered only when Plex is enabled) runs one loop per enabled entity type, driven by
`PlexConfiguration__Entities__<Type>__Schedule`, a cron expression that defaults to `0 3 * * 0` (Sundays at 03:00). It has its own
configuration and does not use `BackgroundImageLookupConfiguration`. Times are in the **server local time zone** (`TimeZoneInfo.Local`;
set `TZ` in the container), the same as the image lookup job. At each occurrence it calls `IPlexSyncService.StartAsync(type,
"scheduled")`, so it uses the same lock and run records as a manual sync (`Trigger` is `scheduled`). A failed start is logged and the
schedule continues.

Lock policy (`schedulePolicy: "queue"` in the status output): a scheduled run that finds the shared lock held is recorded as `queued`
and starts when the lock frees; it never runs in parallel. If that entity type already has a queued or running run, the scheduled run
is skipped and a log line says so. `/plex/status` and `/plex/{entityType}/status` return `nextRun` (the next occurrence as a
`DateTimeOffset` in the server's local offset, `null` when nothing is scheduled) and `schedulePolicy`. An invalid cron expression
(or one with no future occurrence) disables only that entity type: startup validation logs a warning and leaves it out of
`PlexEnablement`, and the scheduler also refuses to schedule it. A disabled type, or an absent Plex section, schedules nothing. A run missed because the app was down is not caught up; the next
occurrence runs.
`PlexScheduleCalculator` holds the cron logic and never throws.

### Plex matching and stored data

A run matches each movie to a Plex item first by the stored Plex item id (rating key) from an earlier run, then by
`Movie.ExternalId` (`tmdbid:`, `imdbid:` or `tvdbid:` values) against the item's tmdb, imdb and tvdb GUIDs. There is no title
fallback; unmatched movies stay unmatched. The Plex data lives in the separate `PlexItems` collection (`PlexItemData`, `_id` =
movie id, plus an index on the Plex item id, created at startup), so editing a movie never touches it. For an item with several
versions the best one is stored: highest resolution, then HDR, then larger file. `GET /plex/{entityType}/items/{movieId}` returns
the document (404 when unlinked) and is the only response with the server file path.
`GET /movies/pagedsearch` attaches a small `plex` summary (`onPlex`, `resolution`, `watched`, `broken`; never a file path) to each
linked movie of the page, read for the page's ids in one query through `IPlexLookup.GetMovieSummariesAsync` (a failed read is
logged and skipped), and accepts an optional `plex` filter: `onPlex` (`OnPlex` true), `notOnPlex` (everything not `onPlex`),
`unwatched` (on Plex and not watched) or `broken` (a manual link whose item is gone), case-insensitive. An unknown value is a
400. The filter is resolved to the ids in the `PlexItems` collection and passed to the movie service as an `IdFilter`, so
paging, the total and sorting stay correct. Both are absent (and the filter ignored) when Plex is not enabled for movies, and
other entity types are unaffected.
`GET /stats` gains a `plexStats` object (`onPlex`, `notOnPlex`, `watched`, `unwatched`, `resolutionBreakdown`, `needsLinking`;
counts only, no file path) through `IPlexLookup.GetMovieStatsAsync`, read from the `PlexItems` collection on every request and
added to the cached collection stats after the cache lookup, because a sync changes Plex data without touching any movie (the
cache is cleared only by entity edits). `onPlex` counts documents with `OnPlex` true, `notOnPlex` is the movie total minus that
(the `notOnPlex` list filter's set), `watched` and `unwatched` split the on-Plex ones, `resolutionBreakdown` groups them by
resolution (`unknown` when none stored) and `needsLinking` is the movies with no document plus broken links. It is null (omitted
from the dashboard) when Plex is not enabled for movies or the read fails (logged and skipped). The dashboard controller
(`app/actions/controller.tsx`) drops `plexStats` unless the request-scoped gate is true, and `PlexSection`
(`app/ui/dashboard/plex-section.tsx`, in the Movies panel) renders the stat cards, the resolution `PieChart` and a Needs linking
card linking to `/movies?plex=notOnPlex`.
`GET /movies/{id}` (the single lookup) adds the same data as a `plex` property without
any file path (item or episodes) for a linked movie, through `IPlexLookup` (registered only when Plex is enabled; otherwise,
or when movies are not enabled, the response is unchanged, and a failed read is logged and skipped). `Movie.Plex` is never
stored (`[BsonIgnore]`) and is cleared on POST and PUT input. Deleting a movie deletes its Plex document
through `IPlexDataCleanup` (registered only when Plex is enabled; a failure is logged, not surfaced), and each run purges documents
of movies that no longer exist. Plex is only ever read.

Per-item stream reads (section listings carry no streams) run with bounded concurrency (`PlexConfiguration__DetailConcurrency`, default 4, minimum 1). A movie whose listed `updatedAt` and a listed file size equal the stored `PlexUpdatedAt` and `SizeBytes` skips its detail request and keeps its stored stream fields; new, changed and legacy (no `PlexUpdatedAt`) movies, and ones whose lookup failed, are read in full. Shows and seasons have no media and never need the request.

Manual links: `PlexItemData.LinkSource` is `auto` (set by a run; a missing value reads as auto) or `manual`.
`PUT /plex/{entityType}/items/{movieId}/link` (body `{ "plexItemId": "..." }`) reads that Plex item and stores it as a manual
link (404 for a missing movie or Plex item); `DELETE .../link` removes the link and its data (204, 404 when unlinked);
`POST .../refresh` re-reads one linked movie under the same lock as runs (400 unlinked, 404 when the Plex item is gone, 409 with
the current run while a sync runs, never queued). A run refreshes a manual link from its own rating key, never re-matches it by
ExternalId or replaces it, flags it `OnPlex = false` if the item is gone, and never purges it unless the movie is deleted.
Link and unlink do not take the lock. Two movies may link to the same Plex item.
`GET .../items/{id}/scope` returns the scope `PlexTvTitleParser` reads from a TV entry's title (`{ kind, from, to }`, kind
`unreadable` when it cannot tell; 404 for a missing entry, 400 for a non-TV entry). It reads only the entry, so it works
linked or not, and never calls Plex; the UI uses it to prefill the link flow's scope step.

The web UI drives these calls from server actions only (`app/api/plex-data.ts`): `/movies/:id/plex/link` is a server-rendered
flow (GET search, then for a TV entry the scope step, POST links and redirects 303) that a dialog enhances by loading the
same route with `fragment=1`; `/movies/:id/plex/unlink` is a confirmation page matching the delete pattern. Failures render
the entity error page with a message per cause (`app/actions/plex/failures.ts`).

The movie list (`app/actions/entity/controller.tsx`) reads the request-scoped gate (`isPlexEnabledForRequest`) and, when it is true,
passes the validated `plex` query value to the paged search, shows the Plex icon on each row (mobile and table) from the
`plex` summary and renders the filter select (`PlexFilterSelect`, a named select in the search form that navigates on change
with JavaScript). `plex` is part of `listHref` and `parseListParams`, so the search, sort, paging and clear-search links keep it.
When the gate is false the param is dropped and no icon or control renders.

The sync page is `/plex` (`app/actions/plex/controller.tsx`, `app/ui/plex/sync-page.tsx`), shown only when the gate
(`isPlexEnabledForMovies`) is true, otherwise 404. It lists each enabled entity type from `GET /plex/status` (last run,
counts, `nextRun`); the Sync now form posts to the same route, which calls `POST /plex/{entityType}/sync` and redirects
303. A 409 is not an error: the page renders again with the current run and a notice. While a run is queued or running the
button is disabled ("Running since <time>") and the `PlexSyncPoller` island polls `/plex/status` (a same-origin route that
forwards the API status uncached, so the browser never calls the API), reloading the page once no run is active. The gear
menu's Plex link is decided per request by `plexNavGate` (`app/utils/plex-nav.server.ts`), which stores the gate result in
async request context for the synchronous header and the Plex handlers (one lookup per page request; it runs after the static files, and asset and `config.json` requests skip it). A 409 from Sync now redirects to `/plex?notice=running`, and the poller stops on a 404, backs off on failures and ignores disabled types.

### Plex statistics

`GET /plex/statistics` (in `PlexApi`, so registered only when Plex is enabled; 400 when movies are not) returns `{ movies, tv }`, each
with coverage (`total`, `onPlex`, `notOnPlex`, `broken`), `units` and ordered `{ name, count }` lists for resolution, video codec, HDR/SDR,
subtitles, watched, audio codec and channel layout, plus `storage`, `runtime`, `mostWatched` (top 10 by view count, titles from the movie list)
and, for TV, `completeness` (episodes present against the count Plex reports). A unit is an on-Plex movie, or an on-Plex episode for TV.
`PlexItemStore.ListStatisticsProjectionAsync` projects the documents (no file path, titles or season list; episodes keep only their stream, size,
duration and watched fields) and the pure `PlexStatisticsCalculator` counts them; documents of no listed entry are ignored. The Plex page
(`app/ui/plex/plex-statistics.tsx`) renders it before the sync section with the dashboard chart components, and renders without it when
the call fails. This is separate from the dashboard's `plexStats`.

### Plex TV series entries

A TV series entry (`Movie.IsTvSeries`) links to a Plex show (item type `show`), never to a movie, and movie entries never link
to shows; runs read both movie and show libraries (search returns both, each result with a `type`). The entry usually covers one
season, a range or the complete series, read from its title by `PlexTvTitleParser` (pure): "Season 2", "Series 2", "S2",
"Season Two", "Second Season", "Seasons 1-3", "Seasons 1 & 2" (consecutive only), "Complete Series" and "All Seasons". No season
text, several phrases, reversed or non-consecutive lists, season 0 or above 99, or a phrase joined to other content ("Seasons 1-3
+ Movie") is unreadable, so the entry stays unmatched until linked by hand; digits in a title ("24: Season 3") are never a season.
A run matches the show by `ExternalId` against the show GUIDs, reads the show's seasons (`GetSeasonsAsync`, once per show per run)
and stores in the entry's `PlexItems` document the show id, the `Scope` (`season`, `range` or `all`, with `From` and `To`),
the show-level fields (library, date added, deep link, last sync time) and, for each season the scope covers, selected by its
index: season rating key, episode count as Plex reports it, date added and deep link. Plex seasons carry no GUIDs. The parser is a heuristic: a real title that contains "Series 7" or "Season 2" is read as that season. An automatic
match needs at least one covered season on Plex; the scope is re-read from the title by each run and by a single-item refresh of an
automatic link (the stored one is kept when the title no longer says). A manual link keeps its scope. Several entries may link to
one show, each with its own document and scope. `PUT .../link` for a TV entry takes an optional
`scope`: `{ "kind": "season", "season": 2 }`, `{ "kind": "range", "from": 1, "to": 3 }` or `{ "kind": "all" }` (seasons 0 to 99);
without it the title is parsed and an unreadable title answers 400. A scope on a non-TV entry is a 400, and a TV entry only links
to a show (404 for a movie item). The backfill also covers TV entries: it searches TMDB TV on `PlexTvTitleParser.CleanTitle`
(season text removed), needs no year, takes only a result named like the cleaned title (a lone differently named hit is not trusted; a title whose season text is joined to other content, such as "Seasons 1-3 + Movie", is not searched), uses the first air year
only to break a tie between same-named shows, and otherwise leaves the field blank. It shares the batch, throttle and rotation.

Episodes are read only for a linked entry (auto-matched or manually linked or refreshed) and only for the seasons its scope
covers: `PlexEpisodeLoader` calls `GetEpisodesAsync` once per season (`library/metadata/{seasonKey}/children`, paged, with a
stream lookup per episode and the best version chosen as for movies), cached per season within a run, in the read phase so
an outage fails the run before any write. The `PlexItems` document embeds `Episodes` (season and episode number, title,
watched, last watched, resolution, HDR, codec, best audio, audio and subtitle languages, duration, container, file path, size,
`OnPlex`, `MissCount`) and a `Rollup` on the entry and on each season, computed by the pure `PlexRollups` from the episodes
that are on Plex: watched count (the entry is watched only when every present episode is, and there is one), latest last
watched, most common resolution (a tie goes to the higher one) with a mixed flag, HDR if any, the union of audio and subtitle
languages with a flag when episodes differ, and episodes present against the count Plex reports (summed for the entry). The
entry's top-level watched, last watched, resolution, HDR and subtitle fields come from that rollup. An episode a reachable
season no longer returns keeps its data with `OnPlex` false and a miss; an automatic link drops it at the second consecutive
miss (`AutoLinkDropMisses`), a manual link keeps it. A season in scope that Plex no longer has treats all its episodes as
missing, and a refresh flags without counting misses. A show gone from Plex leaves stored episodes as they were. Episode file
paths are returned only by the single-item responses (`GET .../items/{id}`, link and refresh).

## Local development

See [DEVELOPMENT.md](DEVELOPMENT.md). The API and frontend run on the host; only MongoDB runs in a container.

## Image storage

- Images are stored on the filesystem, not in MongoDB: `data/images/` at the repository root in development,
  configurable with `ImageConfiguration__StoragePath`.
- Served through `/static/images/{filePath}` (prefixed with `/dev-api` for development browser requests).
- Upload via multipart/form-data or URL download; a background service fetches images asynchronously after
  an entity is created.
