# Barcode Lookup with Strategy Pattern Implementation Plan

## Overview

This feature enhances the existing lookup functionality by adding support for UPC/EAN barcode lookups for both books and movies. The implementation will refactor the current ISBN-only lookup endpoint to support multiple identifier types and entity types using the Strategy Pattern.

Currently, the lookup endpoint only supports book lookups via ISBN using OpenLibrary. This feature will:
- Add UPC/EAN barcode lookup support using UPCitemdb.com
- Extend movie metadata lookup using TMDB (The Movie Database)
- Implement a strategy pattern to route lookups to the appropriate service based on identifier type and entity type
- Update the API endpoint structure to: `GET /lookup/{entityType}/{identifierType}/{identifierValue}`

The value to users is the ability to quickly add movies by scanning barcodes (UPC/EAN), similar to the existing ISBN book lookup functionality.

## Related Existing Functionality

### Backend Components

**Existing Lookup API** (`MediaSet.Api/Lookup/LookupApi.cs`)
- Current endpoint: `GET /lookup/{identifierType}/{identifierValue}`
- Only supports books via OpenLibrary
- Uses `IOpenLibraryClient` directly in the endpoint handler
- Returns `BookResponse` model

**OpenLibrary Client** (`MediaSet.Api/Clients/OpenLibraryClient.cs`, `MediaSet.Api/Clients/IOpenLibraryClient.cs`)
- HTTP client for OpenLibrary API
- Methods: `GetReadableBookAsync`, `GetBookByIsbnAsync`, etc.
- Returns `BookResponse` with book metadata
- Uses structured response mapping from OpenLibrary's Read API

**IdentifierType Enum** (`MediaSet.Api/Models/IdentifierType.cs`)
- Current types: ISBN, LCCN, OCLC, OLID
- Needs extension for UPC/EAN
- Helper: `IdentifierTypeExtensions.TryParseIdentifierType`

**Entity Models** (`MediaSet.Api/Models/Book.cs`, `MediaSet.Api/Models/Movie.cs`)
- Book: Has `ISBN` property
- Movie: Has `Barcode` property
- Both implement `IEntity` interface

### Frontend Components

**Add/Edit Entity Routes** (`MediaSet.Remix/app/routes/$entity_.add/route.tsx`, `MediaSet.Remix/app/routes/$entity_.$id.edit/route.tsx`)
- Books: Has ISBN lookup section that prefills form with lookup results
- Uses intent-based form submission (lookup vs. create/update)
- Pattern should be extended to movies with barcode field

**Lookup Data Function** (`MediaSet.Remix/app/lookup-data.ts`)
- `lookup(entityType: Entity, barcode: string)` function
- Currently hardcoded to `/lookup/isbn/${barcode}`
- Returns `BookEntity | LookupError`

**TypeScript Models** (`MediaSet.Remix/app/models.ts`)
- `BookEntity` and `MovieEntity` interfaces
- `Entity` enum with Books, Movies, Games, Musics

### Infrastructure/Configuration

**Dependency Injection** (`MediaSet.Api/Program.cs`)
- OpenLibrary client registered with HttpClient factory
- Configuration from `OpenLibraryConfiguration`

**Database Schema**
- Books collection with ISBN field
- Movies collection with Barcode field

**Third-Party Integrations**
- OpenLibrary API (existing) - for book metadata
- UPCitemdb.com (new) - for UPC/EAN to product identification
- TMDB (new) - for movie metadata

## Requirements

### Functional Requirements

1. **API Endpoint Structure**
   - Update endpoint to `GET /lookup/{entityType}/{identifierType}/{identifierValue}`
   - Support `entityType`: books, movies
   - Support `identifierType`: isbn, lccn, oclc, olid, upc, ean

2. **UPC/EAN Lookup**
   - Query UPCitemdb.com to identify product type and title
   - For movies, extract title and use TMDB for full metadata
   - For books, extract ISBN and use OpenLibrary for full metadata

3. **TMDB Integration**
   - Search movies by title from UPC lookup
   - Retrieve comprehensive movie metadata (genres, studios, rating, runtime, release date, plot)
   - Map TMDB response to `MovieResponse` model

