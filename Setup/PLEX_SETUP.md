# Plex integration — end-user how-to

This document explains how to connect MediaSet to your Plex Media Server. The integration is optional and
**read-only**: MediaSet only reads from Plex and never changes anything on your Plex server. After a sync, a
movie in MediaSet shows whether it is on Plex, its resolution, HDR, video and audio codecs, subtitle languages,
watched state, date added and a link back to Plex. TV series entries (which MediaSet stores as movies) are
linked to Plex shows. Only movies are supported.

1) Find your Plex token

    - Sign in to Plex Web (https://app.plex.tv) and open any item in your library.
    - Open the item's menu, choose "Get Info", then "View XML". The address in the browser ends with
      `X-Plex-Token=<your token>`. Copy that value.
    - Plex also documents this in its support article "Finding an authentication token / X-Plex-Token".
    - The token gives access to your Plex account. Keep it secret (see step 6).

2) Find your base URL

    - This is the address of your Plex server as seen from the `mediaset-api` container, for example
      `http://plex.local:32400` (32400 is Plex's default port).
    - It must be a full `http://` or `https://` address. Inside a container, `localhost` means the container
      itself, so use the server's LAN name or IP address instead.

3) Configure MediaSet (`docker-compose.prod.yml`)

    - Open `docker-compose.prod.yml` and find the commented Plex entries in the `mediaset-api` environment section.
    - Uncomment them and replace the placeholder values:

    ```bash
    # Plex configuration (optional, read-only; omit all PlexConfiguration__ keys to disable)
    PlexConfiguration__BaseUrl: "http://plex.local:32400"
    PlexConfiguration__Token: "[ReplaceThis]"    # <-- replace with your Plex token
    PlexConfiguration__Entities__Movies__Enabled: "true"
    ```

    - Restart the stack:

    ```bash
    docker compose -f docker-compose.prod.yml up -d
    ```

    - If the base URL or token is missing or invalid, the Plex integration stays off and the API log says why
      (never showing the token).

4) Settings reference

    | Setting (all start with `PlexConfiguration__`) | Default | Meaning |
    | --- | --- | --- |
    | `BaseUrl` | none (required) | Address of your Plex server |
    | `Token` | none (required) | Your Plex token (a secret) |
    | `Entities__Movies__Enabled` | `false` | Turns on syncing for movies |
    | `Entities__Movies__Schedule` | `0 3 * * 0` | When to sync automatically (cron, see below) |
    | `Entities__Movies__LibrarySectionIds__0`, `__1`, ... | empty (all libraries) | Which Plex libraries to read |
    | `Timeout` | `30` | Seconds to wait for Plex to answer |
    | `PageSize` | `100` | Items requested from Plex at a time |
    | `DetailConcurrency` | `4` | Detail requests sent to Plex at once; lower it if Plex struggles during a sync |
    | `AcceptInvalidCertificate` | `false` | Accept any HTTPS certificate from Plex |
    | `Backfill__RequestsPerMinute` | `30` | Most TMDB searches per minute when filling in ids (step 7) |
    | `Backfill__BatchSize` | `50` | Most movies searched on TMDB per sync |

    The numeric settings must be above 0. An invalid value switches the Plex integration off, and an invalid
    schedule switches off just the movie sync; the API log names the problem at startup.

5) Schedule

    - `Entities__Movies__Schedule` is a standard five-field cron expression. The default, `0 3 * * 0`, syncs
      every Sunday at 03:00.
    - Times use the server's local time zone, which is UTC in the container unless you set `TZ` on
      `mediaset-api` (for example `TZ: "America/New_York"`).
    - If MediaSet was stopped at the scheduled time, that run is skipped; the next one happens as normal.

    **Library section ids.** By default every movie and TV library on the server is read. To read only some,
    list their Plex library ids:

    ```bash
    PlexConfiguration__Entities__Movies__LibrarySectionIds__0: "1"
    PlexConfiguration__Entities__Movies__LibrarySectionIds__1: "3"
    ```

    To find the ids, open `<your base URL>/library/sections?X-Plex-Token=<your token>` in a browser and read the
    `key` of each library. Do not share that address or save it anywhere, because it contains your token.

    **Certificates and timeouts.** If Plex uses HTTPS with a self-signed certificate, the connection fails.
    Install a trusted certificate (best), use an `http://` address on a network you trust, or set
    `PlexConfiguration__AcceptInvalidCertificate: "true"`. That setting turns off certificate checks for Plex
    only, so use it only on a trusted network. Raise `Timeout` if you have a slow server or a very large library.

