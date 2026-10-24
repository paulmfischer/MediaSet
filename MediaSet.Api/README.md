# MediaSet.Api

Backend REST API service for MediaSet, built with .NET 10.0 and MongoDB.

## Overview

The MediaSet API provides endpoints for managing your personal media library (books, movies, games, and music), along with metadata lookup integration for multiple external APIs.

**Technologies:**
- **.NET 10.0 Web API**: Modern minimal APIs with typed results
- **MongoDB**: Document database for flexible media entity storage
- **External API Integrations**: OpenLibrary, TMDB, IGDB, MusicBrainz, UPCitemdb

## Getting Started

### Prerequisites and setup

Install .NET 10, Podman, and podman-compose. Complete the
[first setup](../Development/DEVELOPMENT.md#first-setup) to configure MongoDB
credentials and .NET User Secrets.

### Development

From the repository root:

```bash
podman-compose -f docker-compose.dev.yml up -d
dotnet watch --project MediaSet.Api run
```

The API listens at http://127.0.0.1:5000; Swagger is at
http://127.0.0.1:5000/swagger. Stop watch with Ctrl+C. VS Code provides
**Launch API** for breakpoint debugging and **Launch API + UI** for both applications.
See the [development guide](../Development/DEVELOPMENT.md) for tasks and troubleshooting.

### Building

```bash
# Build the project
dotnet build MediaSet.Api/MediaSet.Api.csproj

# Run tests
dotnet test MediaSet.Api.Tests/MediaSet.Api.Tests.csproj
```

## Configuration

Native development uses appsettings.Development.json plus .NET User Secrets.
Do not commit passwords or API keys to appsettings files. The root .env configures
MongoDB initialization only; the native API does not load it.

```bash
dotnet user-secrets set "MediaSetDatabaseSettings:ConnectionString" 'mongodb://mediaset:YOUR_APP_PASSWORD@127.0.0.1:27017/MediaSet?authSource=MediaSet' --project MediaSet.Api
dotnet user-secrets set "TmdbConfiguration:BearerToken" "YOUR_TOKEN" --project MediaSet.Api
```

Use the same application password configured in the root .env.
Production containers continue to receive configuration through environment
variables; see [container setup](../Setup/CONTAINER_SETUP.md).

See [Setup](../Setup/) documentation for detailed integration configuration:
- [TMDB_SETUP.md](../Setup/TMDB_SETUP.md) - Movie metadata
- [IGDB_SETUP.md](../Setup/IGDB_SETUP.md) - Game metadata
- [OPENLIBRARY_SETUP.md](../Setup/OPENLIBRARY_SETUP.md) - Book metadata
- [MUSICBRAINZ_SETUP.md](../Setup/MUSICBRAINZ_SETUP.md) - Music metadata
- [UPCITEMDB_SETUP.md](../Setup/UPCITEMDB_SETUP.md) - Barcode lookup

## Project Structure

```
MediaSet.Api/
├── Attributes/       # Custom attributes
├── Clients/          # External API clients
├── Constraints/      # Route constraints
├── Converters/       # JSON converters
├── Entities/         # Entity API endpoints
├── Helpers/          # Utility functions
├── Lookup/           # Metadata lookup services
├── Metadata/         # Metadata management
├── Models/           # Data models and entities
├── Services/         # Core business logic
└── Stats/            # Statistics services
```

## API Endpoints

### Core Entity Operations

All entity types (Books, Movies, Games, Music) support:

**List all:**
```
GET /api/{entityType}
```

**Search:**
```
GET /api/{entityType}/search?searchText={query}&orderBy={field}
```

**Get by ID:**
```
GET /api/{entityType}/{id}
```

**Create:**
```
POST /api/{entityType}
Content-Type: multipart/form-data

Form fields:
- entity: JSON string of the entity
- coverImage: Optional file upload
- imageUrl: Optional URL to download image from
```

**Update:**
```
PUT /api/{entityType}/{id}
Content-Type: multipart/form-data

Form fields:
- entity: JSON string of the updated entity
- coverImage: Optional file upload
- imageUrl: Optional URL to download image from
```

**Delete:**
```
DELETE /api/{entityType}/{id}
```

### Additional Endpoints

**Retrieve cover image:**
```
GET /static/images/{filePath}
```


**Health check:**
```
GET /health
```

**Statistics:**
```
GET /api/stats
```

### Swagger Documentation

Full API documentation is available at http://localhost:5000/swagger when running in development mode.

## Development Resources

- **[Development/DEVELOPMENT.md](../Development/DEVELOPMENT.md)** - Complete development setup and debugging
- **[Development/TESTING.md](../Development/TESTING.md)** - Testing guidelines
- **[MediaSet.Api.Tests/README.md](../MediaSet.Api.Tests/README.md)** - API testing documentation
