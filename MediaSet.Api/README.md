# MediaSet.Api

Backend API service for MediaSet, built with .NET 9.0 and MongoDB.

## Overview

This is the backend REST API that powers MediaSet's media library management system. It provides endpoints for managing books, movies, and games, along with metadata lookup integration for multiple external APIs.

## Technologies

- **.NET 9.0 Web API**: Modern minimal APIs with typed results
- **MongoDB**: Document database for flexible media entity storage
- **External API Integrations**:
  - OpenLibrary (book metadata)
  - The Movie Database (TMDB)
  - GiantBomb (game metadata)
  - UPCitemdb (barcode lookup)

## Development

### Prerequisites

- .NET 9.0 SDK
- MongoDB (or use Docker)

### Running Locally

```bash
# From the MediaSet.Api directory
dotnet run

# With hot-reload (watch mode)
dotnet watch run

# API will be available at http://localhost:5000
```

### Using Docker (Recommended)

**From the project root:**

```bash
# Start API only
./dev.sh start api

# Start API with MongoDB
./dev.sh start all
```

**From the MediaSet.Api directory:**

```bash
# Build the Docker image
docker build -t mediaset-api .

# Run the container
docker run -it --rm -p 5000:8080 --name mediaset-api \
  -e "MediaSetDatabase:ConnectionString=mongodb://host.docker.internal:27017" \
  -e "MediaSetDatabase:DatabaseName=MediaSet" \
  mediaset-api
```

### Building

```bash
# Build the project
dotnet build

# Publish for production
dotnet publish -c Release
```

## Configuration

API configuration is managed through `appsettings.json` and `appsettings.Development.json`.

Key settings:
- **MongoDB Connection**: Database connection string and name
- **TMDB**: Bearer token for movie metadata
- **GiantBomb**: API key for game metadata
- **Caching**: In-memory cache configuration

See the main project [README.md](../README.md) for detailed API key setup instructions.

## Project Structure

```
MediaSet.Api/
├── Attributes/       # Custom attributes (Upload, etc.)
├── Clients/          # External API clients
├── Constraints/      # Route constraints and filters
├── Converters/       # JSON converters
├── Entities/         # Entity API endpoints
├── Helpers/          # Utility functions
├── Lookup/           # Metadata lookup services
├── Metadata/         # Metadata management
├── Models/           # Data models and entities
├── Services/         # Core business logic services
└── Stats/            # Statistics services
```

## Testing

Run unit tests:

```bash
# From project root
dotnet test MediaSet.Api.Tests/MediaSet.Api.Tests.csproj

# From MediaSet.Api directory
cd ../MediaSet.Api.Tests
dotnet test
```

## API Documentation

When running in development mode, Swagger UI is available at:
- http://localhost:5000/swagger

## Code Style

Follow the backend code style guidelines in [.github/code-style-api.md](../.github/code-style-api.md).

Key conventions:
- File-scoped namespaces
- Minimal APIs with route groups
- Async/await for all async operations
- Structured logging
- Constructor injection