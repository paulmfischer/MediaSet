# Plex integration — end-user how-to

This document explains how to connect MediaSet to a Plex Media Server. The integration is optional and
**read-only**: MediaSet only reads from Plex (GET requests) and never changes your Plex server. A sync
matches your movie entries (including TV series entries, which MediaSet stores as movies) to Plex items and
stores Plex data about them: whether the item is on Plex, resolution, HDR, codecs, audio and subtitle
languages, watched state, date added and a deep link.

For native local development, Plex movies are already enabled in `MediaSet.Api/appsettings.Development.json`, so
you only need to set the base URL and token with .NET User Secrets (placeholders shown):

```bash
dotnet user-secrets set "PlexConfiguration:BaseUrl" "http://plex.local:32400" --project MediaSet.Api
dotnet user-secrets set "PlexConfiguration:Token" "YOUR_PLEX_TOKEN" --project MediaSet.Api
```

The Compose instructions below apply to production deployments. For the design behind the integration see the
[architecture notes](../Development/ARCHITECTURE.md#external-api-integrations).

Only the `movies` entity type is supported. If the `PlexConfiguration` section is absent, nothing Plex-related
is registered and the `/plex` routes do not exist.

1) Find your Plex token

    - Sign in to Plex Web (https://app.plex.tv) and open any item in your library.
    - Open the item's menu, choose "Get Info", then "View XML". The address bar of the XML page ends in
      `X-Plex-Token=<your token>`. Copy that value.
    - Alternatively, see Plex's own article "Finding an authentication token / X-Plex-Token".
    - The token grants access to your Plex account. Keep it secret (see step 6).

2) Find your base URL

    - `PlexConfiguration__BaseUrl` is the address of your Plex Media Server as seen from the `mediaset-api`
      container, for example `http://plex.local:32400` (32400 is Plex's default port).
    - It must be an absolute `http` or `https` URL. Inside Docker, `localhost` is the container itself, so use
      the server's LAN name or IP address, or a container name on a shared network.

3) Configure MediaSet

    - Open `docker-compose.prod.yml`, find the `mediaset-api` environment section and uncomment the Plex entries:

    ```bash
    # Plex configuration (optional, read-only; omit all PlexConfiguration__ keys to disable)
    PlexConfiguration__BaseUrl: "http://plex.local:32400"
    PlexConfiguration__Token: "[ReplaceThis]"
    PlexConfiguration__Entities__Movies__Enabled: "true"
    PlexConfiguration__Entities__Movies__Schedule: "0 3 * * 0"
    ```

    - Restart the stack: `docker compose -f docker-compose.prod.yml up -d`.
    - A missing or invalid `BaseUrl` or `Token` disables the whole integration. Startup logs a warning that
      names the problem and never includes the token.
    - `Entities__Movies__Enabled` must be `true`; a type that is not enabled is never synced or scheduled.

