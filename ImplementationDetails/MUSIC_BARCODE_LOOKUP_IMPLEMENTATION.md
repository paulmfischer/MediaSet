# Music Barcode Lookup Implementation Plan

## Overview

This feature extends the existing barcode lookup functionality to support music albums/CDs. Similar to how movies use UPCitemdb for barcode identification and TMDB for metadata enrichment, music will use UPCitemdb to identify the product and a free music metadata API to fetch comprehensive album information.

The existing lookup system already supports books (ISBN/UPC via OpenLibrary), movies (UPC via TMDB), and games (UPC via GiantBomb), all using the Strategy Pattern. This implementation will add a fourth strategy for music that follows the same established patterns.

**What is being built?**
- Music barcode (UPC/EAN) lookup endpoint
- Integration with MusicBrainz API for music metadata
- `MusicLookupStrategy` implementing the existing `ILookupStrategy<MusicResponse>` interface
- Frontend lookup button on music add/edit forms (following the same pattern as books, movies, and games)

**Why is it needed?**
Users want to quickly add music albums to their collection by scanning barcodes, just like they can with books (ISBN), movies (UPC), and games (UPC).

**What value does it provide?**
- Rapid music cataloging via barcode scanning
- Automatic metadata population (title, artist, release date, genres, label, duration, track list)
- Consistent UX across all media types (books, movies, games, music)
- Reduced manual data entry errors

## Related Existing Functionality

### Backend Components

**Lookup System** (`MediaSet.Api/Lookup/LookupApi.cs`)
- Endpoint pattern: `GET /lookup/{entityType}/{identifierType}/{identifierValue}`
- Currently supports: Books (isbn, lccn, oclc, olid, upc, ean), Movies (upc, ean), Games (upc, ean)
- Uses `LookupStrategyFactory` to route to appropriate strategy
- Returns typed responses: `Results<Ok<BookResponse>, Ok<MovieResponse>, Ok<GameResponse>, NotFound, BadRequest<string>>`
- Need to add: Musics (upc, ean)
- Need to update: Return type to include `Ok<MusicResponse>`

**Strategy Pattern Implementation**
- `ILookupStrategy<TResponse>` interface (`MediaSet.Api/Services/ILookupStrategy.cs`)
  - `bool CanHandle(MediaTypes entityType, IdentifierType identifierType)`
  - `Task<TResponse?> LookupAsync(IdentifierType identifierType, string identifierValue, CancellationToken cancellationToken)`
- `BookLookupStrategy` - handles books with multiple identifier types
- `MovieLookupStrategy` - handles movies with UPC/EAN via UPCitemdb → TMDB
- `GameLookupStrategy` - handles games with UPC/EAN via UPCitemdb → GiantBomb
- `LookupStrategyFactory` - resolves strategies via DI

**UPC Lookup Client** (`MediaSet.Api/Clients/UpcItemDbClient.cs`)
- Already implemented and shared by MovieLookupStrategy and GameLookupStrategy
- Returns `UpcItemResponse` with title, category, brand, etc.
- Will be reused by MusicLookupStrategy
- No changes needed

**Music Model** (`MediaSet.Api/Models/Music.cs`)
- Properties: Title, Format, Artist, ReleaseDate, Genres, Duration, Label, Barcode, Tracks, Discs, DiscList
- Implements `IEntity` interface
- Has `IsEmpty()` method
- Already has Barcode property for UPC/EAN codes

**IdentifierType Enum** (`MediaSet.Api/Models/IdentifierType.cs`)
- Current values: Isbn, Lccn, Oclc, Olid, Upc, Ean
- Already supports UPC and EAN - no changes needed

**MediaTypes Enum** (`MediaSet.Api/Models/MediaTypes.cs`)
- Already includes `Musics = 3`
- No changes needed

### Frontend Components

**Add/Edit Entity Routes** (`MediaSet.Remix/app/routes/$entity_.add/route.tsx`, `MediaSet.Remix/app/routes/$entity_.$id.edit/route.tsx`)
- Books, Movies, Games: Have lookup sections that prefill form with lookup results
- Uses intent-based form submission (lookup vs. create/update)
- Pattern should be extended to music with barcode field
- Already has support for music forms with `MusicForm` component

**Lookup Data Function** (`MediaSet.Remix/app/lookup-data.server.ts`)
- `lookup(entityType: Entity, identifierType: IdentifierType, identifierValue: string)` function
- Currently supports books, movies, games
- Need to add: Music support with `MusicLookupResponse` type