4. **Strategy Pattern Implementation**
   - Create `ILookupStrategy` interface
   - Implement `BookLookupStrategy` for books (ISBN, LCCN, OCLC, OLID, UPC/EAN)
   - Implement `MovieLookupStrategy` for movies (UPC/EAN)
   - Create `LookupStrategyFactory` to select appropriate strategy

5. **Frontend Updates**
   - Remove separate lookup routes (`/books/lookup`, `/movies/lookup`)
   - Add lookup button on ISBN field in book add/edit forms
   - Add lookup button on barcode field in movie add/edit forms
   - Lookup button fetches metadata and populates form fields
   - Update data functions to pass entity type and identifier type to API

### Non-Functional Requirements

- **Performance**: API responses within 3 seconds for external lookups
- **Caching**: Consider caching UPC/TMDB lookups to reduce API calls
- **Error Handling**: Clear error messages for invalid barcodes, API failures, and rate limits
- **API Rate Limits**: 
  - UPCitemdb.com: 100 requests/day (free tier)
  - TMDB: 40 requests/10 seconds (standard)
- **Security**: Store API keys in configuration/environment variables
- **Accessibility**: Maintain existing accessibility standards in UI

## Proposed Changes

### Backend Changes (MediaSet.Api)

#### New/Modified Models

**IdentifierType.cs** (`MediaSet.Api/Models/IdentifierType.cs`)
- Add new enum values:
  ```csharp
  [Description("upc")]
  Upc,
  
  [Description("ean")]
  Ean
  ```
- Update `IdentifierTypeExtensions.TryParseIdentifierType` if needed

**New: LookupResponse.cs** (`MediaSet.Api/Models/LookupResponse.cs`)
- Create base response type that can represent both books and movies:
  ```csharp
  public abstract record LookupResponse(MediaTypes EntityType);
  public record BookLookupResponse(...) : LookupResponse;
  public record MovieLookupResponse(...) : LookupResponse;
  ```
- Or use discriminated union pattern with `OneOf` library

**New: UpcItemResponse.cs** (`MediaSet.Api/Models/UpcItemResponse.cs`)
- Model for UPCitemdb.com API response
- Properties: `ean`, `title`, `description`, `category`, `brand`, etc.

**New: TmdbMovieResponse.cs** (`MediaSet.Api/Models/TmdbMovieResponse.cs`)
- Model for TMDB API response
- Properties: `id`, `title`, `overview`, `release_date`, `genres`, `runtime`, `production_companies`, etc.

**New: MovieResponse.cs** (`MediaSet.Api/Models/MovieResponse.cs`)
- Similar to existing `BookResponse`
- Properties: title, genres, studios, releaseDate, rating, runtime, plot
  ```csharp
  public record MovieResponse(
    string Title,
    List<string> Genres,
    List<string> Studios,
    string ReleaseDate,
    string Rating,
    int? Runtime,
    string Plot
  );
  ```

#### New/Modified Services

**New: ILookupStrategy.cs** (`MediaSet.Api/Services/ILookupStrategy.cs`)
- Interface for lookup strategies using generics:
  ```csharp
  public interface ILookupStrategy<TResponse> where TResponse : class
  {
    Task<TResponse?> LookupAsync(IdentifierType identifierType, string identifierValue, CancellationToken cancellationToken);
    bool CanHandle(MediaTypes entityType, IdentifierType identifierType);
  }
  ```

**New: BookLookupStrategy.cs** (`MediaSet.Api/Services/BookLookupStrategy.cs`)
- Implements `ILookupStrategy<BookResponse>`
- Dependencies: `IOpenLibraryClient`, `IUpcItemDbClient`
- Methods:
  - `CanHandle(MediaTypes.Books, [ISBN, LCCN, OCLC, OLID, UPC, EAN])`
  - `LookupAsync()`: Route to OpenLibrary for ISBN/LCCN/OCLC/OLID, or UPC lookup → ISBN → OpenLibrary
  - Returns `Task<BookResponse?>`
- Business logic:
  1. If ISBN/LCCN/OCLC/OLID: delegate to OpenLibraryClient
  2. If UPC/EAN: query UPCitemdb, extract ISBN or title, then query OpenLibrary

