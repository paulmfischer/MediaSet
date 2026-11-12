# Production log review (issue #846)

Review of the Seq logs at `seq.fcortext.net` for `Environment = 'Production'`.

- **Window:** 2026-11-05 00:00Z to 2026-11-12 23:59Z (7 days).
- **Method:** server-side aggregate queries (counts grouped by application, level and
  message template), then bounded samples of 3 to 5 events for each finding. The full dataset was not loaded.
- **Volume in window (Production):** API 10,741 events, BackgroundImage processor 5,706,
  Remix 364, plus a few from other apps (Haversack, RepairTracker) that share the Seq instance.
  Development events (57k from `MediaSet.Api`) share the same instance and are excluded unless noted.
- **Caveat:** a week of logs is a small sample; frequencies below are per week.

## 1. Errors and repeated errors

Production `Error` events: 121 in the BackgroundImage processor and 19 in the API.
Most are expected misses logged at the wrong level; one is a real bug.

| Finding | Count | Verdict |
|---|---|---|
| `HTTP error downloading image from {ImageUrl}` plus the paired `HTTP GET {RequestUri}` span at Error | 66 + 53 | Not a bug. Open Library (`?default=false`) and Cover Art Archive return 404 when no cover exists. Logged twice, both at Error. |
| MusicBrainz `rate limit exceeded` on `GET /lookup` (2026-11-06 12:46Z) surfaces as an **unhandled exception and HTTP 500** | 1 (5 related events) | **Bug.** `MusicBrainzClient.GetReleaseByIdAsync` (line 114) throws `HttpRequestException`; `LookupApi` (line 140) does not map it. Should return 429/503 with a clear message. |
| MusicBrainz rate limit and HTTP error in the background processor (2026-11-08 08:06Z) | 2 | Provider limit. Handled; the entity just gets no image. Consider a per-provider delay/retry. |
| Seq sink connectivity: `HTTP connection_setup` / `socket connect` to `192.168.50.221:5341` at Error (2026-11-08 16:20-16:53Z) | 12 | Transient Seq unavailability (about 30 min). Not an app bug. Note it is emitted by the instrumentation of the logging sink itself. |
| `Content-Length ... exceeds maximum` for an image | 1 | Working as designed. |

Production `Warning` events worth noting:

- **TMDB `GET /tv/{id}/season/0` returns 404** (5 shows, 18 warnings). The code asks for
  "season 0" (specials) on shows that have none. Treat 404 here as normal and log at Debug, or skip season 0.
- `No unambiguous TMDB match found for title` (28 processor + 4 API), `No IGDB results found` (17): see item 4.
- `Plex item {RatingKey} is linked by movies {A} and {B}` (3): a data-quality signal (duplicate Plex links),
  possibly worth surfacing in the UI rather than only in logs.
- UpcItemDb 429 burst limits (3) and one daily-limit exhaustion: handled by existing pause logic.
- `Overriding HTTP_PORTS ... Binding to URLS` (7, one per start) and Haversack's
  `Failed to determine the https port for redirect` (4): startup config noise. The first is likely resolved by removing
  `HTTP_PORTS`/`HTTPS_PORTS` from the container environment (unverified); the second belongs to another app.

No other repeated application errors were seen. The 401 responses from TMDB found in an
unfiltered query were all `Environment = Development` (a missing or invalid dev key), not Production.

## 2. Information logs: over-logging

Production `Information` is 10,700 (API) + 5,454 (processor) events a week, about 95% of all
Production events. Most of it is framework or duplicated:

| Source | Events / week | Value | Recommendation |
|---|---|---|---|
| `HttpLoggingMiddleware` `{HttpLog:l}` (multi-line request text) | 1,854 | Low. Duplicates the span below and bloats storage. | Remove, or limit to non-2xx. |
| `SerilogTracing ... HTTP {RequestMethod} {RequestPath}` request span | 1,853 | Good: one per request. | Keep. |
| `StaticFileMiddleware` "Sending file. Request path ..." | 1,300 | None. One per image fetched by the UI. | Override `Microsoft.AspNetCore.StaticFiles` to Warning. |
| `Experimental.System.Net.Sockets/Connections` to Seq `:5341` | 792 | None, and feedback from the Seq sink itself. | Override `Experimental.System.Net` to Warning (or filter by port 5341). |
| `socket connect` to MongoDB `:27017` | 421 | None. | Same override. |
| `OkObjectResult` "Setting HTTP status code" / "Writing value of type" | 701 | None. | Override `Microsoft.AspNetCore.Http.Result` to Warning. |
| `MemoryCacheService` "Removed cache entry"/"Removed N entries matching pattern" and `Invalidated caches for entity type` | 467 | Low (one burst per update). | Move to Debug. |
| Processor: four `HttpClient` handler events per call (Start/Sending/Received/End) plus the span | about 2,900 of 5,454 | Redundant (span has the same data). | Override `System.Net.Http.HttpClient` to Warning. |
| Processor: `Image run attempt: {@Attempt}` and `Image lookup outcome` per entity | 204 | **High value.** | Keep. |
| Endpoint `GetList`/`Get`/`Update` handler lines, `Requesting {property} for {media}` | about 900 | Low, redundant with the request span. | Move to Debug. |

Applying the namespace overrides above would cut roughly 60-70% of Production volume without
losing information. Event names are the only change needed, since the overrides in
`LoggingExtensions.cs` already follow this pattern.

## 3. Secrets

No evidence of secrets in the Production events checked:

- No message contained `apikey`, `api_key`, `Bearer`, `password`, `token=` or `Authorization`.
- No logged request URI (TMDB, MusicBrainz, Open Library, Cover Art Archive, UPCitemdb) contained a query string
  other than Open Library's `?default=false`; this suggests (not verified in code) that TMDB keys are sent in a header, not the URL.
- Sampled `HttpLog` events contain only method, path and (spot check) no headers.

Limits of this check: it covered message text, URIs and `HttpLog`; it did not scan every
structured property (such as `@Attempt`) or exception text for tokens. The Plex token is
the highest-value item to confirm, since Plex sync URLs commonly carry `X-Plex-Token` as a query
parameter. Follow-up: unit test that outgoing-request logging strips query strings, and a
saved Seq query for `X-Plex-Token` / `token=` that should stay empty.

## 4. Background image processor

The processor runs at about 08:00 daily and logged 102 attempts a week. 84 `Failed to find image`
events (77 distinct entities, so almost no repeat attempts) break down as:

| Media | Failures | Reason (from the `Error` property) |
|---|---|---|
| Movies | 32 | `No image URL: no matching entity` |
| Books | 27 + 1 | no matching entity / no usable artwork |
| Games | 16 + 1 | no matching entity / no usable artwork |
| Music | 3 + 3 + 1 | no usable artwork / no match / provider request failed |

Why images are not found:

1. **Noisy titles (Movies, Games).** The unmatched titles are retail strings, not titles:
   `20th Century Fox Home Entertainment - Firefly: The Complete Series (DVD`,
   `Twentieth Century Fx From Hell Dvd Ws`, `Red Dead Redemption 2 Rockstar Games 710425498916`
   (a UPC in the title), `Family Guy Volume 1: Seasons 1 & 2`. Normalisation could strip studio
   names, format words (`DVD`, `WS`, `AS SEEN ON`), parenthesis fragments and trailing UPC/EAN digits,
   and try a series/season fallback (`Eureka: Season 3.5`).
2. **No cover at Open Library (Books).** 66 download-error events, most URLs appearing twice (not investigated why: either a retry or a
   second lookup pass). Whether a fallback provider is tried after the 404 was not checked in code.
   A fallback (e.g. Google Books cover by ISBN) is worth evaluating.
3. **Provider limits (Music).** One MusicBrainz rate-limit event during a processor run.
4. **Matched but no artwork (5).** Entity found, no image. Worth recording the provider/id in the log.

Improvements:

- Log the outcome at `Information` with one structured event per entity (already present) and demote the
  per-attempt 404 `Error` pair to `Debug`.
- Record unmatched entities so they are not retried every day (we could not tell from logs whether
  a negative cache exists; only 7 of 77 were seen twice, so retries are already rare).
- Treat TMDB season 0 404 as expected (item 1).

## 5. Metadata and structure

- **Plex has no `Application`.** Plex sync logs under `Application = 'MediaSet.Api'` (about 25
  events a week), distinguishable only by `SourceContext` (`...PlexSyncService`, `...PlexSyncScheduler`).
  It already carries a `RunId`. Recommendation: add a `Component` property
  (`Plex`, `Lookup`, `Images`, `Stats`) via the logger scope, rather than a new `Application` per feature, so `Application`
  stays one-per-process. If a per-process split is wanted for Plex, it is only worthwhile when Plex runs in a separate worker.
- **Inconsistent `Environment` values.** Remix logs `production`, `Production`, `development` and `Development`;
  `RepairTracker.Server` has none. Queries on `Environment = 'Production'` miss the 404 lowercase `production` Remix events.
  Normalise the casing in the Remix logger.
- **Remix events have `SourceContext = null` and a free-text `{Message}` template**, so they cannot be
  grouped. Use a real message template with properties (`entity`, `id`, `count`).
- **Add `Provider` (TMDB, IGDB, OpenLibrary, MusicBrainz, UpcItemDb) and `MediaType`** as properties on
  lookup and image logs, so failures can be filtered by upstream.
- **Add `Trigger`** (scheduled/manual/api) on processor runs; Plex already has `{Trigger}`.
- **Seq noise:** keep dev and prod separate. Dev emits ~5x more events than prod; use a separate
  API key/workspace or a Seq retention/signal rule, because dev stack traces (with local paths) show up in the same instance.

## Proposed follow-up issues

1. Bug: map MusicBrainz/lookup provider `HttpRequestException` (rate limit) to 429/503 instead of an unhandled 500.
2. Chore: raise log level overrides for framework/HTTP noise (static files, result writer, HttpClient handler,
   Experimental.System.Net, HttpLog) to cut Production volume 60-70%.
3. Chore: demote expected 404s (cover download, TMDB season 0) from Error/Warning to Debug.
4. Feat: add `Component` and `Provider` properties; fix Remix `Environment` casing and message templates.
   Plex additions: when the Plex sync runs on its schedule, log it under a distinct `Application` value
   (separate from `MediaSet.Api`), and surface the `Plex item {RatingKey} is linked by movies {A} and {B}`
   data-quality warning to the user (not only in logs).
5. Feat: improve title normalisation (studio, format words, UPC) for TMDB/IGDB matching.
6. Feat: add a fallback cover provider for books after an Open Library 404.
7. Chore: verify that no secrets (`X-Plex-Token`, API keys) reach logs; add a test and a saved Seq query.
8. Chore: use a separate Seq workspace and API key for Development and Production logging, so dev events
   (and their local-path stack traces) stop mixing with Production.