6) Keep the token secret

    - Provide `PlexConfiguration__Token` only through the compose environment (or your deployment tool's
      secret store). Do not commit the real value to a repository.
    - MediaSet sends the token to Plex in a request header and keeps it out of its logs, error messages and
      API responses. Do not paste it into issues, chat or screenshots either.
    - If the token leaks, sign out of the devices in your Plex account to invalidate it, then set the new one.

7) How items are matched

    - MediaSet matches each movie to a Plex item in this order: the Plex item it was linked to before (by an
      earlier sync or by hand), then its `ExternalId`.
    - `ExternalId` is written as `source:value`, for example `tmdbid:603`, `imdbid:tt0133093` or
      `tvdbid:78804`. It is compared with the TMDB, IMDb and TVDB ids Plex holds for the item.
    - MediaSet never guesses by title. A movie with no `ExternalId` and no link stays unmatched.
    - A TV series entry links to a Plex show. The seasons it covers are read from its title ("Season 2", "S2",
      a range, or the complete series).
    - **Backfill.** To save you typing ids, each sync first searches TMDB for entries whose `ExternalId` is empty
      and fills it in when there is exactly one clear match. Movies need a release year and an exact title and
      year match. TV series entries are searched on TMDB TV by their title without the season text and need no
      year. Unclear cases are left blank. This needs TMDB to be set up (see [TMDB_SETUP.md](TMDB_SETUP.md));
      without it the step is skipped and the sync carries on. An `ExternalId` you entered is never changed.

8) Run a sync and check status

    - A sync starts on the schedule, or when you ask for one. The API is published on port 8080 in
      `docker-compose.prod.yml`, so from the Docker host:

    ```bash
    # Start a sync now
    curl -X POST http://localhost:8080/plex/movies/sync

    # Check whether it is enabled, the last run (state and counts) and the next scheduled run
    curl http://localhost:8080/plex/movies/status
    ```

    - A run ends as `succeeded` (with how many entries were matched and unmatched), `failed` (with a reason) or
      `interrupted` (MediaSet stopped during it).
    - Only one sync runs at a time. Asking for another while one is running answers 409; wait for it to finish.

9) Link and unlink by hand

    The movie page has buttons for this (step 11). The same actions are available as API calls. Use them when an entry
    was not matched or was matched to the wrong item. A hand-made link is never replaced by a sync. Ids shown as `<...>`
    are placeholders.

    ```bash
    # Find the Plex item id by title (at least 2 characters)
    curl "http://localhost:8080/plex/movies/search?query=The+Matrix"

    # Link a movie entry (<movie-id> is the entry's id in MediaSet) to that Plex item
    curl -X PUT http://localhost:8080/plex/movies/items/<movie-id>/link \
      -H "Content-Type: application/json" -d '{ "plexItemId": "<plex-item-id>" }'

    # Re-read one linked entry from Plex now
    curl -X POST http://localhost:8080/plex/movies/items/<movie-id>/refresh

    # Remove the link and the stored Plex data
    curl -X DELETE http://localhost:8080/plex/movies/items/<movie-id>/link

    # The seasons a TV entry's title says (200 with kind "season", "range", "all" or "unreadable"; 400 for a
    # non-TV entry). It works whether or not the entry is linked and never calls Plex.
    curl http://localhost:8080/plex/movies/items/<movie-id>/scope
    ```

    - For a TV series entry whose title does not say which seasons it covers, add a `scope` to the link body:
      `{ "plexItemId": "<id>", "scope": { "kind": "season", "season": 2 } }`, or `"kind": "range"` with `"from"`
      and `"to"`, or `"kind": "all"`. Seasons run from 0 (specials) to 99. Only TV series entries take a scope.