**Music Form Component** (`MediaSet.Remix/app/components/music-form.tsx`)
- Existing form for music entity
- Has fields for: title, artist, release date, genres, duration, label, barcode, tracks, discs, disc list
- Need to add: Lookup button similar to books, movies, and games

**TypeScript Models** (`MediaSet.Remix/app/models.ts`)
- `MusicEntity` already exists
- Need to add: `MusicLookupResponse` interface

### Infrastructure/Configuration

**HTTP Client Registration** (`MediaSet.Api/Program.cs`)
- Pattern: `builder.Services.AddHttpClient<IClient, Client>()`
- Existing clients: OpenLibraryClient, UpcItemDbClient, TmdbClient, GiantBombClient
- Need to add: MusicBrainzClient

**Strategy Registration** (`MediaSet.Api/Program.cs`)
- Pattern: `builder.Services.AddScoped<ILookupStrategy<TResponse>, TStrategy>()`
- Existing: `BookLookupStrategy`, `MovieLookupStrategy`, `GameLookupStrategy`
- Need to add: `MusicLookupStrategy`

**Configuration** (`MediaSet.Api/appsettings.json`)
- Existing sections: OpenLibrary, UpcItemDb, Tmdb, GiantBomb
- Need to add: MusicBrainz configuration

## Requirements

### Functional Requirements

1. **Music Barcode Lookup**
   - Support UPC/EAN identifier types for music
   - Query MusicBrainz API directly by barcode to fetch comprehensive album information
   - Map music metadata to `MusicResponse` model

2. **MusicBrainz API Integration**
   - Use MusicBrainz API (Free, no API key required)
     - Search by barcode: `GET https://musicbrainz.org/ws/2/release/?query=barcode:{barcode}&fmt=json`
     - Get release details: `GET https://musicbrainz.org/ws/2/release/{id}?inc=artists+labels+recordings&fmt=json`
     - Docs: https://musicbrainz.org/doc/MusicBrainz_API
     - Rate limit: 1 request per second (respectful usage)
     - Provides: title, artist, release date, label, track list, genres (via tags)
   - Implement HTTP client with proper rate limiting
   - Query by barcode directly (no need for UPCitemdb as intermediary)
   - Map response to `MusicResponse` model, including full track list

3. **MusicLookupStrategy Implementation**
   - Implement `ILookupStrategy<MusicResponse>`
   - Handle UPC/EAN identifier types
   - Single-step lookup process:
     1. Query MusicBrainz API directly by barcode → fetch full album details
   - Clean and normalize data (format dates, extract genres, build track list)
   - Return `MusicResponse` or null if not found

4. **Frontend Lookup Integration**
   - Add lookup button to music add/edit forms
   - Display lookup results in a preview section
   - Pre-fill form fields with lookup data
   - Allow user to review and modify before saving
   - Show loading state during lookup
   - Display clear error messages for failed lookups

### Non-Functional Requirements

- **Performance**: API responses within 3 seconds for external lookups
- **Caching**: Consider caching UPC/music API lookups to reduce API calls
- **Error Handling**: Clear error messages for invalid barcodes, API failures, and rate limits
- **API Rate Limits**: 
  - MusicBrainz: 1 request/second (no API key needed)
- **Security**: Store API keys (if needed) in configuration/environment variables
- **Accessibility**: Maintain existing accessibility standards in UI

## Proposed Changes

### Backend Changes (MediaSet.Api)

#### New/Modified Models

**New: MusicResponse.cs** (`MediaSet.Api/Models/MusicResponse.cs`)
- Response model for music lookup results
- Properties:
  ```csharp
  public string Title { get; set; }
  public string Artist { get; set; }
  public string ReleaseDate { get; set; }
  public List<string> Genres { get; set; }
  public int? Duration { get; set; }  // Total duration in seconds
  public string Label { get; set; }
  public int? Tracks { get; set; }
  public int? Discs { get; set; }
  public List<DiscResponse> DiscList { get; set; }
  public string Format { get; set; }  // CD, Vinyl, Digital, etc.
  ```

**New: DiscResponse.cs** (`MediaSet.Api/Models/DiscResponse.cs`)
- Response model for disc/track information
- Properties:
  ```csharp
  public int TrackNumber { get; set; }
  public string Title { get; set; }
  public string Duration { get; set; }  // Format: "MM:SS"
  ```

