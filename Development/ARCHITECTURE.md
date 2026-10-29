# MediaSet architecture

## Project Structure

- `MediaSet.Api/` — .NET backend API
  - `Features/` — Config, Entities, Health, Images, Lookup, Metadata, and Statistics endpoints/services
  - `Infrastructure/` — Caching, DataAccess, Database, Lookup clients/strategies, Serialization, and Storage
  - `Program.cs` — Dependency injection and application startup
- `MediaSet.Api.Analyzers/` — Custom backend analyzers
- `MediaSet.Api.Tests/` — Backend unit tests (NUnit)
- `MediaSet.Remix/` — Remix 3 frontend UI
  - `app/routes.ts` — Route map; `app/router.ts` registers one controller per page area
  - `app/actions/` — Controllers (request handling and data fetching), with their helpers and router tests
  - `app/ui/` — Page components, mirroring `app/actions/`; `public/` folders hold browser code
  - `app/api/` — Server-side entity, stats, metadata, lookup, and image data access functions
  - `server.ts` — Node HTTP entry point
- `Development/` — Development documentation
- `docker-compose.dev.yml` — MongoDB-only local development service
- `Setup/` — Production setup and deployment files
- `assets/` — Static assets

## Architecture Overview

### Backend (.NET 10 Minimal API)

**API Organization:**
- Minimal API endpoints organized using `MapGroup()` pattern
- Entity groups: `/Books`, `/Movies`, `/Games`, `/Musics`; additional groups include `/health`, `/stats`, `/metadata`, `/lookup`, `/config`, and `/images`
- Dependency injection configured in `Program.cs`

**Key Services:**
- `IEntityService<TEntity>` — CRUD operations for all entity types (books, movies, games, music)
- `IMetadataService` — Metadata lookup orchestration using strategy pattern
- `IImageService` — Image upload, download, and storage management
- `ICacheService` — In-memory caching (MemoryCache) for performance optimization
- `IImageLookupService` / `BackgroundImageLookupService` — Background image retrieval from external sources

**Lookup Strategy Pattern:**
- Base interface: `ILookupStrategy<T>` where T is entity type
- Implementations: `BookLookupStrategy`, `MovieLookupStrategy`, `GameLookupStrategy`, `MusicLookupStrategy`
- Factory: `LookupStrategyFactory` for runtime strategy selection
- Two-stage lookup for movies/games: UPCitemdb → TMDB/IGDB
- Direct lookup for books (OpenLibrary) and music (MusicBrainz)

**Entity Model:**
- All entities implement `IEntity` interface
- MongoDB storage with `[BsonId]` attributes
- Custom JSON converters for specific types (runtime, boolean)

### Frontend (Remix 3)

Node runs the TypeScript sources directly, and `tsc` only type checks.

**Routing:**
- `app/routes.ts` declares routes; each page area has a nested route map (for example `routes.entityLists`)
- A controller in `app/actions/<area>/controller.tsx` handles the area's routes and is registered in `app/router.ts`
- Key routes today: `/` (dashboard), `/books`, `/movies`, `/games`, `/musics` (collection lists), `/books/add` (add form)

**Rendering:**
- Actions fetch data, then render a page component from `app/ui/` inside the shared `Document` layout with `context.render()`
- Components come from `remix/component` and render to HTML on the server
- Interactive pieces are client entries under `public/` folders; only `app/**/public/**` and a few shared files are served to the browser (see `app/assets.ts`)

**Data Access Pattern:**
- Server-side data functions in `app/api/*-data.ts` files, called from actions
- Mutations are plain HTML forms handled by POST actions that redirect (see Forms below)
- Server-side API calls use `apiUrl` (localhost in native development; internal API URL in production)
- Browser API/image requests use `clientApiUrl` (`/dev-api` through the dev server proxy in native development)

