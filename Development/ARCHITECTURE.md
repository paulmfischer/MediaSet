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

## Local development

See [DEVELOPMENT.md](DEVELOPMENT.md). The API and frontend run on the host; only MongoDB runs in a container.

## Image storage

- Images are stored on the filesystem, not in MongoDB: `data/images/` at the repository root in development,
  configurable with `ImageConfiguration__StoragePath`.
- Served through `/static/images/{filePath}` (prefixed with `/dev-api` for development browser requests).
- Upload via multipart/form-data or URL download; a background service fetches images asynchronously after
  an entity is created.