**New: MusicBrainzConfiguration.cs** (`MediaSet.Api/Models/MusicBrainzConfiguration.cs`)
- Configuration model for MusicBrainz API
- Properties:
  ```csharp
  public string BaseUrl { get; set; }
  public int Timeout { get; set; } = 10;
  public string UserAgent { get; set; } = "MediaSet/1.0";
  ```

**API Response Models**
- Internal models for mapping music API responses (similar to `TmdbMovieResponse`, `GiantBombSearchResult`)
- Examples:
  - `MusicBrainzReleaseResponse` - for MusicBrainz API responses
  - `MusicBrainzRelease` - release information
  - `MusicBrainzArtist` - artist information
  - `MusicBrainzTrack` - track information

#### New/Modified Services

**New: MusicLookupStrategy.cs** (`MediaSet.Api/Services/MusicLookupStrategy.cs`)
- Implements `ILookupStrategy<MusicResponse>`
- Dependencies: `IMusicBrainzClient`, `ILogger<MusicLookupStrategy>`
- Supported identifiers: UPC, EAN
- Methods:
  ```csharp
  public bool CanHandle(MediaTypes entityType, IdentifierType identifierType)
  {
      return entityType == MediaTypes.Musics && 
             (identifierType == IdentifierType.Upc || identifierType == IdentifierType.Ean);
  }
  
  public async Task<MusicResponse?> LookupAsync(
      IdentifierType identifierType, 
      string identifierValue, 
      CancellationToken cancellationToken)
  {
      // Query MusicBrainz API directly by barcode
      var musicResult = await _musicBrainzClient.GetReleaseByBarcodeAsync(identifierValue, cancellationToken);
      if (musicResult == null)
          return null;
      
      // Map to MusicResponse
      return MapToMusicResponse(musicResult);
  }
  
  private MusicResponse MapToMusicResponse(MusicBrainzReleaseResponse release)
  {
      // Map API response to MusicResponse model
      // Extract: title, artist, release date, genres, label, tracks, disc list
      // Format duration, clean strings, normalize dates
  }
  
  private string FormatDuration(int? milliseconds)
  {
      // Convert milliseconds to "MM:SS" format
  }
  ```

**Modified: LookupApi.cs** (`MediaSet.Api/Lookup/LookupApi.cs`)
- Update return type to include `Ok<MusicResponse>`
- Current: `Results<Ok<BookResponse>, Ok<MovieResponse>, Ok<GameResponse>, NotFound, BadRequest<string>>`
- New: `Results<Ok<BookResponse>, Ok<MovieResponse>, Ok<GameResponse>, Ok<MusicResponse>, NotFound, BadRequest<string>>`
- Add handling for music entity type in the lookup endpoint

#### New/Modified Clients

**New: IMusicBrainzClient.cs** (`MediaSet.Api/Clients/IMusicBrainzClient.cs`)
- Interface for MusicBrainz API
- Methods:
  ```csharp
  public interface IMusicBrainzClient
  {
      Task<MusicBrainzReleaseResponse?> GetReleaseByBarcodeAsync(string barcode, CancellationToken cancellationToken);
  }
  ```

**New: MusicBrainzClient.cs** (`MediaSet.Api/Clients/MusicBrainzClient.cs`)
- HTTP client for MusicBrainz API
- Configuration: `MusicBrainzConfiguration` (base URL, timeout, user agent)
- Endpoints:
  - Search by barcode: `https://musicbrainz.org/ws/2/release/?query=barcode:{barcode}&fmt=json`
  - Get release details: `https://musicbrainz.org/ws/2/release/{id}?inc=artists+labels+recordings&fmt=json`