**New: MovieLookupStrategy.cs** (`MediaSet.Api/Services/MovieLookupStrategy.cs`)
- Implements `ILookupStrategy<MovieResponse>`
- Dependencies: `IUpcItemDbClient`, `ITmdbClient`
- Methods:
  - `CanHandle(MediaTypes.Movies, [UPC, EAN])`
  - `LookupAsync()`: UPC lookup → title → TMDB search → full metadata
  - Returns `Task<MovieResponse?>`
- Business logic:
  1. Query UPCitemdb with UPC/EAN
  2. Extract title/description
  3. Search TMDB for matching movie
  4. Retrieve full movie details
  5. Map to `MovieResponse`

**New: LookupStrategyFactory.cs** (`MediaSet.Api/Services/LookupStrategyFactory.cs`)
- Factory to select appropriate strategy
- Dependencies: `IServiceProvider` (to resolve strategy instances dynamically)
- Methods:
  - `ILookupStrategy<BookResponse> GetBookStrategy(IdentifierType identifierType)`
  - `ILookupStrategy<MovieResponse> GetMovieStrategy(IdentifierType identifierType)`
- Returns appropriate typed strategy or throws if none found
- Alternative approach: Use base interface without generics for CanHandle, then cast to specific generic type

#### New/Modified Clients

**New: IUpcItemDbClient.cs** (`MediaSet.Api/Clients/IUpcItemDbClient.cs`)
- Interface for UPCitemdb.com API
- Method: `Task<UpcItemResponse?> GetItemByCodeAsync(string code, CancellationToken cancellationToken)`

**New: UpcItemDbClient.cs** (`MediaSet.Api/Clients/UpcItemDbClient.cs`)
- HTTP client for UPCitemdb.com API
- Configuration: `UpcItemDbConfiguration` (base URL, API key, timeout)
- Endpoint: `https://api.upcitemdb.com/prod/trial/lookup?upc={code}`
- Error handling for rate limits and invalid codes

**New: ITmdbClient.cs** (`MediaSet.Api/Clients/ITmdbClient.cs`)
- Interface for TMDB API
- Methods:
  - `Task<TmdbSearchResponse?> SearchMovieAsync(string title, CancellationToken cancellationToken)`
  - `Task<TmdbMovieResponse?> GetMovieDetailsAsync(int movieId, CancellationToken cancellationToken)`

**New: TmdbClient.cs** (`MediaSet.Api/Clients/TmdbClient.cs`)
- HTTP client for TMDB API
- Configuration: `TmdbConfiguration` (base URL, API key/bearer token, timeout)
- Endpoints:
  - Search: `https://api.themoviedb.org/3/search/movie?query={title}`
  - Details: `https://api.themoviedb.org/3/movie/{id}`
- Response mapping to `MovieResponse`

#### New/Modified API Endpoints

**LookupApi.cs** (`MediaSet.Api/Lookup/LookupApi.cs`)

**Endpoint**: `GET /lookup/{entityType}/{identifierType}/{identifierValue}`
- Purpose: Lookup media metadata by identifier
- Parameters:
  - `entityType`: books | movies (string, case-insensitive)
  - `identifierType`: isbn | lccn | oclc | olid | upc | ean (string, case-insensitive)
  - `identifierValue`: the identifier value (string)
- Request model: None (route parameters)
- Response models: 
  - 200 OK: `BookResponse` or `MovieResponse` (polymorphic based on entityType)
  - 400 Bad Request: Invalid entityType or identifierType
  - 404 Not Found: No results found
  - 500 Internal Server Error: API failure
- Authorization: None required
- Dependencies: `LookupStrategyFactory`, `ILogger`
- Implementation:
  1. Parse and validate entityType (Books/Movies)
  2. Parse and validate identifierType
  3. Get typed strategy from factory based on entity type
  4. Execute lookup with strongly-typed return
  5. Return typed result

**Note**: The old endpoint `/lookup/{identifierType}/{identifierValue}` will be **removed** (breaking change). All clients must update to use the new three-parameter endpoint.

#### Database Changes

- No database schema changes required
- Books already have ISBN field
- Movies already have Barcode field

#### Configuration Changes

**appsettings.json** updates:
```json
{
  "UpcItemDb": {
    "BaseUrl": "https://api.upcitemdb.com/",
    "Timeout": 10
  },
  "Tmdb": {
    "BaseUrl": "https://api.themoviedb.org/3/",
    "BearerToken": "",
    "Timeout": 10
  }
}
```