10) Troubleshooting

    | Problem | What to check |
    | --- | --- |
    | Run failed with "status code 401" | The token is wrong or expired. Get a new one (step 1) |
    | Run failed with "Plex server could not be reached" | Wrong base URL, Plex is down, a firewall is in the way, or `localhost` was used inside the container (step 2). A bad HTTPS certificate gives the same message |
    | Same message and the address is `https://` | The certificate is not trusted; see "Certificates and timeouts" |
    | "Plex request timed out" | Raise `Timeout`, or lower `DetailConcurrency` |
    | Plex calls answer 404 or the status call is missing | The integration is off: check the base URL, token and `Entities__Movies__Enabled`, then the API log at startup |
    | Many entries are unmatched | They have no `ExternalId` and no link. Set up TMDB for the backfill, enter an id, or link by hand |
    | An entry matched the wrong item | Link the right item by hand; a hand-made link wins |
    | Backfill leaves an id blank | No single clear TMDB match, or a movie has no release year. Enter the `ExternalId` yourself |
    | Nothing comes from a library | The library is not in `LibrarySectionIds`, or it is not a movie or TV library |
    | Starting a sync answers 409 | A sync is already running. Wait, then check status |
    | A run shows `interrupted` | MediaSet stopped or restarted during it. Start another sync |
    | Scheduled sync never runs | The movie sync is not enabled, the cron expression is invalid (see the startup log), or the time zone is not what you expect (set `TZ`) |
    | Linking fails | The movie id or Plex item id does not exist, `plexItemId` is missing, or a TV entry needs a `scope` (a movie entry must not send one) |
    | Refresh fails | The entry is not linked yet, or its Plex item is gone from Plex |

11) What you will see in the UI

    The Plex parts of the UI appear only when Plex is enabled for movies. With Plex off, nothing Plex-related is shown.

    - **Plex icon beside the title** of a movie linked to Plex. It looks normal when the movie is on Plex, muted (faded and
      gray) when it is no longer on Plex, carries a small amber dot when a hand-made link is broken, and a green check when
      you have watched it. Hover it for a short description. A movie that is not linked shows no icon.
    - **Plex card** below the movie's details: resolution (with an HDR tag), video codec, best audio track, subtitle
      languages, whether you watched it, when, how many times, the date added, duration, container, size, and when the data
      was last synced ("Synced 3 hours ago"). It never shows the file path.
    - **Open in Plex** opens the item in Plex. **Refresh** re-reads only this movie from Plex and returns to the page; the
      page never refreshes from Plex by itself. If a refresh fails you see a message saying why: Plex could not be
      reached, a sync is running, or the item is missing on Plex.
    - **No longer on Plex:** the card is dimmed with a notice and keeps the last data it had.
    - **Broken link:** a warning notice appears with Relink and Unlink buttons.
    - **Not linked:** the movie shows only a **Link to Plex** button. There is no "Not on Plex" text.
    - **Link to Plex** (and **Relink**, also shown on a linked movie) opens a search prefilled with the movie's title. Each
      result shows its title, year and library; pick one to link it, and you return to the movie page with the new data. For
      a TV series entry you pick a show, then the seasons it covers (one season, a range or the complete series), prefilled
      from the entry's title; the complete series is the default when the title does not say. With JavaScript this opens in a
      dialog; without it the same steps are pages of their own. Plex is only contacted when you open the search.
    - **Unlink** asks for confirmation first (it deletes the stored Plex data; the movie itself is kept). Refresh and link
      need no confirmation.
    - If linking or unlinking fails you see a message saying why: Plex could not be reached, a sync is running, or the item
      is missing on Plex.
    - **Movie list.** Each row of a linked movie shows the same Plex icon next to its title, in the mobile list and the
      table. A filter beside the search box limits the list to **On Plex**, **Not on Plex**, **Unwatched on Plex** (on Plex
      and not watched) or **Broken link**; choosing a value applies it at once, or with the Search button without JavaScript.
      The filter is kept in the address (`?plex=unwatched`) together with the search, sort and page, so a filtered list can
      be bookmarked. Unlinked movies show no icon. With Plex off the filter and icons are absent.
    - **Plex sync page.** Open the gear (Tools) menu and choose **Plex** to reach `/plex`. For each enabled entity type it
      shows the last run (its state, when it ended and the counts it recorded, or the failure message), and the next
      scheduled run. **Sync now** starts a run. While a run is queued or running the button is disabled and reads
      "Running since <time>"; the page then checks the status every few seconds and reloads itself when the run ends.
      If you press **Sync now** while a run is already active (for example from another tab), you see the current run
      with a note rather than an error.
    - **Dashboard.** The Movies & TV tab gains a **Plex** section with cards for **On Plex**, **Not on Plex**, **Watched** and
      **Unwatched** (watched and unwatched count only what is on Plex), a **Plex Resolution** pie chart of what is on Plex
      (movies with no stored resolution show as "Unknown"), and a **Needs linking** card. Needs linking counts the movies
      with no Plex link plus those whose hand-made link is broken; selecting it opens the movie list filtered to **Not on
      Plex**. The same numbers are in the `plexStats` part of `GET /stats`, which is present only when Plex is enabled for
      movies and holds counts only, never a file path. With Plex off the section is absent.