- Response mapping to `MusicResponse`
- Error handling for rate limits (503 status code)
- Implement polite rate limiting (1 request per second)
- Example implementation:
  ```csharp
  public class MusicBrainzClient : IMusicBrainzClient
  {
      private readonly HttpClient _httpClient;
      private readonly ILogger<MusicBrainzClient> _logger;
      private readonly MusicBrainzConfiguration _configuration;
      private static readonly SemaphoreSlim _rateLimiter = new(1, 1);
      private static DateTime _lastRequestTime = DateTime.MinValue;
      
      public MusicBrainzClient(
          HttpClient httpClient,
          IOptions<MusicBrainzConfiguration> configuration,
          ILogger<MusicBrainzClient> logger)
      {
          _httpClient = httpClient;
          _logger = logger;
          _configuration = configuration.Value;
          
          _httpClient.BaseAddress = new Uri(_configuration.BaseUrl);
          _httpClient.Timeout = TimeSpan.FromSeconds(_configuration.Timeout);
          _httpClient.DefaultRequestHeaders.Add("User-Agent", _configuration.UserAgent);
      }
      
      public async Task<MusicBrainzReleaseResponse?> GetReleaseByBarcodeAsync(
          string barcode, 
          CancellationToken cancellationToken)
      {
          await _rateLimiter.WaitAsync(cancellationToken);
          try
          {
              // Ensure at least 1 second between requests
              var timeSinceLastRequest = DateTime.UtcNow - _lastRequestTime;
              if (timeSinceLastRequest < TimeSpan.FromSeconds(1))
              {
                  await Task.Delay(TimeSpan.FromSeconds(1) - timeSinceLastRequest, cancellationToken);
              }
              
              _logger.LogInformation("Looking up music release by barcode: {Barcode}", barcode);
              
              var response = await _httpClient.GetAsync(
                  $"ws/2/release/?query=barcode:{barcode}&fmt=json", 
                  cancellationToken);
              
              _lastRequestTime = DateTime.UtcNow;
              
              if (!response.IsSuccessStatusCode)
              {
                  if (response.StatusCode == System.Net.HttpStatusCode.ServiceUnavailable)
                  {
                      _logger.LogWarning("MusicBrainz rate limit exceeded for barcode: {Barcode}", barcode);
                      throw new HttpRequestException("MusicBrainz rate limit exceeded", null, response.StatusCode);
                  }
                  
                  _logger.LogWarning("MusicBrainz returned status code {StatusCode} for barcode: {Barcode}", 
                      response.StatusCode, barcode);
                  return null;
              }
              
              var content = await response.Content.ReadAsStringAsync(cancellationToken);
              var result = JsonSerializer.Deserialize<MusicBrainzSearchResponse>(content, 
                  new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
              
              if (result?.Releases == null || result.Releases.Count == 0)
              {
                  _logger.LogInformation("No releases found for barcode: {Barcode}", barcode);
                  return null;
              }
              
              // Get the first release (best match)
              var release = result.Releases[0];
              
              // Fetch full release details if needed
              // (Optional: If initial search doesn't include tracks)
              
              return release;
          }
          finally
          {
              _rateLimiter.Release();
          }
      }
  }
  ```

#### New/Modified API Endpoints

**Modified: Lookup Endpoint** (`MediaSet.Api/Lookup/LookupApi.cs`)
- Endpoint: `GET /lookup/{entityType}/{identifierType}/{identifierValue}`
- Add support for `entityType = musics`
- Return `MusicResponse` when successful
- Example response for `GET /lookup/musics/upc/093624993629`:
  ```json
  {
    "title": "Abbey Road",
    "artist": "The Beatles",
    "releaseDate": "1969-09-26",
    "genres": ["Rock", "Pop"],
    "duration": 2880,
    "label": "Apple Records",
    "tracks": 17,
    "discs": 1,
    "discList": [
      { "trackNumber": 1, "title": "Come Together", "duration": "4:20" },
      { "trackNumber": 2, "title": "Something", "duration": "3:03" },
      // ... more tracks
    ],
    "format": "CD"
  }
  ```

#### Database Changes

- No database schema changes required
- Music collection already exists
- Music model already has Barcode field

#### Configuration Changes

**appsettings.json** updates:
```json
{
  "MusicBrainz": {
    "BaseUrl": "https://musicbrainz.org/",
    "Timeout": 10,
    "UserAgent": "MediaSet/1.0 (contact@yourdomain.com)"
  }
}
```

**Program.cs** updates:
```csharp
// Configure MusicBrainz client
var musicBrainzConfig = builder.Configuration.GetSection(nameof(MusicBrainzConfiguration));
if (musicBrainzConfig.Exists())
{
    using var bootstrapLoggerFactory = LoggerFactory.Create(logging => logging.AddSimpleConsole());
    var bootstrapLogger = bootstrapLoggerFactory.CreateLogger("MediaSet.Api");
    bootstrapLogger.LogInformation("MusicBrainz configuration exists. Setting up MusicBrainz services.");
    builder.Services.Configure<MusicBrainzConfiguration>(musicBrainzConfig);
    builder.Services.AddHttpClient<IMusicBrainzClient, MusicBrainzClient>();
}

// Register MusicLookupStrategy
builder.Services.AddScoped<ILookupStrategy<MusicResponse>, MusicLookupStrategy>();
```