**Program.cs** updates:
- Register new HTTP clients: `UpcItemDbClient`, `TmdbClient`
- Register strategies: `BookLookupStrategy`, `MovieLookupStrategy`
- Register factory: `LookupStrategyFactory`
- Bind configuration sections

### Frontend Changes (MediaSet.Remix)

#### Removed Routes

**Route**: `/books/lookup` and `/movies/lookup` (to be removed)
- These standalone lookup routes will be removed
- Lookup functionality will be integrated directly into add/edit forms

#### Modified Routes

**Route**: `/books/add` and `/books/:id/edit` (existing, updates)
- Purpose: Add/edit book with inline lookup
- Updates:
  - Remove separate "ISBN Lookup Section" above the form
  - Add "Lookup" button next to ISBN input field within the form
  - Button triggers lookup and populates form fields with results
  - User can edit populated fields before saving
  - Action handler: distinguish between "lookup" intent and "save" intent

**Route**: `/movies/add` and `/movies/:id/edit` (existing, updates)
- Purpose: Add/edit movie with inline lookup
- Updates:
  - Add "Lookup" button next to Barcode input field within the form
  - Button triggers UPC/EAN lookup and populates form fields with results
  - User can edit populated fields before saving
  - Action handler: distinguish between "lookup" intent and "save" intent
  - Follow same pattern as books

#### New/Modified Components

**Component**: `FieldWithLookup` (`MediaSet.Remix/app/components/FieldWithLookup.tsx`)
- Props: `{ label: string, name: string, value: string, onChange: (value: string) => void, onLookup: () => void, isLookingUp: boolean, entityType: Entity, identifierType: string, error?: string }`
- Reusable input field with integrated lookup button
- Layout: Input field with lookup button adjacent (not as separate section)
- Button displays loading state during lookup
- Displays error messages inline
- Used for ISBN field in books, Barcode field in movies

**Alternative simpler approach**: Add lookup button directly in existing form components without new abstraction

#### New/Modified Data Functions

**lookup-data.ts** (`MediaSet.Remix/app/lookup-data.ts`)

**Function**: `lookup(entityType: Entity, identifierType: string, identifierValue: string)`
- Updates:
  - Add `identifierType` parameter (isbn, lccn, oclc, olid, upc, ean)
  - Update API call to new endpoint structure: `/lookup/{entityType}/{identifierType}/{identifierValue}`
  - Return `BookEntity | MovieEntity | LookupError` (discriminated union)
- Type guards for response type checking
- Error handling:
  - 400: Invalid parameters
  - 404: Not found
  - 500: Server error
- Called from add/edit route actions when intent is "lookup"

**Helper function**: `getIdentifierTypeForField(entityType: Entity, fieldName: string): string`
- Returns appropriate identifier type based on entity and field
- Books + "isbn" field → "isbn"
- Movies + "barcode" field → "upc" (default for movies)
- Used to determine which identifier type to use for lookup

#### Type Definitions

**models.ts** (`MediaSet.Remix/app/models.ts`)

Add new types:
```typescript
export type IdentifierType = 'isbn' | 'lccn' | 'oclc' | 'olid' | 'upc' | 'ean';

export type LookupResult<T extends BaseEntity> = T | LookupError;

export type MovieLookupResponse = {
  title: string;
  genres: string[];
  studios: string[];
  releaseDate: string;
  rating: string;
  runtime: number;
  plot: string;
};
```

### Testing Changes

#### Backend Tests (MediaSet.Api.Tests)

**Test Class**: `BookLookupStrategyTests` (`MediaSet.Api.Tests/Services/BookLookupStrategyTests.cs`)
- Test scenarios:
  - `LookupAsync_WithIsbn_ReturnsBookResponse`
  - `LookupAsync_WithUpc_ReturnsBookResponse`
  - `LookupAsync_WithInvalidIsbn_ReturnsNull`
  - `CanHandle_WithBooksAndIsbn_ReturnsTrue`
  - `CanHandle_WithMoviesAndIsbn_ReturnsFalse`
  - `CanHandle_WithBooksAndUpc_ReturnsTrue`
