# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

# MediaSet

Full-stack application for managing personal media collections.

## Shared Guidelines (REQUIRED READING)

**All agents must follow the rules in:** `.github/instructions/shared.md`

This file contains:
- Project overview and tech stack
- Branch protection rules (NEVER commit to `main`)
- Commit conventions and signing requirements
- AI agent workflow (propose → approve → commit → push)
- Development and testing commands
- General code quality and testing practices

## Project Structure

- `MediaSet.Api/` — .NET backend API (Models, Services, Helpers, endpoints)
  - `Entities/` — MongoDB entity models and API endpoints
  - `Services/` — Business logic (EntityService, MetadataService, ImageService, CacheService, LookupStrategies)
  - `Clients/` — HTTP clients for external APIs (OpenLibrary, TMDB, IGDB, MusicBrainz, UPCitemdb)
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
- Two-stage lookup for movies/games: UPCitemdb → TMDB/IGDB
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

## Code Style & Conventions

Before making changes, read and follow these instruction files:
- `.github/instructions/shared.md` — project-wide rules (branching, commits, workflow, commands)
- `.github/instructions/backend.instructions.md` — .NET code style (applies to `*.cs` files)
- `.github/instructions/frontend.instructions.md` — Remix/TypeScript code style (applies to `*.ts`, `*.tsx` files)

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

Integrations are optional and configured via environment variables. The UI adapts based on configured integrations using the `/integrations` and `/lookup-capabilities` endpoints.

## Image Storage

- Images stored on filesystem (not in MongoDB)
- Default path: `data/images/` (relative to API project root)
- Configurable via `ImageConfiguration__StoragePath`
- Images served as static files via `/images/{id}` endpoint
- Upload via multipart/form-data or URL download
- Background image lookup service fetches images asynchronously after entity creation