### Frontend Changes (MediaSet.Remix)

#### New/Modified Routes

**Modified: Add Music Route** (`MediaSet.Remix/app/routes/$entity_.add/route.tsx`)
- Already has basic support for music forms
- Lookup functionality should already work with the intent-based action handler
- Ensure `MusicLookupResponse` type is imported and used

**Modified: Edit Music Route** (`MediaSet.Remix/app/routes/$entity_.$id.edit/route.tsx`)
- Similar to add route
- Ensure lookup functionality works in edit mode

#### New/Modified Components

**Modified: MusicForm Component** (`MediaSet.Remix/app/components/music-form.tsx`)
- Add barcode lookup section similar to BookForm, MovieForm, GameForm
- Add lookup button with barcode input
- Display lookup results in a preview section
- Example addition:
  ```tsx
  {/* Barcode Lookup Section */}
  <div className="bg-gray-700 p-4 rounded-md">
    <h3 className="text-lg font-semibold text-gray-100 mb-3">Barcode Lookup</h3>
    <div className="flex gap-2">
      <div className="flex-1">
        <label htmlFor="barcodeInput" className="block text-sm font-medium text-gray-200 mb-1">
          UPC/EAN Barcode
        </label>
        <input
          id="barcodeInput"
          name="barcodeInput"
          type="text"
          className={inputClasses}
          placeholder="Enter barcode (UPC or EAN)"
          aria-label="Barcode"
          defaultValue={lookupIdentifierValue}
        />
      </div>
      <div className="flex items-end">
        <button
          type="submit"
          name="intent"
          value="lookup"
          className="px-4 py-2 bg-blue-600 text-white rounded-md hover:bg-blue-700 focus:outline-none focus:ring-2 focus:ring-blue-400"
          disabled={isSubmitting}
        >
          <input type="hidden" name="fieldName" value="barcode" />
          {isSubmitting ? "Looking up..." : "Lookup"}
        </button>
      </div>
    </div>
    {lookupError && (
      <div className="mt-2 p-2 bg-red-900/50 border border-red-700 rounded text-red-200">
        {lookupError}
      </div>
    )}
  </div>
  ```

#### New/Modified Data Functions

**Modified: lookup-data.server.ts** (`MediaSet.Remix/app/lookup-data.server.ts`)
- Add support for music entity type
- Add `getIdentifierTypeForField` support for music barcode field
- Example:
  ```typescript
  export function getIdentifierTypeForField(entityType: Entity, fieldName: string): IdentifierType {
    // ... existing logic for books, movies, games
    
    if (entityType === Entity.Musics) {
      if (fieldName === 'barcode') {
        return 'upc'; // or 'ean', both supported
      }
    }
    
    throw new Error(`Unknown field name ${fieldName} for entity type ${entityType}`);
  }
  ```

#### Type Definitions

**Modified: models.ts** (`MediaSet.Remix/app/models.ts`)
- Add `MusicLookupResponse` interface
- Example:
  ```typescript
  export interface MusicLookupResponse {
    title: string;
    artist: string;
    releaseDate: string;
    genres: string[];
    duration: number | null;
    label: string;
    tracks: number | null;
    discs: number | null;
    discList: Disc[];
    format?: string;
  }
  ```

### Testing Changes

#### Backend Tests (MediaSet.Api.Tests)

**Test Class**: `MusicLookupStrategyTests` (`MediaSet.Api.Tests/Services/MusicLookupStrategyTests.cs`)
- Test scenarios to cover:
  - `CanHandle_WithMusicsAndUpc_ReturnsTrue`
  - `CanHandle_WithMusicsAndEan_ReturnsTrue`
  - `CanHandle_WithBooksAndUpc_ReturnsFalse`
  - `CanHandle_WithMoviesAndUpc_ReturnsFalse`
  - `LookupAsync_WithValidUpc_ReturnsMusicResponse`
  - `LookupAsync_WithValidEan_ReturnsMusicResponse`
  - `LookupAsync_WithInvalidUpc_ReturnsNull`
  - `LookupAsync_WithNoMusicBrainzResults_ReturnsNull`
  - `LookupAsync_MapsArtistCorrectly`
  - `LookupAsync_MapsGenresCorrectly`
  - `LookupAsync_MapsTrackListCorrectly`
  - `LookupAsync_FormatsDurationCorrectly`