- Mock dependencies: `IOpenLibraryClient`, `IUpcItemDbClient`
- Edge cases: 
  - UPC lookup returns non-book item
  - UPC lookup returns no ISBN
  - OpenLibrary fails after UPC success

**Test Class**: `MovieLookupStrategyTests` (`MediaSet.Api.Tests/Services/MovieLookupStrategyTests.cs`)
- Test scenarios:
  - `LookupAsync_WithUpc_ReturnsMovieResponse`
  - `LookupAsync_WithEan_ReturnsMovieResponse`
  - `LookupAsync_WithInvalidUpc_ReturnsNull`
  - `LookupAsync_WithNoTmdbResults_ReturnsNull`
  - `CanHandle_WithMoviesAndUpc_ReturnsTrue`
  - `CanHandle_WithBooksAndUpc_ReturnsFalse`
- Mock dependencies: `IUpcItemDbClient`, `ITmdbClient`
- Edge cases:
  - UPC lookup returns non-movie item
  - Multiple TMDB matches (select best match)
  - TMDB API rate limit

**Test Class**: `LookupStrategyFactoryTests` (`MediaSet.Api.Tests/Services/LookupStrategyFactoryTests.cs`)
- Test scenarios:
  - `GetStrategy_WithBooksAndIsbn_ReturnsBookStrategy`
  - `GetStrategy_WithMoviesAndUpc_ReturnsMovieStrategy`
  - `GetStrategy_WithInvalidEntityType_ThrowsException`
  - `GetStrategy_WithUnsupportedCombination_ThrowsException`

**Test Class**: `UpcItemDbClientTests` (`MediaSet.Api.Tests/Clients/UpcItemDbClientTests.cs`)
- Test scenarios:
  - `GetItemByCodeAsync_WithValidUpc_ReturnsResponse`
  - `GetItemByCodeAsync_WithInvalidUpc_ReturnsNull`
  - `GetItemByCodeAsync_WithRateLimitError_ThrowsException`
  - HTTP error handling (429, 500, etc.)

**Test Class**: `TmdbClientTests` (`MediaSet.Api.Tests/Clients/TmdbClientTests.cs`)
- Test scenarios:
  - `SearchMovieAsync_WithTitle_ReturnsResults`
  - `GetMovieDetailsAsync_WithId_ReturnsDetails`
  - `SearchMovieAsync_WithNoResults_ReturnsEmptyList`
  - HTTP error handling

**Test Class**: `LookupApiTests` (existing, updates) (`MediaSet.Api.Tests/Lookup/LookupApiTests.cs`)
- Updated test scenarios:
  - `GetLookup_WithBooksAndIsbn_ReturnsBook`
  - `GetLookup_WithMoviesAndUpc_ReturnsMovie`
  - `GetLookup_WithInvalidEntityType_ReturnsBadRequest`
  - `GetLookup_WithInvalidIdentifierType_ReturnsBadRequest`
  - `GetLookup_WithUnsupportedCombination_ReturnsBadRequest`

#### Frontend Tests (MediaSet.Remix)

**Test File**: `lookup-data.test.ts` (`MediaSet.Remix/app/lookup-data.test.ts`)
- Component interaction tests:
  - `lookup returns BookEntity for valid ISBN`
  - `lookup returns MovieEntity for valid UPC`
  - `lookup returns error for invalid barcode`
  - `lookup constructs correct API URL with entity type and identifier type`

**Test File**: `FieldWithLookup.test.tsx` (`MediaSet.Remix/app/components/FieldWithLookup.test.tsx`)
- Component tests:
  - Renders input field with lookup button
  - Handles user input
  - Triggers onLookup callback when button clicked
  - Displays loading state on button
  - Disables button during lookup
  - Displays error messages inline

**Test File**: `$entity_.add.route.test.tsx` (updates) (`MediaSet.Remix/app/routes/$entity_.add/route.test.tsx`)
- User flow tests:
  - Book add with ISBN lookup populates form
  - Movie add with barcode lookup populates form
  - Lookup error displays inline
  - User can edit fields after lookup
  - Form submission saves entity (not lookup)

**Test File**: `$entity_.$id.edit.route.test.tsx` (updates) (`MediaSet.Remix/app/routes/$entity_.$id.edit/route.test.tsx`)
- User flow tests:
  - Book edit with ISBN lookup updates form fields
  - Movie edit with barcode lookup updates form fields
  - Existing data preserved if lookup fails

