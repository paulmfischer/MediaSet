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
- **Image Storage**: Local filesystem configuration for cover images

See the main project [README.md](../README.md) for detailed API key setup instructions.

### Image Storage Configuration

Images are stored locally on the filesystem. Configure storage settings in `appsettings.json`:

```json
{
  "ImageConfiguration": {
    "StoragePath": "/app/data/images",
    "MaxFileSizeMb": 5,
    "AllowedImageExtensions": "jpg,jpeg,png",
    "HttpTimeoutSeconds": 30,
    "StripExifData": true
  }
}
```

**Key Settings:**
- **StoragePath**: Directory where image files are stored (can be relative or absolute)
- **MaxFileSizeMb**: Maximum file size per image in megabytes
- **AllowedImageExtensions**: Comma-separated list of allowed file extensions
- **HttpTimeoutSeconds**: Timeout for downloading images from URLs (seconds)
- **StripExifData**: Whether to remove EXIF metadata from uploaded images

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

## Entity Endpoints

### Standard CRUD Operations

All entity types (Books, Movies, Games, Music) provide the following endpoints:

**List all entities:**
```
GET /api/{entityType}
```

**Search entities:**
```
GET /api/{entityType}/search?searchText={query}&orderBy={field}
```

**Get entity by ID:**
```
GET /api/{entityType}/{id}
```

**Create entity with optional image:**
```
POST /api/{entityType}
Content-Type: multipart/form-data

Form fields:
- entity: JSON string of the entity
- coverImage: Optional file upload
- imageUrl: Optional URL to download image from
```

**Update entity with optional image:**
```
PUT /api/{entityType}/{id}
Content-Type: multipart/form-data

Form fields:
- entity: JSON string of the updated entity
- coverImage: Optional file upload
- imageUrl: Optional URL to download image from
```

**Delete entity:**
```
DELETE /api/{entityType}/{id}
```
*Note: Deleting an entity automatically deletes its associated image file.*

## Image Endpoints

### Retrieve Cover Image

```
GET /static/images/{filePath}
```

To retrieve an image, use the `filePath` property from the entity's `coverImage` object.

**Response:**
- `200 OK`: Image file with appropriate Content-Type header (image/jpeg or image/png)
- `404 Not Found`: Image not found

**Features:**
- HTTP caching headers included for browser caching (7-day cache)
- Efficient static file serving
- Compressed responses for bandwidth optimization

**Example:**

First, get an entity to retrieve the image path:
```bash
curl http://localhost:5000/api/books/507f1f77bcf86cd799439011
```

Response includes:
```json
{
  "id": "507f1f77bcf86cd799439011",
  "title": "My Book",
  "coverImage": {
    "fileName": "cover.jpg",
    "filePath": "books/507f1f77bcf86cd799439011-a1b2c3d4.jpg",
    "fileSize": 102400,
    "mimeType": "image/jpeg"
  }
}
```

Then retrieve the image using the `filePath`:
```bash
curl http://localhost:5000/static/images/books/507f1f77bcf86cd799439011-a1b2c3d4.jpg
```

### Delete Cover Image

```
DELETE /api/{entityType}/{entityId}/image
```

**Response:**
- `204 No Content`: Image successfully deleted
- `404 Not Found`: Entity or image not found

**Effect:**
- Removes the image file from filesystem
- Removes the image reference from the entity
- Entity remains intact without image

**Note:** This endpoint removes the cover image from an entity but keeps the entity itself. To delete both the entity and its image, use the entity delete endpoint.

**Example:**
```bash
curl -X DELETE http://localhost:5000/api/books/507f1f77bcf86cd799439011/image
```

## Image Upload Features

### File Upload

When creating or updating an entity, include an image file in the `coverImage` multipart field:

```bash
curl -X POST http://localhost:5000/api/books \
  -F "entity={\"title\":\"My Book\"}" \
  -F "coverImage=@/path/to/image.jpg"
```

**Validation:**
- File type: JPEG or PNG only
- File size: Maximum 5MB (configurable)
- Format: Binary image file

### Image URL Download

When creating or updating an entity, provide an `imageUrl` field:

```bash
curl -X POST http://localhost:5000/api/books \
  -F "entity={\"title\":\"My Book\",\"imageUrl\":\"https://example.com/cover.jpg\"}"
```

**Behavior:**
- Backend downloads the image from the URL
- Image is validated (format, size)
- Image is saved to filesystem
- Image reference is embedded in entity

**Error Handling:**
- Returns 400 Bad Request if URL is invalid or unreachable
- Returns 400 Bad Request if image format is not supported
- Returns 400 Bad Request if image exceeds size limit

### File Upload Takes Precedence

If both `coverImage` file and `imageUrl` are provided:
- File upload is processed
- URL field is ignored/cleared
- File takes precedence

## Project Structure

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