4) Settings reference

    | Key (prefix `PlexConfiguration__`) | Default | Meaning |
    | --- | --- | --- |
    | `BaseUrl` | none (required) | Plex server address, absolute http(s) URL |
    | `Token` | none (required) | Plex token, a secret |
    | `Timeout` | `30` | Seconds before a Plex request times out; must be above 0 |
    | `PageSize` | `100` | Items requested per page when listing a library; must be above 0 |
    | `DetailConcurrency` | `4` | Most per-item detail (stream) requests in flight at once; values below 1 are treated as 1 |
    | `AcceptInvalidCertificate` | `false` | Accept any TLS certificate from the Plex server (see below) |
    | `Backfill__RequestsPerMinute` | `30` | Most TMDB searches per minute during the ExternalId backfill; at least 1 |
    | `Backfill__BatchSize` | `50` | Most movies searched on TMDB per sync; at least 1 |
    | `Entities__Movies__Enabled` | `false` | Turns the integration on for movies |
    | `Entities__Movies__Schedule` | `0 3 * * 0` | Cron expression for scheduled syncs |
    | `Entities__Movies__LibrarySectionIds__0`, `__1`, ... | empty (all) | Plex library section ids to read |

    A non-positive `Timeout` or `PageSize`, a `Backfill` value below 1 or a malformed value disables the whole
    integration (a warning is logged at startup). An unsupported entity key, or an enabled type whose
    schedule is not a valid cron expression, disables only that entity type.

    **Schedule.** The schedule is a standard five-field cron expression in the server's local time zone, so
    set `TZ` on the container if it should not be UTC. The default runs on Sundays at 03:00. A run missed
    because MediaSet was down is not caught up; the next occurrence runs. A scheduled run that finds another
    sync holding the lock is recorded as `queued` and starts when the lock frees; if the same type already
    has a queued or running run, the scheduled run is skipped.

    **Library section ids.** By default every movie and show library on the server is read. To limit that,
    list the section ids, for example `PlexConfiguration__Entities__Movies__LibrarySectionIds__0: "1"`.
    To find a library's id, request `<BaseUrl>/library/sections` with your token (for example with curl and
    the `X-Plex-Token` header) and read the `key` of each library. Ids that match no library read nothing.

    **Certificates and timeouts.** If Plex is served over HTTPS with a self-signed or mismatched
    certificate, either install a trusted certificate (preferred), use an `http` URL on a trusted network,
    or set `PlexConfiguration__AcceptInvalidCertificate: "true"`. That setting turns off certificate
    validation for Plex requests only, so use it only on a network you trust. Raise `Timeout` for a slow
    server or a large library; lower `PageSize` or `DetailConcurrency` if the server struggles under load.

5) Run a sync

    - A sync runs on the schedule, or on demand with `POST /plex/movies/sync` (returns 202 with the run).
    - `GET /plex/status` and `GET /plex/movies/status` report whether the type is enabled, the last run (state,
      counts, message) and the next scheduled run. A run ends `succeeded`, `failed` (with a message that never
      holds the token) or `interrupted` (the application stopped mid-run).
    - One lock serializes all syncs. Starting a manual sync while one is queued or running returns
      **409 Conflict** with the current run in the response; wait for it to finish. There is no cancel endpoint.

6) Keep the token secret

    - Locally, store it with user-secrets, not in a file that is committed:

    ```bash
    dotnet user-secrets set "PlexConfiguration:Token" "YOUR_PLEX_TOKEN" --project MediaSet.Api
    ```

    - In production, supply `PlexConfiguration__Token` through the environment or your deployment tooling,
      and do not commit the real value.
    - MediaSet sends the token only in the `X-Plex-Token` request header, never in a URL, redacts it from
      HTTP client logs, and never writes it to log lines, run messages, error responses or API responses.
      Do not paste it into issues or screenshots either.

7) How items are matched

    - Each sync first backfills missing ExternalIds (below), then matches every movie entry in this order:
      1. the Plex item id stored from an earlier sync or a manual link, then
      2. the entry's `ExternalId` against the Plex item's TMDB, IMDb and TVDB ids.
    - There is no title fallback: an entry with no `ExternalId` and no link stays unmatched.
    - A TV series entry (`IsTvSeries`) links to a Plex **show**, with the seasons it covers read from its
      title ("Season 2", "S2", a range, or the complete series). A movie entry links to a Plex movie.
    - Plex data is stored in a separate collection, so editing an entry never touches it.
    - `GET /movies/{id}` includes a `plex` property for a linked movie, without the server file path. The
      file path is only in `GET /plex/movies/items/{movieId}`.

    **ExternalId.** The field is a `source:value` string such as `tmdbid:603`, `imdbid:tt0133093` or
    `tvdbid:78804`. The prefixes `tmdb`, `imdb` and `tvdb` (with or without the `id` suffix) are
    recognised; matching ignores case.

    **Backfill.** To get entries matched without typing ids, each sync first searches TMDB for movies whose
    `ExternalId` is empty and that have a release year. (TV series entries are backfilled too, through TMDB TV
    by their title with the season text removed; they need no year, which only breaks a tie.) It saves `tmdbid:<id>` only when exactly one result
    has the same title (ignoring case) and the same release year. Anything unclear is left blank and counted
    as skipped. It needs `TmdbConfiguration` (see [TMDB_SETUP.md](TMDB_SETUP.md)); without TMDB the step is
    skipped and the sync continues. It is throttled by the two `Backfill__` settings above, and movies that
    never resolve rotate behind untried ones so each sync works through the library.
    A value already in `ExternalId`, including one you typed, is never changed or overwritten.

