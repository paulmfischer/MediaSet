# MediaSet.Api

Backend REST API service for MediaSet, built with .NET 10.0 and MongoDB.

## Overview

The MediaSet API provides endpoints for managing your personal media library (books, movies, games, and music), along with metadata lookup integration for multiple external APIs.

**Technologies:**
- **.NET 10.0 Web API**: Modern minimal APIs with typed results
- **MongoDB**: Document database for flexible media entity storage
- **External API Integrations**: OpenLibrary, TMDB, IGDB, MusicBrainz, UPCitemdb

## Getting Started

### Prerequisites

- Docker or Podman (for containerized development)
- Git

### Development

**Start the API with hot-reload:**

```bash
# From the project root
./dev.sh start api

# View logs
./dev.sh logs api

# Restart after config changes
./dev.sh restart api

# Stop the API
./dev.sh stop api
```

The API will be available at:
- http://localhost:5000
- Swagger Documentation: http://localhost:5000/swagger

### Building

```bash
# Build the project
dotnet build MediaSet.Api/MediaSet.Api.csproj

# Run tests
dotnet test MediaSet.Api.Tests/MediaSet.Api.Tests.csproj
```

## Configuration

Configuration is managed through `appsettings.json` files and environment variables (via `.env` file).

### Local Development Configuration

For local development, create or modify `MediaSet.Api/appsettings.Development.json`:

```json
{
  "MediaSetDatabaseSettings": {
    "ConnectionString": "mongodb://localhost:27017",
    "DatabaseName": "MediaSet"
  },
  "TmdbConfiguration": {
    "BearerToken": "your-tmdb-bearer-token"
  },
  "OpenLibraryConfiguration": {
    "ContactEmail": "your-email@example.com"
  }
}
```

### Docker/Podman Configuration

When running with Docker/Podman, configuration is provided through environment variables in `.env`. There is an example file (`.env.example`) in the root of the project that you can copy and remove `.example` from. This contains a set of configurations for running locally but will require some updates for integrations to fully work.

```bash
# Database
MediaSetDatabaseSettings__ConnectionString=mongodb://mongo:27017
MediaSetDatabaseSettings__DatabaseName=MediaSet

# API Keys
TmdbConfiguration__BearerToken=your-tmdb-bearer-token
IgdbConfiguration__ClientId=your-twitch-client-id
IgdbConfiguration__ClientSecret=your-twitch-client-secret
OpenLibraryConfiguration__ContactEmail=your-email@example.com
```

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