- Mock dependencies: `IMusicBrainzClient`
- Edge cases:
  - Barcode with no results
  - Multiple releases (select best match)
  - MusicBrainz rate limit (503 error)
  - Missing track information
  - Format variations (CD, Vinyl, Digital)

**Test Class**: `MusicBrainzClientTests` (`MediaSet.Api.Tests/Clients/MusicBrainzClientTests.cs`)
- Test scenarios:
  - `GetReleaseByBarcodeAsync_WithValidBarcode_ReturnsRelease`
  - `GetReleaseByBarcodeAsync_WithInvalidBarcode_ReturnsNull`
  - `GetReleaseByBarcodeAsync_WithRateLimitError_ThrowsException`
  - `GetReleaseByBarcodeAsync_RespectsRateLimit`
  - HTTP error handling (503, 404, 500)
  - Rate limiting (1 request per second)

**Test Class**: `LookupApiTests` (`MediaSet.Api.Tests/Lookup/LookupApiTests.cs`)
- Update existing tests to include music
- Test scenarios:
  - `Lookup_WithMusicsAndUpc_ReturnsMusicResponse`
  - `Lookup_WithMusicsAndEan_ReturnsMusicResponse`
  - `Lookup_WithMusicsAndInvalidBarcode_ReturnsNotFound`
  - `Lookup_WithMusicsAndEmptyBarcode_ReturnsBadRequest`

**Test Class**: `LookupStrategyFactoryTests` (if exists)
- Test scenarios:
  - `GetStrategy_WithMusicsAndUpc_ReturnsMusicStrategy`
  - `GetStrategy_WithMusicsAndEan_ReturnsMusicStrategy`

#### Frontend Tests (MediaSet.Remix)

**Test File**: `music-form.test.tsx` (`MediaSet.Remix/app/components/music-form.test.tsx`)
- Test scenarios:
  - Renders barcode lookup section
  - Lookup button triggers form submission with correct intent
  - Displays lookup results in form fields
  - Shows error message on lookup failure
  - Allows user to modify pre-filled fields

**Test File**: `music-add.test.tsx` (or similar integration test)
- Test scenarios:
  - Full lookup flow: enter barcode → click lookup → see results → save entity
  - Error handling: invalid barcode, API failure
  - Loading states during lookup

#### Integration Tests

**Scenario**: End-to-end music barcode lookup
- Steps:
  1. Navigate to add music page
  2. Enter UPC barcode "093624993629" (Abbey Road by The Beatles)
  3. Click lookup button
  4. Verify MusicBrainz API is called with correct barcode
  5. Verify form is pre-filled with correct data:
     - Title: "Abbey Road"
     - Artist: "The Beatles"
     - Release Date: "1969-09-26"
     - Genres: ["Rock"]
     - Label: "Apple Records"
     - Tracks: 17
  6. Submit form
  7. Verify music entity is created with correct data

**Scenario**: Music barcode lookup with no results
- Steps:
  1. Enter invalid barcode "000000000000"
  2. Click lookup button
  3. Verify error message is displayed
  4. Verify form fields are not changed

## Implementation Steps

1. **Create backend models**
   - Create `MusicResponse.cs` with all required properties
   - Create `DiscResponse.cs` for track information
   - Create `MusicBrainzConfiguration.cs` for API configuration
   - Create internal API response models (`MusicBrainzReleaseResponse`, etc.)

2. **Implement MusicBrainz client**
   - Create `IMusicBrainzClient` interface
   - Create `MusicBrainzClient` implementation with rate limiting
   - Add HTTP client registration in `Program.cs`
   - Write unit tests with mocked HTTP responses
   - Test with real API (manual/integration test)

3. **Implement MusicLookupStrategy**
   - Create `MusicLookupStrategy` class implementing `ILookupStrategy<MusicResponse>`
   - Implement `CanHandle` method for Musics + UPC/EAN
   - Implement `LookupAsync` method with direct MusicBrainz barcode search
   - Implement `MapToMusicResponse` helper method
   - Implement duration formatting helper
   - Write comprehensive unit tests
   - Register strategy in `Program.cs`

4. **Update LookupApi**
   - Update return type to include `Ok<MusicResponse>`
   - Update endpoint handler to support music entity type
   - Add appropriate logging
   - Update tests to include music scenarios

5. **Add configuration**
   - Add MusicBrainz configuration section to `appsettings.json`
   - Add MusicBrainz configuration to `appsettings.Development.json`
   - Document required configuration in README