#### Integration Tests

**End-to-end scenarios** (manual or using testing framework):
1. Book ISBN lookup → displays book data
2. Book UPC lookup → resolves to ISBN → displays book data
3. Movie UPC lookup → resolves to title → TMDB search → displays movie data
4. Invalid barcode → displays error
5. API timeout → displays error message

**API contract validation**:
- Validate UPCitemdb.com API response structure
- Validate TMDB API response structure
- Ensure response mapping doesn't lose data
- Test with real API keys in integration environment (not unit tests)

## Implementation Steps

1. **Add UPC/EAN to IdentifierType enum**
   - Update `MediaSet.Api/Models/IdentifierType.cs`
   - Add enum values and descriptions
   - Test enum parsing

2. **Create strategy pattern interfaces and base classes**
   - Create `ILookupStrategy<TResponse>` generic interface
   - Create `LookupStrategyFactory` class with typed methods
   - Add unit tests for factory

3. **Implement UPCitemdb.com client**
   - Create configuration model
   - Create `IUpcItemDbClient` interface and `UpcItemDbClient` implementation
   - Add HTTP client registration in `Program.cs`
   - Write unit tests with mocked HTTP responses
   - Test with real API (manual/integration test)

4. **Implement TMDB client**
   - Create configuration model
   - Create `ITmdbClient` interface and `TmdbClient` implementation
   - Add HTTP client registration in `Program.cs`
   - Write unit tests with mocked HTTP responses
   - Test with real API (manual/integration test)

5. **Implement BookLookupStrategy**
   - Create strategy class with dependencies
   - Implement `CanHandle` and `LookupAsync` methods
   - Handle ISBN direct lookup
   - Handle UPC → ISBN → OpenLibrary flow
   - Write comprehensive unit tests
   - Register in DI container

6. **Implement MovieLookupStrategy**
   - Create strategy class with dependencies
   - Implement `CanHandle` and `LookupAsync` methods
   - Handle UPC → TMDB flow
   - Write comprehensive unit tests
   - Register in DI container

7. **Update LookupApi endpoint**
   - Modify endpoint signature to include entityType
   - Add entity type validation
   - Replace direct OpenLibraryClient usage with strategy factory
   - Use generic strategy methods for type-safe responses
   - **Remove old endpoint** (breaking change)
   - Update Swagger documentation
   - Update all API consumers to use new endpoint structure

8. **Update backend configuration**
   - Add UpcItemDb and Tmdb configuration sections to `appsettings.json`
   - Document required API keys in README
   - Add environment variable support

9. **Create/update backend tests**
   - Write all strategy tests
   - Write all client tests
   - Update LookupApi tests
   - Ensure good code coverage (>80%)

10. **Update frontend TypeScript models**
    - Add `IdentifierType` type
    - Add `MovieLookupResponse` type
    - Update `LookupError` if needed

11. **Update lookup data function**
    - Add `identifierType` parameter
    - Update API endpoint construction
    - Add type guards for response handling
    - Update error handling
    - Add helper function for identifier type mapping

12. **Remove standalone lookup routes**
    - Delete `/books/lookup` route
    - Delete `/movies/lookup` route (if exists)
    - Update any navigation links that pointed to these routes

13. **Create FieldWithLookup component (or integrate inline)**
    - Implement input field with adjacent lookup button
    - Add prop types
    - Handle loading state
    - Write component tests

14. **Update book add/edit routes**
    - Remove separate "ISBN Lookup Section" above form
    - Add lookup button next to ISBN field within form
    - Update action handler to support "lookup" intent
    - Populate form fields with lookup results
    - Test form prefill and edit functionality

15. **Update movie add/edit routes**
    - Add lookup button next to Barcode field within form
    - Update action handler to support "lookup" intent
    - Populate form fields with lookup results
    - Follow same pattern as books
    - Test form prefill and edit functionality

16. **Write frontend tests**
    - Component tests for FieldWithLookup
    - Route tests for add/edit with lookup
    - Data function tests
    - User flow tests for inline lookup