8) Link and unlink by hand

    When automatic matching misses an entry, find the Plex item and link it. A manual link is never
    re-matched or replaced by a sync, and is refreshed from its own Plex item.

    - Search: `GET /plex/movies/search?query=<title>` (at least 2 characters) returns matching Plex items with their ids.
    - Link: `PUT /plex/movies/items/{movieId}/link` with body `{ "plexItemId": "<id from search>" }`.
      For a TV series entry the seasons are read from the title; if they cannot be, send
      `"scope": { "kind": "season", "season": 2 }`, `{ "kind": "range", "from": 1, "to": 3 }` or `{ "kind": "all" }`.
    - Refresh one entry now: `POST /plex/movies/items/{movieId}/refresh` (409 while a sync runs).
    - Unlink: `DELETE /plex/movies/items/{movieId}/link` removes the link and its stored data (204).
    - Deleting a movie also deletes its Plex data. Plex itself is never modified.

9) Troubleshooting

    | Symptom | Likely cause and fix |
    | --- | --- |
    | Every `/plex/...` route returns 404 | The Plex section is missing or invalid, so the routes are not registered. Check the startup warnings in the API log |
    | `/plex/xyz/...` returns 404 | Unknown entity type; only `movies` is supported |
    | `/plex/movies/...` returns 400 "entity type disabled" | `Entities__Movies__Enabled` is not `true`, or its schedule is invalid (startup warning). Only the status routes work for a disabled type |
    | Run `failed` with "Plex request failed (status code 401)" | Wrong or expired token. Get a new token (step 1) |
    | Run `failed` with "Plex server could not be reached" | Wrong `BaseUrl`, server down, firewall, or `localhost` used inside a container. This message also covers TLS errors |
    | Same message and the URL is `https` | Certificate not trusted by the container. Use a trusted certificate or `AcceptInvalidCertificate` (step 4) |
    | "Plex request timed out" | Raise `Timeout`, lower `PageSize` or `DetailConcurrency` |
    | Many entries unmatched | They have no `ExternalId` and no link. Add a `tmdbid:`, `imdbid:` or `tvdbid:` value, configure TMDB for the backfill, or link by hand. Set the log level to Debug to see the reason per entry |
    | Entry matched the wrong item | Link the right item by hand; a manual link wins over `ExternalId` |
    | Backfill leaves entries blank | No unique TMDB result with the same title and year (movies), or no clear TMDB TV match (TV entries), or a movie has no release year. Set `ExternalId` yourself |
    | Nothing is synced from a library | `LibrarySectionIds` does not include it, or it is not a movie or show library |
    | `POST .../sync` returns 409 | A sync for that type is already queued or running. Wait for it; check `/plex/movies/status` |
    | Run `interrupted` | The application stopped or restarted mid-run. Start another sync |
    | Scheduled sync never runs | Invalid cron expression (startup warning), the type is disabled, or `TZ` is not what you expect |
    | Search or link returns 502 | Plex was unreachable or returned an error; the message is the same fixed text as above |
    | Link returns 404 | No movie with that id, or Plex has no movie (or, for a TV entry, show) with that `plexItemId` |
    | Link returns 400 | Missing `plexItemId`; for a TV entry a scope that cannot be read from the title ("Scope required") or is invalid (kind must be `season`, `range` or `all`, seasons 0 to 99, `from` not above `to`); or a `scope` sent for a non-TV entry |
    | Refresh returns 400 | The entry is not linked yet; link it first. A 404 means the movie or its Plex item is gone |