6. **Create/update backend tests**
   - Write `MusicBrainzClientTests`
   - Write `MusicLookupStrategyTests`
   - Update `LookupApiTests`
   - Ensure good code coverage (>80%)

7. **Update frontend TypeScript models**
   - Add `MusicLookupResponse` interface to `models.ts`
   - Ensure `Disc` interface is already defined (it is)

8. **Update lookup data function**
   - Update `getIdentifierTypeForField` to support music barcode
   - Ensure `lookup` function handles music entity type
   - Update error handling if needed

9. **Update MusicForm component**
   - Add barcode lookup section with input and button
   - Add lookup error display
   - Add lookup result pre-fill logic
   - Style consistently with other entity forms

10. **Update add/edit routes**
    - Ensure music lookup is properly handled in action functions
    - Ensure `MusicLookupResponse` is properly typed in actionData
    - Test lookup flow in both add and edit modes

11. **Create/update frontend tests**
    - Write `music-form.test.tsx`
    - Write integration tests for lookup flow
    - Update existing tests if needed

12. **Integration testing**
    - Test with real barcodes (e.g., "093624993629" for Abbey Road)
    - Test error scenarios (invalid barcodes, rate limits)
    - Test across different music formats (CD, Vinyl, etc.)
    - Verify all metadata is correctly populated

13. **Documentation updates**
    - Update README.md with music barcode lookup feature
    - Add MusicBrainz setup instructions (similar to TMDB_SETUP.md)
    - Document MusicBrainz API rate limits and best practices
    - Update API documentation (if exists)

8. **Update frontend TypeScript models**
   - Add `MusicLookupResponse` interface to `models.ts`
   - Ensure `Disc` interface is already defined (it is)

9. **Update lookup data function**
   - Update `getIdentifierTypeForField` to support music barcode
   - Ensure `lookup` function handles music entity type
   - Update error handling if needed

10. **Update MusicForm component**
    - Add barcode lookup section with input and button
    - Add lookup error display
    - Add lookup result pre-fill logic
    - Style consistently with other entity forms

11. **Update add/edit routes**
    - Ensure music lookup is properly handled in action functions
    - Ensure `MusicLookupResponse` is properly typed in actionData
    - Test lookup flow in both add and edit modes

12. **Create/update frontend tests**
    - Write `music-form.test.tsx`
    - Write integration tests for lookup flow
    - Update existing tests if needed

13. **Integration testing**
    - Test with real barcodes (e.g., "093624993629" for Abbey Road)
    - Test error scenarios (invalid barcodes, rate limits)
    - Test across different music formats (CD, Vinyl, etc.)
    - Verify all metadata is correctly populated

14. **Documentation updates**
    - Update README.md with music barcode lookup feature
    - Add MusicBrainz setup instructions (similar to TMDB_SETUP.md)
    - Document MusicBrainz API rate limits and best practices
    - Update API documentation (if exists)

## Acceptance Criteria

- [ ] Backend: `MusicResponse` model created with all required properties
- [ ] Backend: `MusicBrainzClient` successfully queries MusicBrainz API by barcode
- [ ] Backend: `MusicBrainzClient` respects rate limiting (1 request per second)
- [ ] Backend: `MusicLookupStrategy` handles UPC/EAN lookups
- [ ] Backend: Lookup API endpoint supports music entity type (`/lookup/musics/{upc|ean}/{value}`)
- [ ] Backend: API returns `MusicResponse` with correct data structure
- [ ] Backend: All unit tests pass with >80% coverage
- [ ] Frontend: TypeScript models include `MusicLookupResponse` type
- [ ] Frontend: `MusicForm` component includes barcode lookup section
- [ ] Frontend: Lookup button triggers lookup action correctly
- [ ] Frontend: Lookup results pre-fill form fields
- [ ] Frontend: Error messages display clearly on lookup failure
- [ ] Frontend: Loading states display during lookup
- [ ] Integration: Full lookup flow works end-to-end
- [ ] Integration: Invalid barcodes return appropriate error messages
- [ ] Integration: Rate limiting is respected and doesn't cause errors
- [ ] Documentation: README updated with music barcode lookup feature
- [ ] Documentation: MusicBrainz setup instructions added
- [ ] Tests: All backend tests pass (MusicBrainzClient, MusicLookupStrategy, LookupApi)
- [ ] Tests: All frontend tests pass (music-form, integration tests)

## Risks and Mitigations