17. **Integration testing**
    - Test book ISBN lookup from add form end-to-end
    - Test book ISBN lookup from edit form end-to-end
    - Test movie UPC lookup from add form end-to-end
    - Test movie UPC lookup from edit form end-to-end
    - Test error scenarios (invalid codes, API failures)
    - Test that lookup results can be edited before saving

18. **Documentation updates**
    - Update README with new lookup capabilities
    - Document API endpoint changes
    - Add notes about required API keys
    - Update DEVELOPMENT.md with setup instructions
    - Note removal of standalone lookup routes

## Acceptance Criteria

- [x] Backend: IdentifierType enum includes UPC and EAN values
- [x] Backend: Strategy pattern implemented with generic ILookupStrategy<TResponse> interface
- [x] Backend: UpcItemDbClient successfully queries UPCitemdb.com API
- [x] Backend: TmdbClient successfully queries TMDB API
- [x] Backend: BookLookupStrategy handles ISBN, LCCN, OCLC, OLID, UPC, EAN lookups
- [x] Backend: MovieLookupStrategy handles UPC/EAN lookups
- [x] Backend: Lookup API endpoint follows pattern `/lookup/{entityType}/{identifierType}/{identifierValue}`
- [x] Backend: API returns strongly-typed response (BookResponse or MovieResponse) using generics
- [x] Backend: Old endpoint `/lookup/{identifierType}/{identifierValue}` is removed
- [x] Backend: All unit tests pass with >80% coverage
- [x] Frontend: TypeScript models include new types
- [x] Frontend: Lookup data function supports entity type and identifier type parameters
- [x] Frontend: FieldWithLookup component (or inline implementation) handles lookup button
- [x] Frontend: Standalone `/books/lookup` and `/movies/lookup` routes removed
- [x] Frontend: Book add/edit forms have lookup button on ISBN field
- [x] Frontend: Movie add/edit forms have lookup button on Barcode field
- [x] Frontend: Lookup populates form fields that can be edited before saving
- [x] Frontend: All component tests pass
- [x] Integration: Book ISBN lookup from add/edit form works end-to-end
- [x] Integration: Book UPC lookup resolves to book metadata
- [x] Integration: Movie UPC lookup from add/edit form resolves to movie metadata via TMDB
- [x] Integration: Lookup results populate form fields correctly
- [x] Integration: User can edit lookup results before saving
- [x] Integration: Error states display inline on form
- [x] Documentation: README updated with new features and API key requirements
- [x] Configuration: API keys can be set via appsettings.json or environment variables

## Risks and Mitigations

| Risk | Impact | Likelihood | Mitigation |
|------|--------|------------|------------|
| UPCitemdb.com rate limits (100 req/day free tier) | High | High | Implement caching; consider paid tier ($20/mo for unlimited); use UPC database lookup sparingly with clear user messaging |
| TMDB rate limits (40 req/10 sec) | Medium | Low | Implement rate limiting on our side; cache results; use exponential backoff |
| UPC lookup returns ambiguous results | Medium | Medium | Implement fuzzy matching logic; allow user to select from multiple results; prefer exact title matches |
| External API downtime (UPCitemdb or TMDB) | Medium | Low | Implement timeout handling; graceful degradation; clear error messages; consider fallback services |
| UPC/EAN codes might not be in database | High | High | Clear messaging that not all barcodes are in database; provide manual entry option; suggest alternative identifiers |
| TMDB search returns wrong movie | Medium | Medium | Use release year from UPC data if available; implement confidence scoring; show top 3 matches for user selection |
| API key exposure in configuration | High | Low | Use environment variables; .gitignore config files with keys; document secure configuration |
| Breaking changes to existing ISBN lookup | High | High | Comprehensive testing; update all frontend clients to use new endpoint; clear migration documentation; update in single PR |
| Performance degradation with multiple API calls | Medium | Medium | Implement parallel requests where possible; cache aggressively; monitor response times |
| UPC lookup returns book when expecting movie (or vice versa) | Medium | Medium | Validate product category from UPC response; route to correct strategy; display category mismatch warning to user |

## Open Questions

1. **Should we support user selection when multiple TMDB results are found?**
   - Option A: Auto-select best match (simpler UX)
   - Option B: Show top 3 results for user to choose (more accurate)
   - **Recommendation**: Start with auto-select, add multi-select in future iteration