**Forms** (the Add Book form, `app/actions/entity/add/`, is the template for the other forms):
- The route is `form('/books/add')`: the controller's `index` action renders the page and its `action` handles the POST at the same URL. `formData()` is installed once in `app/router.ts`, so every action reads `context.formData`
- Form-data parsing happens at the controller level, in the action, with a per-entity schema (`book-form.ts`) built on `remix/data-schema` and `remix/data-schema/form-data`. Parse with `s.parseSafe` and save only `parsed.value`
- Shared schema helpers live in `app/actions/forms/form-schema.ts`: `text()` / `requiredText(message)` trim input, `optionalWholeNumber(message)` maps a blank numeric input to no value (`undefined`) instead of `0` or `NaN`, `textList(name)` reads a repeated field (multi-select) trimmed and de-duplicated, `fieldErrors(issues)` keys the first message by field name, and `submittedValues(formData, fields)` echoes the submitted strings back for re-rendering
- On a validation failure the action renders the same page again with status `400`, the submitted values and the field errors, so input is preserved. Do not redirect to the GET page. An API failure re-renders with `500` and a form-level message
- On success the action redirects with `303` so a refresh repeats the GET, not the POST
- Show each error next to its field with the `Field` and `errorProps` helpers in `app/ui/forms/field.tsx`; they link the error to the control with `aria-describedby` and `aria-invalid`
- Hydrated islands live in `app/ui/forms/public/`: `MultiSelectInput` (submits one hidden input per value, so the schema reads it with `textList`), `SingleSelectInput` (one hidden input) and `SaveButton` (pending state from the top frame's `reloadStart` / `reloadComplete`). The rest of the form is server-rendered
- Tests: router tests (`add-book.test.tsx`) cover validation, repeated fields, blank numerics, the 400 re-render and the 303; `app/e2e/add-book.test.e2e.ts` covers error then success without a full reload

**State Management:**
- Server state is fetched per request in actions
- Client state is local to client-entry components
- No global state library

## Code Style & Conventions

Before making changes, read and follow these instruction files:
- `.github/instructions/shared.md` — project-wide rules (commits, workflow, commands)
- `.github/instructions/backend.instructions.md` — .NET code style (applies to `*.cs` files)
- `.github/instructions/frontend.instructions.md` — Remix 3/TypeScript code style (applies to `*.ts`, `*.tsx` files)

## External API Integrations

The application integrates with several external APIs for metadata lookup:

- **OpenLibrary** — Book metadata (ISBN lookup)
  - Config: `OpenLibraryConfiguration__ContactEmail` and `OpenLibraryConfiguration__BaseUrl`
  - Service: `IOpenLibraryClient`

- **TMDB (The Movie Database)** — Movie metadata
  - Config: `TmdbConfiguration__BearerToken`
  - Service: `ITmdbClient`

- **IGDB (Internet Game Database)** — Game metadata
  - Config: `IgdbConfiguration__ClientId` and `IgdbConfiguration__ClientSecret`
  - Service: `IIgdbClient`

- **MusicBrainz** — Music album metadata
  - Config: `MusicBrainzConfiguration__UserAgent`
  - Service: `IMusicBrainzClient`

- **UPCitemdb** — Barcode to product lookup (used for movies/games)
  - Config: `UpcItemDbConfiguration__ApiKey`
  - Service: `IUpcItemDbClient`

Integrations are optional. Use .NET User Secrets for local API credentials and environment variables for production containers. The UI adapts using `/config/integrations` and `/config/lookup-capabilities`.

## Local Development

Follow [DEVELOPMENT.md](DEVELOPMENT.md) for first setup, terminal commands, and VS Code launch/tasks.
The API and frontend run on the host; only MongoDB runs in a container.
Optional mobile HTTPS requires the one-time certificate setup documented there.

## Image Storage

- Images stored on filesystem (not in MongoDB)
- Native development path: `data/images/` at the repository root (`../data/images` relative to the API project)
- Configurable via `ImageConfiguration__StoragePath`
- Images served through `/static/images/{filePath}` (prefixed with `/dev-api` for development browser requests)
- Upload via multipart/form-data or URL download
- Background image lookup service fetches images asynchronously after entity creation