| Risk | Impact | Likelihood | Mitigation |
|------|--------|------------|------------|
| MusicBrainz API rate limiting | Medium | Medium | Implement proper rate limiter with 1-second delay; add retry logic; cache results; inform user of limitations |
| MusicBrainz returns wrong album | Medium | Low | Use barcode as primary identifier (more accurate than title search); allow user to review and modify results before saving |
| MusicBrainz search returns no results | Medium | Medium | Provide clear error message; suggest manual entry |
| Missing track list in API response | Low | Low | Handle gracefully; populate other fields; allow manual track list entry |
| Genre data missing or inconsistent | Low | Medium | Use MusicBrainz tags/genres when available; allow manual genre entry; normalize genre names |
| Performance degradation with sequential API calls | Medium | Low | Use direct barcode search on MusicBrainz (single call); implement caching; monitor response times |
| MusicBrainz API changes breaking integration | High | Low | Use versioned API endpoints; monitor API status; implement comprehensive error handling; have fallback strategy |
| Duration format inconsistencies | Low | Medium | Implement robust duration parsing and formatting; handle missing durations gracefully |
| Label information missing | Low | Medium | Use available label data from API; allow manual entry; mark as optional field |
| Multiple releases for same barcode | Medium | Low | Select first/best match based on release date or country; consider showing multiple results for user selection (future enhancement) |

## Open Questions

1. **How should we handle multiple releases for the same barcode?**
   - **For MVP**: Return the first/best match (most recent or primary release)
   - **Future enhancement**: Allow user to select from multiple matches

2. **Should we display all track information or just track count?**
   - **Recommendation**: Display full track list with durations if available
   - This provides more value to users and matches the existing Music model structure

3. **What format should we use for duration?**
   - **Recommendation**: 
     - Store total duration in seconds (as `int?`) in backend
     - Display as "MM:SS" for individual tracks in frontend
     - Display as "HH:MM:SS" or "X minutes" for total duration

4. **Should we support lookup in edit mode or only add mode?**
   - **Recommendation**: Support in both modes (consistent with books, movies, games)
   - In edit mode, allow user to re-lookup and update existing data

5. **How should we handle missing or incomplete data from the API?**
   - **Recommendation**: 
     - Populate available fields
     - Leave missing fields empty for user to fill manually
     - Display informational message if data is incomplete

6. **Should we cache music metadata lookups?**
   - **For MVP**: No caching (keep it simple)
   - **Future enhancement**: Implement caching to reduce API calls and improve performance

## Dependencies

### External Libraries/Packages

**Backend (MediaSet.Api)**
- No new NuGet packages required
- Use existing `System.Net.Http` for HTTP client
- Use existing `System.Text.Json` for JSON serialization

**Frontend (MediaSet.Remix)**
- No new npm packages required
- Use existing Remix.js and React functionality

### Third-Party API Integrations

**MusicBrainz API**
- Free tier: 1 request per second (no API key required)
- Documentation: https://musicbrainz.org/doc/MusicBrainz_API
- Rate limit: Be respectful with rate limiting
- User-Agent header required with contact information

### Infrastructure Changes

**Configuration Management**
- Add MusicBrainz configuration to `appsettings.json`
- Update deployment documentation with configuration requirements
- Add user-agent configuration with contact email

**Rate Limiting**
- Implement in-memory rate limiter for MusicBrainz client
- Use `SemaphoreSlim` to ensure 1 request per second

## References

### Similar Implementations
- `MovieLookupStrategy.cs` - very similar pattern (UPC → metadata API)
- `GameLookupStrategy.cs` - very similar pattern (UPC → metadata API)
- `BookLookupStrategy.cs` - strategy pattern, multiple identifier types
- Existing `LookupStrategyFactory.cs` - factory pattern to follow

### API Documentation
- MusicBrainz API docs: https://musicbrainz.org/doc/MusicBrainz_API
- MusicBrainz Release Search: https://musicbrainz.org/doc/MusicBrainz_API/Search
- MusicBrainz Web Service: https://musicbrainz.org/doc/Development/XML_Web_Service/Version_2

### Music Database Resources
- MusicBrainz release example: https://musicbrainz.org/release/0c324f3a-dcbb-4e5a-8d8a-f13f2f49c663 (Abbey Road)
- Test barcode: UPC 093624993629 (Abbey Road by The Beatles)

### Code Style Guidelines
- Backend: [/.github/code-style-api.md](../.github/code-style-api.md)
- Frontend: [/.github/code-style-ui.md](../.github/code-style-ui.md)

### Related Issues
- GitHub Issue #201: Music barcode lookup