2. **What should happen when a UPC lookup returns the wrong entity type?**
   - Example: User scans UPC in movies/lookup but UPCitemdb identifies it as a book
   - Option A: Return error and suggest books/lookup
   - Option B: Automatically route to correct entity type and return result
   - **Recommendation**: Return error with helpful message and link to correct lookup page

3. **Should we implement caching for UPC/TMDB lookups?**
   - Given rate limits, caching would be valuable
   - Question: Cache duration? Storage mechanism (memory, Redis, database)?
   - **Recommendation**: Start with in-memory cache (use existing CacheService), 24-hour TTL, evaluate for Redis if needed

4. ~~**Do we need backward compatibility for the old `/lookup/{identifierType}/{identifierValue}` endpoint?**~~
   - **RESOLVED**: Breaking change accepted. Old endpoint will be removed.
   - All frontend callers will be updated in the same PR to use new endpoint structure
   - This simplifies the API and avoids maintaining two endpoint patterns

5. **Should we support EAN-13 to UPC-A conversion automatically?**
   - EAN-13 codes starting with 0 are equivalent to UPC-A (remove leading 0)
   - Most APIs accept both, but some normalization might help
   - **Recommendation**: Handle both formats, normalize if needed for specific APIs

6. **What level of movie metadata should we retrieve from TMDB?**
   - Basic: title, year, genres
   - Standard: + plot, runtime, rating, studios
   - Extended: + cast, crew, images, videos
   - **Recommendation**: Standard for now (matches Movie entity model), extended can be future enhancement

7. **Should we allow multiple identifier types in a single lookup?**
   - Example: Pass both UPC and ISBN if known
   - Benefit: Fallback if primary lookup fails
   - Complexity: Increased endpoint/UI complexity
   - **Recommendation**: Single identifier per request for MVP, consider multi-lookup in future

8. **How should we handle UPC lookups that return multiple ISBNs?**
   - Some products (box sets, special editions) might have multiple ISBNs
   - Option A: Return first ISBN
   - Option B: Return all and let user choose
   - **Recommendation**: Return first ISBN for MVP, add multi-result UI in future

## Dependencies

### External Libraries/Packages

**Backend (.NET)**
- No new packages required (use existing HttpClient, System.Text.Json)
- Optional: Consider `OneOf` library for discriminated unions if needed

**Frontend (TypeScript/React)**
- No new packages required

### Third-Party API Integrations

**UPCitemdb.com**
- Free tier: 100 requests/day
- Paid tier: $20/month for unlimited requests
- API key required (sign up at https://www.upcitemdb.com/)
- Documentation: https://www.upcitemdb.com/api/

**TMDB (The Movie Database)**
- Free tier: 40 requests per 10 seconds, 1,000,000 requests per month
- API key/Bearer token required (sign up at https://www.themoviedb.org/signup)
- Documentation: https://developer.themoviedb.org/docs

**OpenLibrary (existing)**
- No API key required
- Rate limit: Unspecified, but recommended to be respectful
- Already integrated

### Infrastructure Changes

**Configuration Management**
- Add API keys to environment variables or secure configuration
- Update deployment documentation

**Caching (optional for MVP)**
- If implementing cache, consider Redis for production
- Development can use in-memory cache

## References

### Similar Implementations
- Existing OpenLibraryClient pattern can be followed for new clients
- Existing MetadataService uses similar factory pattern (though different use case)
- Strategy pattern examples: https://refactoring.guru/design-patterns/strategy/csharp/example

### API Documentation
- UPCitemdb.com API docs: https://www.upcitemdb.com/api/
- TMDB API docs: https://developer.themoviedb.org/docs/getting-started
- OpenLibrary API docs: https://openlibrary.org/dev/docs/api/

### Related Issues/Design Docs
- Current lookup implementation: `MediaSet.Api/Lookup/LookupApi.cs`
- Entity models: `MediaSet.Api/Models/Book.cs`, `MediaSet.Api/Models/Movie.cs`
- Frontend models: `MediaSet.Remix/app/models.ts`

### Code Style Guidelines
- Backend: [/.github/code-style-api.md](../.github/code-style-api.md)
- Frontend: [/.github/code-style-ui.md](../.github/code-style-ui.md)
