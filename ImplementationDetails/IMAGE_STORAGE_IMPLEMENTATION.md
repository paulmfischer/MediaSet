# Image Storage Implementation Plan

## Related Issue/Task

**GitHub Issue**: [#151](https://github.com/paulmfischer/MediaSet/issues/151)

## Overview

This feature adds the ability to save and display cover images for media items (books, movies, games, and music). The implementation will support three image source methods:

1. **Upload an image** - User manually uploads a cover image file
2. **Barcode lookup integration** - When using barcode lookups, extract image URLs from API responses
3. **URL field in entity** - Backend automatically downloads and saves images when `imageUrl` field is provided during entity creation/update

The feature stores images as files in the filesystem (organized by media type) with references to file paths stored in the database. This approach provides better performance, easier backup/restore, and simpler image serving compared to storing images as database blobs.

The value to users is the ability to maintain a visual catalog of their media collections with cover art, enhancing the browsing and searching experience.

## Related Existing Functionality

### Backend Components

**Entity Models** (`MediaSet.Api/Models/Book.cs`, `MediaSet.Api/Models/Movie.cs`, `MediaSet.Api/Models/Game.cs`, `MediaSet.Api/Models/Music.cs`)
- All entity types have a common pattern with metadata fields
- Use `[BsonId]` for MongoDB ObjectId
- Have `IsEmpty()` method to check if entity has no data
- Upload attribute used for CSV import field mapping

**Upload Attribute** (`MediaSet.Api/Attributes/UploadAttribute.cs`)
- Custom attribute for marking properties as importable from CSV
- Used for field mapping during bulk imports
- Could be extended or used similarly for other bulk operations

**Services** (`MediaSet.Api/Services/`)
- Pattern of using dependency injection for repositories and external clients
- Structured logging throughout the application
- Async/await patterns for all I/O operations

**Lookup Services** (existing and planned)
- Current barcode lookup infrastructure (OpenLibrary, UPCitemdb, TMDB)
- These lookups return metadata including cover image URLs
- Can be extended to automatically save images

### Frontend Components

**Entity Add/Edit Routes** (`MediaSet.Remix/app/routes/$entity_.add/`, `MediaSet.Remix/app/routes/$entity_.$id.edit/`)
- Existing forms for creating/editing each media type
- Support for ISBN lookup integration
- Form submission pattern using Remix actions

**Entity Display Components** (`MediaSet.Remix/app/routes/$entity_.$id/`)
- Detail view pages for each entity
- Would benefit from displaying cover images
- Existing pattern for displaying entity metadata

**TypeScript Models** (`MediaSet.Remix/app/models.ts`)
- Interfaces for Book, Movie, Game, Music entities
- Could be extended with image-related properties

### Infrastructure/Configuration

**Database Schema**
- MongoDB collections for each entity type (books, movies, games, musics)
- Each collection stores entity data with metadata

**File System**
- Application runs in Docker containers in both development and production
- Both environments use volume mounts for persistent image storage
- Volume mounts persist images across container restarts
- Local files on host machine are mounted into container at `/app/data/images`

**Docker Compose**
- Existing volumes for MongoDB data persistence
- May need additional volume for image storage

## Requirements

### Functional Requirements

1. **Image Upload**
   - User can upload image files (JPEG, PNG) when adding/editing media
   - Uploaded images are validated (file type, file size limits)
   - Images are saved to filesystem with unique naming
   - Image path is stored in entity database record

2. **Image URL in Entity Field**
   - Entity has optional `imageUrl` field (string)
   - Backend receives `imageUrl` during entity creation or update
   - If `imageUrl` is provided and is a valid image URL, backend downloads and saves the image
   - Downloaded image is validated (file type, file size limits)
   - Image path is stored in entity database record alongside the URL reference
   - Handle HTTP redirects, timeouts, and errors gracefully
   - If download fails, entity operation fails with validation error

3. **Image URL from Barcode Lookups**
   - When performing barcode lookups (ISBN, UPC, EAN), extract cover image URL from lookup response if available
   - Return image URL to UI along with entity data
   - User can optionally save the image by submitting entity form with `imageUrl` field populated
   - Backend will download and save the image if URL is valid and image data is valid
   - User can also upload a manual image file, which takes precedence over imageUrl

4. **Image Display**
   - Display cover image on entity detail pages
   - Display cover images in entity list/grid views (where space allows)
   - Serve images efficiently with appropriate HTTP caching headers
   - Fallback to placeholder image if no image exists

5. **Image Management**
   - Delete image when entity is deleted
   - Allow user to change/replace image
   - Allow user to delete image (remove from entity, keep entity)
   - View/download original image file

6. **Image Serving**
   - Serve images via HTTP endpoint: `GET /api/images/{entityType}/{entityId}`
   - Return appropriate content-type header
   - Implement caching headers for browser and CDN caching
   - Support image resizing/thumbnails for UI optimization (future enhancement)

### Non-Functional Requirements

- **Performance**: Image serving < 200ms with caching
- **Storage Efficiency**: Images stored with reasonable file size limits (5MB per image, configurable via appsettings)
- **Reliability**: Graceful handling of missing image files, corrupted files, API timeouts
- **Security**: Validate file types, prevent path traversal attacks, sanitize filenames
- **Accessibility**: Images have alt text describing the media item
- **Data Integrity**: Atomic operations for image operations (upload and record, or both fail)
- **Testing**: Unit tests for image service, integration tests for upload/display flow
- **Documentation**: Clear error messages, API documentation, setup instructions

## Proposed Changes

### Backend Changes (MediaSet.Api)

#### New/Modified Models

**Image.cs** (`MediaSet.Api/Models/Image.cs`)
- Nested model to embed within entity documents
- Properties:
  - `FileName`: original filename (for display)
  - `FilePath`: filesystem path to image
  - `FileSize`: size in bytes
  - `MimeType`: image MIME type (image/jpeg, image/png, etc.)
  - `SourceUrl`: original URL (from user upload, barcode lookup, or imageUrl field)
  - `CreatedAt`: timestamp
  - `UpdatedAt`: timestamp
- Use MongoDB nested BSON serialization (no `[BsonId]`, no ObjectId needed)

**Updated Entity Models** (Book.cs, Movie.cs, Game.cs, Music.cs)
- Add property `Image? CoverImage { get; set; }` to embed image data
- Update `IsEmpty()` method to consider image as part of entity state (optional)
- No separate ImageId needed, image lives within entity document

#### New/Modified Services

**New: IImageService.cs** (`MediaSet.Api/Services/IImageService.cs`)
- Interface for image operations
- Methods:
  - `Task<Image> SaveImageAsync(IFormFile file, string entityType, string entityId, CancellationToken cancellationToken)` - Save uploaded file to storage
  - `Task<Image> DownloadAndSaveImageAsync(string url, string entityType, string entityId, CancellationToken cancellationToken)` - Download image from URL and save to storage
  - `Task<Stream> GetImageStreamAsync(string entityType, string entityId, CancellationToken cancellationToken)` - Retrieve image file stream for serving
  - `Task DeleteImageAsync(string entityType, string entityId, CancellationToken cancellationToken)` - Delete image from storage

**New: ImageService.cs** (`MediaSet.Api/Services/ImageService.cs`)
- Implementation of `IImageService`
- Dependencies: `IImageStorageProvider`, `ILogger<ImageService>`, `HttpClient`, `ImageConfiguration`
- Business logic:
  - `SaveImageAsync`: Validate file type and size, generate unique filename, save to storage, return Image metadata
  - `DownloadAndSaveImageAsync`: Download from URL, validate MIME type, save to storage, return Image metadata
  - `GetImageStreamAsync`: Retrieve file stream from storage for serving via HTTP endpoint
  - `DeleteImageAsync`: Delete image file from storage
  - Validate file type and size for uploads
  - Generate unique filename with GUID to prevent collisions
  - Handle errors gracefully (file write failures, HTTP timeouts, etc.)
- Note: Does NOT fetch/update entities - that responsibility belongs to entity endpoint handlers
- Caller is responsible for updating entity document with the returned Image object

**New: IImageStorageProvider.cs** (`MediaSet.Api/Services/IImageStorageProvider.cs`)
- Abstraction for image storage backend (filesystem, S3, Azure Blob, etc.)
- Methods:
  - `Task SaveImageAsync(byte[] imageData, string relativePath, CancellationToken cancellationToken)`
  - `Task<Stream> GetImageAsync(string relativePath, CancellationToken cancellationToken)`
  - `Task DeleteImageAsync(string relativePath, CancellationToken cancellationToken)`
  - `Task<bool> ExistsAsync(string relativePath, CancellationToken cancellationToken)`

**New: LocalFileStorageProvider.cs** (`MediaSet.Api/Services/LocalFileStorageProvider.cs`)
- Implementation of `IImageStorageProvider` for local filesystem
- Handles directory creation, file I/O, error handling
- Constructs file paths: `/images/{mediaType}/{entityId}-{guid}.{ext}`
- Validates paths to prevent directory traversal attacks

**Extended: Barcode Lookup Services** (existing lookup implementations)
- Update `BookLookupStrategy`, `MovieLookupStrategy`, etc. to extract image URLs from API responses
- Add optional `ImageUrl` property to lookup response models
- Return image URL in lookup response (do NOT save image to storage)
- UI receives lookup response with entity data and optional image URL
- User can then choose to save the image by submitting the entity form with the image URL

#### New/Modified API Endpoints

**Image Retrieve Endpoint**: `GET /api/images/{entityType}/{entityId}`
- Purpose: Get image for media entity
- Parameters:
  - `entityType`: books | movies | games | musics (route parameter)
  - `entityId`: ID of entity (route parameter)
- Request: None
- Response: 200 OK with image file as binary stream
  - Content-Type: image/jpeg | image/png | etc.
  - Cache-Control: public, max-age=86400 (24 hours)
  - ETag: file hash for cache validation
- Error responses:
  - 404 Not Found: Entity not found, image not found for entity
  - 500 Internal Server Error: File read failure
- Authorization: None required (public image serving)

**Image Delete Endpoint**: `DELETE /api/images/{entityType}/{entityId}`
- Purpose: Delete image for media entity
- Parameters:
  - `entityType`: books | movies | games | musics (route parameter)
  - `entityId`: ID of entity (route parameter)
- Request: None
- Response: 204 No Content on success
- Error responses:
  - 404 Not Found: Entity not found, image not found
  - 500 Internal Server Error: File deletion failure
- Authorization: User must have permission to edit entity

**Note on Entity Create/Update with Image**: 
- Entity `POST` (create) and `PUT/PATCH` (update) endpoints accept multipart/form-data requests
- Include optional `coverImage` file field in multipart form data
- Image is processed and embedded in the entity document as part of the same request
- Both entity and image are persisted atomically in a single operation
- When entity is deleted (via `DELETE /api/{entityType}/{entityId}`), the embedded image is automatically deleted with the entity
- Image is optional; entity can be created/updated without image
- If image file is invalid, the entire request returns 400 Bad Request with validation errors

#### Configuration Changes

**appsettings.json** updates:
```json
{
  "ImageStorage": {
    "LocalFile": {
      "RootPath": "/app/data/images",
      "AllowedMimeTypes": ["image/jpeg", "image/png"],
      "MaxFileSizeMb": 5,
      "RequestTimeout": 30
    }
  }
}
```

**Program.cs** updates:
- Register `IImageService` as singleton or scoped
- Register `IImageRepository` as scoped
- Register `IImageStorageProvider` based on configuration
- Bind `ImageConfiguration` from settings
- Create images directory on startup if using local file storage
- Add CORS headers for image endpoints if needed
- Register HttpClient for image downloads from URLs

#### Database Changes

**Update Entity Collections** (Books, Movies, Games, Musics)
- Add optional nested field: `"coverImage": { ... }` containing embedded Image data
- Document structure for entity with image:
  ```
  {
    "_id": ObjectId,
    "title": string,
    ... other entity fields ...,
    "coverImage": {
      "fileName": string,
      "filePath": string,
      "fileSize": number,
      "mimeType": string,
      "sourceUrl": string (optional),
      "createdAt": datetime,
      "updatedAt": datetime
    }
  }
  ```
- No schema migration needed; `coverImage` is optional field (null handled gracefully)
- No new collections needed

### Frontend Changes (MediaSet.Remix)

#### Modified Routes

**Route**: `/books/add`, `/books/:id/edit`, and similar for movies/games/music
- Purpose: Add/edit media with image upload capability
- Updates:
  - Add image upload section in form (file input with drag-and-drop)
  - Display current image if editing existing entity
  - Show image preview after selection
  - Image file is included in multipart/form-data request along with entity fields
  - Both entity and image are submitted together in single form submission
  - Display upload progress and errors inline
  - Receive updated entity with embedded coverImage from API response

**Route**: `/books/:id`, `/movies/:id`, etc. (detail view)
- Purpose: Display media details with cover image
- Updates:
  - Display cover image prominently at top of page
  - Use fallback placeholder image if no cover
  - Make image accessible with alt text
  - Optional: Display image in a modal when clicked
  - Link to download original image (optional)

#### New/Modified Components

**Component**: `ImageUpload` (`MediaSet.Remix/app/components/image-upload.tsx`)
- Props:
  - `entityType: Entity`
  - `entityId: string`
  - `onImageUpload: (imageId: string) => void`
  - `onError: (error: string) => void`
  - `existingImage?: ImageResponse`
  - `isLoading?: boolean`
- Features:
  - File input with drag-and-drop support
  - File type and size validation on client (match server rules)
  - Image preview before upload
  - Upload progress indicator
  - Replace/delete existing image buttons
  - Error message display
  - Accessibility: proper labels, ARIA attributes, keyboard navigation

**Component**: `ImageDisplay` (`MediaSet.Remix/app/components/image-display.tsx`)
- Props:
  - `entityType: Entity`
  - `entityId: string`
  - `alt: string`
  - `size?: 'small' | 'medium' | 'large'`
  - `className?: string`
- Features:
  - Displays image or placeholder
  - Responsive sizing
  - Lazy loading for performance
  - Click to view full size (modal)
  - Accessibility: alt text, semantic HTML

#### Modified Data Functions

**Update Entity Add/Edit Actions** (`MediaSet.Remix/app/routes/$entity_.add/route.tsx`, etc.)
- Handle entity creation/update with optional image file in multipart/form-data
- If image file is included in the form, it's uploaded and embedded in the entity in a single request
- If `imageUrl` field is provided without a file, backend downloads and saves the image
- If both image file and `imageUrl` are provided, the file upload takes precedence and `imageUrl` is cleared/ignored
- Call existing entity API endpoints (`POST /api/books`, `PUT /api/books/{id}`, etc.) with multipart form data
- Entity is returned with embedded `coverImage` field if image was successfully processed
- Handle validation errors (file type, size, entity validation, URL download errors) together in single response
- No separate image upload calls needed

**Backend Entity Endpoint Logic** (POST/PUT/PATCH)
- If request includes both `coverImage` file and `imageUrl` field:
  - Process the file upload (validate, save to storage)
  - Ignore the `imageUrl` field (do not attempt download)
  - Return entity with `coverImage` populated from file upload
- If request includes only `coverImage` file:
  - Process the file upload as normal
- If request includes only `imageUrl` field (no file):
  - Download image from URL (validate MIME type, file size)
  - Save to storage
  - Return entity with `coverImage` populated from URL download
- If old image exists and is being replaced:
  - Delete the old image file from storage
- If download from URL fails:
  - Return 400 Bad Request with validation error describing the URL download failure

**New: deleteImage function** (`MediaSet.Remix/app/image-data.ts`)
- `async deleteImage(entityType: Entity, entityId: string): Promise<void>`
- Makes DELETE request to `/api/images/{entityType}/{entityId}` endpoint
- Removes image from entity while keeping entity intact
- Confirmation prompt before deletion
- Handle deletion errors

#### Type Definitions

**models.ts** (`MediaSet.Remix/app/models.ts`)

Add new types:
```typescript
export type ImageData = {
  fileName: string;
  mimeType: string;
  fileSize: number;
  imageUrl: string;
  createdAt: string;
  updatedAt: string;
};
```

Update `BaseEntity` interface:
```typescript
export interface BaseEntity {
  id: string;
  // ... existing properties ...
  coverImage?: ImageData;
}
```

All entity types (Book, Movie, Game, Music) that extend `BaseEntity` automatically inherit the `coverImage` property.

#### Styling/UI

- Use Tailwind CSS for image components
- Image preview styling (thumbnail, border, shadow)
- Upload progress bar animation
- Error state styling (red border, error icon, error message)
- Responsive image sizing for different screen sizes
- Placeholder styling (neutral color, icon, text)

### Testing Changes

#### Backend Tests (MediaSet.Api.Tests)

**Test Class**: `ImageServiceTests` (`MediaSet.Api.Tests/Services/ImageServiceTests.cs`)
- Scenarios:
  - `UploadImageAsync_WithValidFile_SavesAndReturnsImageResponse`
  - `UploadImageAsync_WithUnsupportedMimeType_ThrowsException`
  - `UploadImageAsync_WithFileTooLarge_ThrowsException`
  - `UploadImageAsync_WithInvalidEntityId_ReturnsNotFound`
  - `UploadImageAsync_WithValidFile_UpdatesEntityWithCoverImage`
  - `DownloadAndSaveImageAsync_WithValidUrl_SavesAndReturnsImageResponse`
  - `DownloadAndSaveImageAsync_WithInvalidUrl_ReturnsNotFound`
  - `DownloadAndSaveImageAsync_WithTimeout_ThrowsException`
  - `DownloadAndSaveImageAsync_WithNonImageContent_Throws`
  - `DeleteImageAsync_WithValidEntity_DeletesFromStorageAndEntity`
  - `DeleteImageAsync_WithMissingFile_HandlesGracefully`
- Mock dependencies: entity repository (BookRepository, etc.), `IImageStorageProvider`, `HttpClient`, `ILogger`
- Edge cases: corrupted files, missing entities, storage failures

**Test Class**: `LocalFileStorageProviderTests` (`MediaSet.Api.Tests/Services/LocalFileStorageProviderTests.cs`)
- Scenarios:
  - `SaveImageAsync_WithValidData_CreatesFile`
  - `SaveImageAsync_WithMissingDirectory_CreatesDirectory`
  - `GetImageAsync_WithExistingFile_ReturnsStream`
  - `GetImageAsync_WithMissingFile_ReturnsNull`
  - `DeleteImageAsync_WithExistingFile_DeletesFile`
  - `PathTraversal_WithMaliciousPath_Rejected`
  - `PathTraversal_WithDotDotSlash_Rejected`
- Mock file system using xunit fixtures or temp directories
- Test actual file I/O operations
- Cleanup temp files after tests

**Test Class**: `LocalFileStorageProviderTests` (`MediaSet.Api.Tests/Services/LocalFileStorageProviderTests.cs`)
- HTTP endpoint tests:
  - `POST /api/images/{entityType}`: upload file, validation, error responses
  - `GET /api/images/{entityType}/{entityId}`: retrieve image, caching headers
  - `DELETE /api/images/{entityType}/{entityId}`: delete image, authorization
- Mock ImageService
- Test request/response serialization
- Test status codes and error responses
- Test multipart form data parsing

#### Frontend Tests (MediaSet.Remix)

**Test File**: `image-upload.test.tsx` (`MediaSet.Remix/app/components/image-upload.test.tsx`)
- Component tests:
  - Renders file input and drag-drop zone
  - Handles file selection
  - Shows file preview for images
  - Shows validation error for unsupported file types
  - Shows error for file too large
  - Triggers onImageUpload callback after successful upload
  - Displays loading state during upload
  - Shows replace/delete buttons for existing image
- Use React Testing Library
- Mock file API
- Mock fetch for upload request

**Test File**: `image-display.test.tsx` (`MediaSet.Remix/app/components/image-display.test.tsx`)
- Component tests:
  - Renders image element
  - Shows placeholder when image not found
  - Applies alt text
  - Applies correct sizing classes
  - Lazy loading attribute present
  - Click opens modal (if implemented)
- Mock image responses

**Test File**: `uploadImage.test.ts` (`MediaSet.Remix/app/image-data.test.ts`)
- Function tests:
  - Constructs correct API URL
  - Sends multipart form data
  - Returns ImageResponse on success
  - Throws error on failure
  - Handles 413 Payload Too Large error
  - Handles 400 Bad Request error

**Test File**: `Entity Add/Edit Routes** (updates to existing tests)
- User flow tests:
  - Add entity with image: uploads image first, then creates entity
  - Edit entity and change image: uploads new image, updates entity
  - Upload fails: shows error, doesn't save entity changes
  - Image optional: can add entity without image

**Test File**: `Entity Detail Route** (updates)
- Display tests:
  - Shows image if exists
  - Shows placeholder if not exists
  - Image renders with correct alt text

#### Integration Tests

**End-to-end scenarios**:
1. Upload image when adding new book → image appears on detail page
2. Update image when editing movie → new image appears
3. Delete entity → image file is deleted from storage
4. Provide imageUrl field → image is downloaded and displayed
5. Barcode lookup returns image URL → user can save image by submitting form with imageUrl field populated
6. Image too large → rejection with error message
7. Invalid file type → rejection with error message

## Implementation Steps

Make these sub-tasks that get added to the parent issue.

1. Create backend Image model and update entity models (Book/Movie/Game/Music with CoverImage property)
2. Implement IImageStorageProvider interface and LocalFileStorageProvider implementation
3. Implement IImageService interface and ImageService implementation
4. Modify entity API endpoints to accept multipart/form-data with optional image file and imageUrl field
5. Add image endpoints for retrieval and deletion (GET and DELETE)
6. Add backend logic to download images from URLs if imageUrl is provided during entity creation/update
7. Register image services in Program.cs with DI container
8. Create TypeScript ImageData type and update BaseEntity model
9. Create ImageUpload component for file input with drag-and-drop and preview
10. Create ImageDisplay component for rendering images with fallback
11. Update entity add/edit routes to include ImageUpload component
12. Update entity detail routes to display images using ImageDisplay component
13. Add image-data.ts utility functions (deleteImage)
14. Integrate barcode lookup to return imageUrl that can be used in entity form submission
15. Create backend unit tests for ImageService
16. Create backend unit tests for LocalFileStorageProvider and API endpoints
17. Create frontend component tests (ImageUpload, ImageDisplay)
18. Create integration tests for image workflows
19. Update documentation and API documentation

## Acceptance Criteria

- [x] Backend: `Image` nested model created with MongoDB BSON serialization
- [x] Backend: Entity models updated with `CoverImage` property
- [x] Backend: `IImageStorageProvider` abstraction allows filesystem/cloud storage
- [x] Backend: `LocalFileStorageProvider` successfully saves/retrieves images
- [x] Backend: `IImageService` provides high-level image operations
- [x] Backend: Path traversal attacks prevented in storage provider
- [x] Backend: File type validation enforced (JPEG, PNG only, configurable via appsettings)
- [x] Backend: File size limits enforced (5MB default, configurable via MaxFileSizeMb in appsettings)
- [x] Backend: Image upload endpoint accepts multipart/form-data
- [x] Backend: Image upload with imageUrl field clears imageUrl and uses file upload instead
- [x] Backend: imageUrl field downloads and saves image if no file provided
- [x] Backend: Image retrieval endpoint serves image with correct Content-Type
- [x] Backend: Image delete endpoint removes file and removes CoverImage from entity
- [x] Backend: Deleting entity automatically deletes associated image file (via file cleanup)
- [x] Backend: All image operations handle errors gracefully
- [x] Backend: Configuration supports local filesystem storage
- [x] Backend: ImageService unit tests pass with >80% coverage
- [x] Backend: StorageProvider tests validate path security
- [x] Backend: API endpoint tests validate request/response formats
- [x] Frontend: `ImageUpload` component renders file input and drag-drop zone
- [x] Frontend: `ImageDisplay` component renders image or placeholder
- [x] Frontend: File size validation on client before upload
- [x] Frontend: File type validation on client before upload
- [x] Frontend: Image preview shown before upload submission
- [x] Frontend: Upload progress shown during upload
- [x] Frontend: Upload errors displayed inline with user-friendly messages
- [x] Frontend: `uploadImage()` data function sends request to API
- [x] Frontend: `deleteImage()` data function sends delete request
- [x] Frontend: Entity add form includes image upload section
- [x] Frontend: Entity edit form includes image upload with replace/delete options
- [x] Frontend: Entity detail page displays cover image prominently
- [x] Frontend: Entity detail page shows placeholder if no image
- [x] Frontend: Entity list/grid views display thumbnail images
- [x] Frontend: Images have alt text describing media item
- [x] Frontend: Image components are responsive and accessible
- [x] Frontend: Component tests pass for ImageUpload and ImageDisplay
- [x] Frontend: Route tests pass for add/edit with image upload
- [x] Frontend: Data function tests pass for image operations
- [x] Integration: Book add form → upload image → displays on detail page
- [x] Integration: Movie edit form → change image → displays on detail page
- [x] Integration: Delete entity → image file removed from storage
- [x] Integration: Upload image via URL → downloads and displays
- [x] Integration: Barcode lookup returns image URL → user can save image by submitting form with URL
- [x] Integration: Invalid file type rejected with error message
- [x] Integration: File too large rejected with error message
- [x] Integration: API timeout on URL download handled gracefully
- [x] Documentation: API endpoints documented with request/response examples
- [x] Documentation: Image upload instructions included in README
- [x] Documentation: File restrictions documented (types, size)
- [x] Documentation: Configuration options documented
- [x] Documentation: Setup guide for local filesystem and cloud storage

## Risks and Mitigations

| Risk | Impact | Likelihood | Mitigation |
|------|--------|------------|------------|
| Image files consume disk space rapidly | High | Medium | Implement image size limits and cleanup policies; warn user about storage; consider cloud storage in production |
| Corrupted image files cause display errors | Medium | Low | Validate image files on upload; use try-catch in display components; show placeholder on error |
| Path traversal attack using malicious filenames | High | Low | Sanitize filenames; use UUID-based naming; validate paths before file operations; unit tests for path validation |
| Users upload inappropriate images | Medium | Low | Content moderation could be added later; for now, rely on user responsibility; log uploads for audit |
| Image retrieval becomes bottleneck under load | Medium | Medium | Implement HTTP caching (Cache-Control headers); use CDN in production; consider image resizing for thumbnails |
| Database imageId foreign key references dangling files | Medium | Low | Transaction-like semantics: save file first, then update DB; cleanup orphaned files on startup; integrity checks |
| Cloud storage integration prevents MVP delivery | High | Medium | Start with local filesystem only; make storage provider pluggable; cloud storage as phase 2 enhancement |
| Image metadata conflicts with entity metadata | Low | Low | Image is separate entity; entity.imageId references image.id; clear separation of concerns |
| Users expect instant image transformation/filtering | Low | Medium | Out of scope for MVP; document as future enhancement; consider CDN filters or thumbnail service later |
| External image URLs become unavailable | Low | Medium | Downloaded images are persisted locally; URL reference is metadata only; design allows re-downloading if needed |
| Backup/restore procedures don't include images | High | Medium | Document that image files must be included in backups; same volume mounting as database; clear backup instructions |

## Open Questions

1. **Image upload timing with entity operations**
   - **Decision Made**: Image upload is part of entity POST/PUT/PATCH operations
   - User provides image file or URL when adding/editing entity
   - Entity endpoint handles both image save and entity persistence in single atomic operation
   - Simple, synchronous flow with clear feedback to user

2. **Should barcode lookup automatically save images or return URL to UI?**
   - **Decision Made**: Return image URL to UI via imageUrl field
   - Barcode lookup returns `imageUrl` in response
   - User can save the image by submitting the entity form with the imageUrl field populated
   - Backend will download and save the image if URL is valid and image is valid format
   - User retains full control over which images are saved

3. **What image formats should be supported?**
   - **Decision Made**: JPEG and PNG for MVP
   - Configured via `AllowedMimeTypes` in appsettings.json
   - Can add additional formats (GIF, WebP, AVIF, etc.) without code changes
   - Just update appsettings configuration to enable new formats

4. **Should we implement image resizing/thumbnails?**
   - **Decision Made**: Not for MVP, add to backlog for future consideration
   - Could store multiple sizes: original, thumbnail, medium
   - Simplifies list view rendering
   - Increases storage requirements
   - **Future Enhancement**: Consider CDN-based resizing or ImageKit/Imgix integration

5. **What should be the max file size?**
   - **Decision Made**: 5MB for MVP
   - Configured via `MaxFileSizeMb` in appsettings.json (user-friendly megabytes, not bytes)
   - Easy to adjust without code changes
   - Validation on both frontend and backend

6. **Should we support image metadata (EXIF data)?**
   - **Decision Made**: Strip EXIF data on file save
   - Privacy protection - removes camera, lens, GPS, date taken metadata
   - Not relevant for book/movie/game/music covers
   - Implemented during `SaveImageAsync` before persisting file

7. **Should image be required or optional?**
   - **Decision Made**: Image is optional
   - Entity can exist without image
   - Fallback to placeholder on frontend when image missing
   - User can add/remove image anytime through edit form

8. **How should we handle image updates?**
   - **Decision Made**: Delete old image file when new image is uploaded
   - When uploading new image for entity with existing image, old file is removed
   - Prevents storage bloat from accumulated old images
   - No audit trail of previous images needed for MVP

9. **Should we support image cropping/editing?**
   - **Decision Made**: Not supported
   - Out of scope for MVP
   - Users can crop/edit images externally before upload if needed
   - Can be added as future enhancement if requested

10. **Should images be searchable by visual similarity?**
    - Could implement reverse image search or visual indexing
    - Complex feature, likely not needed
    - **Recommendation**: Not for MVP

## Dependencies

### External Libraries/Packages

**Backend (.NET 9.0)**
- `SixLabors.ImageSharp` - for EXIF data stripping on image uploads (required)
- Could add in future: `SkiaSharp` for image resizing
- Could add in future: advanced image processing libraries

**Frontend (TypeScript/React)**
- No new npm packages required for MVP
- Could add: `react-dropzone` for improved drag-and-drop UX
- Could add: `react-image-lightbox` for image modal viewing

### Third-Party Integrations

**None required for MVP**

**Future enhancements:**
- AWS S3 for cloud storage
- Azure Blob Storage for cloud storage
- Cloudflare Image Optimization for resizing
- ImageKit or Imgix for advanced image handling

### Infrastructure Changes

**File System**
- Create `/app/data/images` directory (or configurable path)
- Ensure write permissions for application process
- Include in container volume mounts for persistence
- Include in backup procedures

**Docker Compose** (updates)
- Add volume for image storage:
  ```yaml
  volumes:
    - ./data/images:/app/data/images
  ```
- Ensure volume persists across container restarts

**Environment Variables** (optional)
- `IMAGE_STORAGE_PROVIDER`: "LocalFile" or "S3" (for future)
- `IMAGE_MAX_SIZE_BYTES`: file size limit
- `IMAGE_STORAGE_PATH`: path to store images (for LocalFile provider)

## References

### Similar Implementations

**Existing file handling in MediaSet:**
- CSV import uses multipart form data parsing
- Could follow similar pattern for image upload

**Best practices for image storage:**
- OWASP guidelines for file upload security
- NIST recommendations for file validation

### Documentation

**Image security considerations:**
- https://owasp.org/www-community/vulnerabilities/Unrestricted_File_Upload
- https://cheatsheetseries.owasp.org/cheatsheets/File_Upload_Cheat_Sheet.html

**HTTP caching for images:**
- https://developer.mozilla.org/en-US/docs/Web/HTTP/Caching
- https://web.dev/http-caching/

**MongoDB image storage patterns:**
- https://docs.mongodb.com/manual/core/gridfs/
- GridFS is alternative for storing large files in MongoDB (not used here; filesystem preferred)

### Related Issues/Code

**Current entity models:** `MediaSet.Api/Models/Book.cs`, etc.
**Current repository pattern:** `MediaSet.Api/Services/`
**Frontend entity components:** `MediaSet.Remix/app/routes/$entity_.add/`
**Existing CSV import:** Shows file upload and validation patterns

