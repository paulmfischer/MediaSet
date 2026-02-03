# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

# MediaSet

Full-stack application for managing personal media collections.

## Tech Stack

- **Backend:** .NET 10 minimal API with MongoDB
- **Frontend:** Remix.js with TypeScript and Tailwind CSS
- **Testing:** NUnit (backend), Vitest + React Testing Library (frontend)

## Project Structure

- `MediaSet.Api/` — .NET backend API (Models, Services, Helpers, endpoints)
  - `Entities/` — MongoDB entity models and API endpoints
  - `Services/` — Business logic (EntityService, MetadataService, ImageService, CacheService, LookupStrategies)
  - `Clients/` — HTTP clients for external APIs (OpenLibrary, TMDB, GiantBomb, MusicBrainz, UPCitemdb)
  - `Models/` — DTOs and configuration models
  - `Config/` — Configuration classes
  - `Helpers/` — Extension methods and utilities
  - `Lookup/` — Lookup API endpoints
  - `Metadata/` — Metadata API endpoints
  - `Stats/` — Statistics API endpoints
  - `Converters/` — JSON converters for MongoDB
- `MediaSet.Api.Tests/` — Backend unit tests (NUnit)
- `MediaSet.Remix/` — Remix.js frontend UI
  - `app/routes/` — Remix route components
  - `app/components/` — Reusable React components
  - `app/*-data.ts` — Data access functions (entity-data, stats-data, metadata-data, lookup-data)
- `Development/` — Docker Compose and local development configuration
- `Setup/` — Production setup and deployment files
- `assets/` — Static assets

## Architecture Overview

### Backend (.NET 10 Minimal API)

**API Organization:**
- Minimal API endpoints organized using `MapGroup()` pattern
- Route groups: `/entities`, `/health`, `/stats`, `/metadata`, `/lookup`
- Dependency injection configured in `Program.cs`

**Key Services:**
- `IEntityService` — CRUD operations for all entity types (books, movies, games, music)
- `IMetadataService` — Metadata lookup orchestration using strategy pattern
- `IImageService` — Image upload, download, and storage management
- `ICacheService` — In-memory caching (MemoryCache) for performance optimization
- `IImageLookupService` / `BackgroundImageLookupService` — Background image retrieval from external sources

**Lookup Strategy Pattern:**
- Base interface: `ILookupStrategy<T>` where T is entity type
- Implementations: `BookLookupStrategy`, `MovieLookupStrategy`, `GameLookupStrategy`, `MusicLookupStrategy`
- Factory: `LookupStrategyFactory` for runtime strategy selection
- Two-stage lookup for movies/games: UPCitemdb → TMDB/GiantBomb
- Direct lookup for books (OpenLibrary) and music (MusicBrainz)

**Entity Model:**
- All entities implement `IEntity` interface
- MongoDB storage with `[BsonId]` attributes
- Custom JSON converters for specific types (runtime, boolean)

### Frontend (Remix.js)

**Routing:**
- Remix file-based routing in `app/routes/`
- Key routes: `_index.tsx` (dashboard), `entities.$type.tsx` (list), `entities.$type.$id.tsx` (detail/edit)

**Data Access Pattern:**
- Server-side data functions in `*-data.ts` files
- `loader` functions for GET requests
- `action` functions for POST/PUT/DELETE mutations
- API calls made server-side via configured `apiUrl` (container-to-container)
- Client-side API calls via `clientApiUrl` (browser-to-API)

**State Management:**
- Remix loaders/actions for server state
- React hooks for client state
- No global state library (uses Remix paradigms)

## Branching & Workflow (CRITICAL)

**⚠️ NEVER commit directly to `main` ⚠️**

Always use feature branches:
```bash
# Create a new feature branch
git checkout -b feature/description-issue-number

# Or for bug fixes
git checkout -b fix/description-issue-number
```

**Workflow:**
1. Sync with main: `git checkout main && git pull`
2. Create feature branch: `git checkout -b feature/your-feature`
3. Make changes and commit to feature branch
4. Push feature branch: `git push -u origin feature/your-feature`
5. Create Pull Request (user responsibility)
6. Merge to main via PR only (never direct push)

## Code Style & Conventions

Before making changes, read and follow these instruction files:
- `.github/copilot-instructions.md` — project-wide rules (branching, commits, workflow)
- `.github/instructions/backend.instructions.md` — .NET code style (applies to `*.cs` files)
- `.github/instructions/frontend.instructions.md` — Remix/TypeScript code style (applies to `*.ts`, `*.tsx` files)

## Development Commands

```bash
# Start entire dev environment
./dev.sh start

# Start individual components
./dev.sh start api           # Backend API only
./dev.sh start frontend      # Frontend UI only

# Restart / stop components
./dev.sh restart api
./dev.sh stop frontend
```

## Testing

```bash
# Backend tests (all)
dotnet test MediaSet.Api.Tests/MediaSet.Api.Tests.csproj

# Backend tests (specific test file)
dotnet test MediaSet.Api.Tests/MediaSet.Api.Tests.csproj --filter "FullyQualifiedName~TestClassName"

# Backend tests (specific test method)
dotnet test MediaSet.Api.Tests/MediaSet.Api.Tests.csproj --filter "FullyQualifiedName~TestClassName.TestMethodName"

# Frontend tests (all)
cd MediaSet.Remix && npm test

# Frontend tests (watch mode)
cd MediaSet.Remix && npm test -- --watch

# Frontend tests (specific file)
cd MediaSet.Remix && npm test -- path/to/test-file.test.ts

# Frontend build
cd MediaSet.Remix && npm run build
```

## External API Integrations

The application integrates with several external APIs for metadata lookup:

- **OpenLibrary** — Book metadata (ISBN lookup)
  - Config: `OpenLibraryConfiguration__ContactEmail` and `OpenLibraryConfiguration__BaseUrl`
  - Service: `IOpenLibraryClient`

- **TMDB (The Movie Database)** — Movie metadata
  - Config: `TmdbConfiguration__BearerToken`
  - Service: `ITmdbClient`

- **GiantBomb** — Game metadata (currently unavailable)
  - Config: `GiantBombConfiguration__ApiKey`
  - Service: `IGiantBombClient`

- **MusicBrainz** — Music album metadata
  - Config: `MusicBrainzConfiguration__UserAgent`
  - Service: `IMusicBrainzClient`

- **UPCitemdb** — Barcode to product lookup (used for movies/games)
  - Config: `UpcItemDbConfiguration__ApiKey`
  - Service: `IUpcItemDbClient`

Integrations are optional and configured via environment variables. The UI adapts based on configured integrations using the `/integrations` and `/lookup-capabilities` endpoints.

## Image Storage

- Images stored on filesystem (not in MongoDB)
- Default path: `data/images/` (relative to API project root)
- Configurable via `ImageConfiguration__StoragePath`
- Images served as static files via `/images/{id}` endpoint
- Upload via multipart/form-data or URL download
- Background image lookup service fetches images asynchronously after entity creation

## Commit Conventions

- Follow Conventional Commits: `type(scope): description`
- Subject line must be ≤ 72 characters
- Reference issues: `closes #N` (final), `refs #N` (intermediate)
- Never commit directly to `main` — use feature branches
