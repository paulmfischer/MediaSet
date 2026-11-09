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
| Plex | Optional read-only library sync (movies) | `PlexConfiguration__` (`BaseUrl`, `Token`, `Entities__Movies__Enabled`, `Entities__Movies__Schedule`) | `IPlexClient` (GET only; token sent in the `X-Plex-Token` header, never in URLs); `/plex/status` reports state and `/plex/{entityType}/search?query=` finds titles |

### Plex sync runs

`POST /plex/{entityType}/sync` records a run in the `PlexSyncRuns` collection (state `running`, or `queued` while another
run holds the lock), starts it in the background and returns 202 with the run, or 409 with the current run when that entity
type is already in progress. One lock in the singleton `PlexSyncService` serializes runs across all entity types and triggers.
Each run reads the Plex libraries for the type, matches movies to Plex items and records `succeeded` with `ItemsScanned`, `MatchedCount` and `UnmatchedCount`, `failed` with a message that never
holds the token, or `interrupted` (application shutdown, or a restart: `PlexSyncInitializer` closes leftover runs and creates
the `(EntityType, StartedAt desc)` index at startup). The run's cancellation token is linked to application shutdown and passed
to every Plex call; there is no cancel endpoint. `/plex/status` and `/plex/{entityType}/status` report the last run per type.

### Plex matching and stored data

A run matches each movie to a Plex item first by the stored Plex item id (rating key) from an earlier run, then by
`Movie.ExternalId` (`tmdbid:`, `imdbid:` or `tvdbid:` values) against the item's tmdb, imdb and tvdb GUIDs. There is no title
fallback; unmatched movies stay unmatched. The Plex data lives in the separate `PlexItems` collection (`PlexItemData`, `_id` =
movie id, plus an index on the Plex item id, created at startup), so editing a movie never touches it. For an item with several
versions the best one is stored: highest resolution, then HDR, then larger file. `GET /plex/{entityType}/items/{movieId}` returns
the document (404 when unlinked) and is the only response with the server file path. Deleting a movie deletes its Plex document
through `IPlexDataCleanup` (registered only when Plex is enabled; a failure is logged, not surfaced), and each run purges documents
of movies that no longer exist. Plex is only ever read.

Manual links: `PlexItemData.LinkSource` is `auto` (set by a run; a missing value reads as auto) or `manual`.
`PUT /plex/{entityType}/items/{movieId}/link` (body `{ "plexItemId": "..." }`) reads that Plex item and stores it as a manual
link (404 for a missing movie or Plex item); `DELETE .../link` removes the link and its data (204, 404 when unlinked);
`POST .../refresh` re-reads one linked movie under the same lock as runs (400 unlinked, 404 when the Plex item is gone, 409 with
the current run while a sync runs, never queued). A run refreshes a manual link from its own rating key, never re-matches it by
ExternalId or replaces it, flags it `OnPlex = false` if the item is gone, and never purges it unless the movie is deleted.
Link and unlink do not take the lock. Two movies may link to the same Plex item.

## Local development

See [DEVELOPMENT.md](DEVELOPMENT.md). The API and frontend run on the host; only MongoDB runs in a container.

## Image storage

- Images are stored on the filesystem, not in MongoDB: `data/images/` at the repository root in development,
  configurable with `ImageConfiguration__StoragePath`.
- Served through `/static/images/{filePath}` (prefixed with `/dev-api` for development browser requests).
- Upload via multipart/form-data or URL download; a background service fetches images asynchronously after
  an entity is created.
