# Game Barcode Lookup Implementation Plan

## Overview

This feature extends the existing barcode lookup functionality to support video games. Similar to how movies use UPCitemdb for barcode identification and TMDB for metadata enrichment, games will use UPCitemdb to identify the product and the GiantBomb API to fetch comprehensive game information.

The existing lookup system already supports books (ISBN/UPC via OpenLibrary) and movies (UPC via TMDB), both using the Strategy Pattern. This implementation will add a third strategy for games that follows the same established patterns.

**What is being built?**
- Game barcode (UPC/EAN) lookup endpoint
- Integration with the GiantBomb API (Search endpoint)
- `GameLookupStrategy` implementing the existing `ILookupStrategy<GameResponse>` interface
- Frontend lookup button on game add/edit forms (following the same pattern as books and movies)

**Why is it needed?**
Users want to quickly add games to their collection by scanning barcodes, just like they can with books (ISBN) and movies (UPC).

**What value does it provide?**
- Rapid game cataloging via barcode scanning
- Automatic metadata population (title, platform, genres, developers, publishers, release date, rating, description)
- Consistent UX across all media types (books, movies, games)
- Reduced manual data entry errors

## Related Existing Functionality

### Backend Components

**Lookup System** (`MediaSet.Api/Lookup/LookupApi.cs`)
- Endpoint pattern: `GET /lookup/{entityType}/{identifierType}/{identifierValue}`
- Currently supports: Books (isbn, lccn, oclc, olid, upc, ean), Movies (upc, ean)
- Uses `LookupStrategyFactory` to route to appropriate strategy
- Returns typed responses: `Results<Ok<BookResponse>, Ok<MovieResponse>, NotFound, BadRequest<string>>`
- Need to add: Games (upc, ean)
- Need to update: Return type to include `Ok<GameResponse>`

**Strategy Pattern Implementation**
- `ILookupStrategy<TResponse>` interface (`MediaSet.Api/Services/ILookupStrategy.cs`)
  - `bool CanHandle(MediaTypes entityType, IdentifierType identifierType)`
  - `Task<TResponse?> LookupAsync(IdentifierType identifierType, string identifierValue, CancellationToken cancellationToken)`
- `BookLookupStrategy` - handles books with multiple identifier types
- `MovieLookupStrategy` - handles movies with UPC/EAN via UPCitemdb → TMDB
- `LookupStrategyFactory` - resolves strategies via DI

**UPC Lookup Client** (`MediaSet.Api/Clients/UpcItemDbClient.cs`)
- Already implemented and shared by MovieLookupStrategy
- Returns `UpcItemResponse` with title, category, brand, etc.
- Will be reused by GameLookupStrategy
- No changes needed

**Game Model** (`MediaSet.Api/Models/Game.cs`)
- Properties: Title, Format, Barcode, ReleaseDate, Rating, Platform, Developers, Publishers, Genres, Description
- Implements `IEntity` interface
- Has `IsEmpty()` method
- Already has Barcode property for UPC/EAN codes

**IdentifierType Enum** (`MediaSet.Api/Models/IdentifierType.cs`)
- Current values: Isbn, Lccn, Oclc, Olid, Upc, Ean
- Games will use: Upc, Ean (no changes needed to enum)

### Frontend Components

**Entity Routes Pattern** (`MediaSet.Remix/app/routes/$entity_.add/route.tsx`, `MediaSet.Remix/app/routes/$entity_.$id.edit/route.tsx`)
- Books have ISBN lookup button inline in form
- Movies have Barcode lookup button inline in form
- Pattern: Lookup button adjacent to barcode field, triggers `lookup` intent
- Form action distinguishes between "lookup" and "save" intents
- Lookup results populate form fields that can be edited before saving
- Games will follow the same pattern

**Lookup Data Function** (`MediaSet.Remix/app/lookup-data.ts`)
- Function: `lookup(entityType: Entity, identifierType: string, identifierValue: string)`
- Makes API call to `/lookup/{entityType}/{identifierType}/{identifierValue}`
- Returns `BookEntity | MovieEntity | LookupError`
- Need to extend return type to include `GameEntity`

**TypeScript Models** (`MediaSet.Remix/app/models.ts`)
- `Entity` enum includes: Books, Movies, Games, Musics
- `BookEntity`, `MovieEntity` interfaces defined
- `GameEntity` interface likely defined
- Need to add `GameLookupResponse` type

### Infrastructure/Configuration

**HTTP Client Registration** (`MediaSet.Api/Program.cs`)
- Pattern: `builder.Services.AddHttpClient<IClient, Client>()`
- Existing clients: OpenLibraryClient, UpcItemDbClient, TmdbClient
- Need to add: Game metadata client (e.g., IgdbClient)

**Strategy Registration** (`MediaSet.Api/Program.cs`)
- Pattern: `builder.Services.AddScoped<ILookupStrategy<TResponse>, TStrategy>()`
- Existing: `BookLookupStrategy`, `MovieLookupStrategy`
- Need to add: `GameLookupStrategy`

**Configuration** (`MediaSet.Api/appsettings.json`)
- Existing sections: OpenLibrary, UpcItemDb, Tmdb
- Need to add: Game metadata API configuration (e.g., Igdb)

## Requirements

### Functional Requirements

1. **Game Barcode Lookup**
   - Support UPC/EAN identifier types for games
   - Query UPCitemdb to identify game product and extract title
   - Use game metadata API to fetch comprehensive game information
   - Map game metadata to `GameResponse` model

2. **Game Metadata API Integration**
   - Use the GiantBomb API, specifically the Search endpoint
     - Endpoint: `GET https://www.giantbomb.com/api/search/?api_key=...&format=json&resources=game&query={title}`
     - Docs: https://www.giantbomb.com/api/documentation/#toc-0-41
   - Implement HTTP client for GiantBomb
   - For each UPC lookup: strip edition markers from the title for the search query
   - Search by title and fetch a matching game; optionally follow-up with game details (`/game/[guid]`) if additional fields are required
   - Map response to `GameResponse` model, adding edition back to the Title in the final response

3. **GameLookupStrategy Implementation**
   - Implement `ILookupStrategy<GameResponse>`
   - Support UPC/EAN identifier types
   - Flow: UPC → UPCitemdb → extract title → search game API → fetch details
   - Return `GameResponse` with all relevant metadata

4. **API Endpoint Extension**
   - Extend existing endpoint `/lookup/{entityType}/{identifierType}/{identifierValue}`
   - Add support for `entityType=games` with `identifierType=upc|ean`
   - Update return type to include `Ok<GameResponse>`
   - Maintain backward compatibility with books and movies

5. **Frontend Integration**
   - Add lookup button to Barcode field in game add/edit forms
   - Implement lookup intent handling in form actions
   - Populate form fields with lookup results
   - Allow user to edit fields before saving
   - Display errors inline

### Non-Functional Requirements

- **Performance**: Game lookups complete within 3 seconds
- **Caching**: Consider caching UPC → game metadata mappings to reduce API calls
- **Error Handling**: 
  - Clear messages for invalid barcodes
  - Handle API failures gracefully
  - Display rate limit warnings
- **API Rate Limits**:
  - GiantBomb: API key required; observe GiantBomb's documented limits and fair use policies
  - UPCitemdb: 100 requests/day (already in use, shared with movies)
- **Security**: Store API keys in configuration/environment variables
- **Consistency**: Follow same UX patterns as book/movie lookups

## Proposed Changes

### Backend Changes (MediaSet.Api)

#### New/Modified Models

**New: GameResponse.cs** (`MediaSet.Api/Models/GameResponse.cs`)
- Response model for game metadata lookup
- Properties:
  ```csharp
  public record GameResponse(
      string Title,              // From GiantBomb, with edition added back to match barcode
      string Platform,           // Single platform from barcode result (not metadata)
      List<string> Genres,
      List<string> Developers,
      List<string> Publishers,
      string ReleaseDate,        // Formatted date string
      string Rating,             // ESRB rating or equivalent
      string Description,        // Game plot/description
      string Format              // Physical format (from UPC, e.g., "Disc", "Cartridge")
  );
  ```
- Maps from GiantBomb API response combined with barcode-derived platform/format

**New: GiantBombGameResponse.cs** (`MediaSet.Api/Models/`)
- API-specific response models for GiantBomb
- Examples:
  ```csharp
  public record GiantBombSearchResult(
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("original_release_date")] string? OriginalReleaseDate,
    [property: JsonPropertyName("deck")] string? Deck,
    [property: JsonPropertyName("api_detail_url")] string ApiDetailUrl
  );
  
  public record GiantBombGameDetails(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("genres")] List<GiantBombNameRef>? Genres,
    [property: JsonPropertyName("developers")] List<GiantBombCompanyRef>? Developers,
    [property: JsonPropertyName("publishers")] List<GiantBombCompanyRef>? Publishers,
    [property: JsonPropertyName("original_release_date")] string? OriginalReleaseDate,
    [property: JsonPropertyName("description")] string? Description,
    [property: JsonPropertyName("original_game_rating")] List<GiantBombRatingRef>? Ratings
  );
  
  public record GiantBombNameRef([property: JsonPropertyName("name")] string Name);
  public record GiantBombCompanyRef([property: JsonPropertyName("name")] string Name);
  public record GiantBombRatingRef([property: JsonPropertyName("name")] string Name);
  ```

**Configuration Model** (e.g., `GiantBombConfiguration.cs`)
```csharp
public class GiantBombConfiguration
{
  public string BaseUrl { get; set; } = "https://www.giantbomb.com/api/";
  public string ApiKey { get; set; } = string.Empty;
  public int Timeout { get; set; } = 30;
}
```

**LookupApi.cs Return Type Update** (`MediaSet.Api/Lookup/LookupApi.cs`)
- Current: `Results<Ok<BookResponse>, Ok<MovieResponse>, NotFound, BadRequest<string>>`
- Updated: `Results<Ok<BookResponse>, Ok<MovieResponse>, Ok<GameResponse>, NotFound, BadRequest<string>>`
- Add Games case to switch statement:
  ```csharp
  MediaTypes.Games => await strategyFactory.GetStrategy<GameResponse>(parsedEntityType, parsedIdentifierType)
      .LookupAsync(parsedIdentifierType, identifierValue, cancellationToken),
  ```
- Add GameResponse case to result mapping:
  ```csharp
  GameResponse game => TypedResults.Ok(game),
  ```

#### New/Modified Services

**New: GameLookupStrategy.cs** (`MediaSet.Api/Services/GameLookupStrategy.cs`)
- Implements `ILookupStrategy<GameResponse>`
- Dependencies: `IUpcItemDbClient`, `IGiantBombClient`, `ILogger<GameLookupStrategy>`
- Supported identifiers: UPC, EAN
- Methods:
  ```csharp
  public bool CanHandle(MediaTypes entityType, IdentifierType identifierType)
  {
      return entityType == MediaTypes.Games && 
             (identifierType == IdentifierType.Upc || identifierType == IdentifierType.Ean);
  }
  
  public async Task<GameResponse?> LookupAsync(
      IdentifierType identifierType, 
      string identifierValue, 
      CancellationToken cancellationToken)
  {
      // 1. Query UPCitemdb for barcode
      var upcResult = await _upcItemDbClient.GetItemByCodeAsync(identifierValue, cancellationToken);
      if (upcResult == null || upcResult.Items.Count == 0)
          return null;
      
      var firstItem = upcResult.Items[0];
      if (string.IsNullOrEmpty(firstItem.Title))
          return null;
      
    // 2. Clean game title and extract edition for response; strip edition from search
    var (cleanedTitle, edition) = CleanGameTitleAndExtractEdition(firstItem.Title);
    var format = ExtractGameFormat(firstItem.Title);
    var platform = ExtractPlatformFromBarcode(firstItem.Title, firstItem.Category, firstItem.Brand, firstItem.Model);
      
    // 3. Search GiantBomb API for title (edition stripped)
    var searchResult = await _giantBombClient.SearchGameAsync(cleanedTitle, cancellationToken);
      if (searchResult == null || searchResult.Count == 0)
          return null;
      
    // 4. Get detailed game information
    var gameDetails = await _giantBombClient.GetGameDetailsAsync(searchResult[0].Id, cancellationToken);
      if (gameDetails == null)
          return null;
      
    // 5. Map to GameResponse; add edition back to title and use barcode-derived platform
    return MapToGameResponse(gameDetails, format, platform, edition);
  }
  ```
- Helper methods:
  - `CleanGameTitleAndExtractEdition(string rawTitle)` - remove platform indicators, editions, format markers for search and return edition
  - `ExtractGameFormat(string rawTitle)` - extract physical format (Disc, Cartridge, Digital)
  - `ExtractPlatformFromBarcode(...)` - extract platform from barcode/UPCitemdb data
  - `MapToGameResponse(GiantBombGameDetails details, string format, string platform, string edition)` - map API response to GameResponse

#### New/Modified Clients

**New: IGiantBombClient.cs** (`MediaSet.Api/Clients/IGiantBombClient.cs`)
- Interface for GiantBomb API
- Methods:
  ```csharp
  public interface IGiantBombClient
  {
      Task<List<GiantBombSearchResult>?> SearchGameAsync(string title, CancellationToken cancellationToken);
      Task<GiantBombGameDetails?> GetGameDetailsAsync(int id, CancellationToken cancellationToken);
  }
  ```

**New: GiantBombClient.cs** (`MediaSet.Api/Clients/GiantBombClient.cs`)
- HTTP client for GiantBomb API
- Configuration: `GiantBombConfiguration` (base URL, API key, timeout)
- Endpoints:
  - Search: `GET search/?api_key=...&format=json&resources=game&query={title}`
  - Details: `GET game/{guid}/?api_key=...&format=json` (or use `api_detail_url` from search result)
- Response mapping:
  - Parse JSON responses (status_code, error, results)
  - Extract fields: name, original_release_date, genres, developers, publishers, description (deck/description)
  - Map ratings from `original_game_rating` if present
- Error handling:
  - Handle invalid API key (status_code 100), not found (101), and rate limit/format errors

#### New/Modified API Endpoints

**LookupApi.cs** (`MediaSet.Api/Lookup/LookupApi.cs`)

**Endpoint**: `GET /lookup/{entityType}/{identifierType}/{identifierValue}`
- No signature changes, just extension
- Add validation for `entityType=games`
- Add case for `MediaTypes.Games` in switch statement
- Update typed result union to include `Ok<GameResponse>`
- Update Swagger documentation tags

Implementation additions:
```csharp
// In validation section, update valid types message:
return TypedResults.BadRequest($"Invalid entity type: {entityType}. Valid types are: Books, Movies, Games");

// In switch statement:
MediaTypes.Games => await strategyFactory.GetStrategy<GameResponse>(parsedEntityType, parsedIdentifierType)
    .LookupAsync(parsedIdentifierType, identifierValue, cancellationToken),

// In result mapping:
GameResponse game => TypedResults.Ok(game),
```

#### Database Changes

- No database schema changes required
- Game model already has `Barcode` property
- No migrations needed

#### Configuration Changes

**appsettings.json** updates:
```json
{
  "GiantBomb": {
    "BaseUrl": "https://www.giantbomb.com/api/",
    "ApiKey": "",
    "Timeout": 30
  }
}
```

**Program.cs** updates:
```csharp
// Register game metadata API configuration
builder.Services.Configure<GiantBombConfiguration>(builder.Configuration.GetSection("GiantBomb"));

// Register game metadata API client
builder.Services.AddHttpClient<IGiantBombClient, GiantBombClient>();

// Register game lookup strategy
builder.Services.AddScoped<ILookupStrategy<GameResponse>, GameLookupStrategy>();
```

### Frontend Changes (MediaSet.Remix)

#### Modified Routes

**Route**: `/games/add` and `/games/:id/edit` (`MediaSet.Remix/app/routes/games_.add/route.tsx`, `MediaSet.Remix/app/routes/games_.$id.edit/route.tsx`)
- Updates:
  - Add "Lookup" button next to Barcode input field (inline, not separate section)
  - Button triggers lookup and populates form fields with results
  - User can edit populated fields before saving
  - Action handler: distinguish between "lookup" intent and "save" intent
  - Follow same pattern as books and movies
  
Example code structure (action handler):
```typescript
export async function action({ request, params }: ActionFunctionArgs) {
  const formData = await request.formData();
  const intent = formData.get("intent");
  
  if (intent === "lookup") {
    const barcode = formData.get("barcode") as string;
    if (!barcode) {
      return json({ error: "Barcode is required for lookup" });
    }
    
    const result = await lookup(Entity.Games, "upc", barcode);
    
    if ("error" in result) {
      return json({ lookupError: result.error });
    }
    
    // Return lookup results to populate form
    return json({ lookupResult: result });
  }
  
  // Handle save intent...
}
```

Example JSX structure:
```tsx
<div className="mb-4">
  <label htmlFor="barcode" className="block text-sm font-medium text-gray-700">
    Barcode
  </label>
  <div className="mt-1 flex rounded-md shadow-sm">
    <input
      type="text"
      name="barcode"
      id="barcode"
      value={game.barcode}
      onChange={(e) => handleChange("barcode", e.target.value)}
      className="flex-1 rounded-l-md border-gray-300"
    />
    <button
      type="submit"
      name="intent"
      value="lookup"
      disabled={navigation.state === "submitting"}
      className="inline-flex items-center px-4 py-2 border border-l-0 border-gray-300 rounded-r-md bg-gray-50 hover:bg-gray-100"
    >
      {navigation.state === "submitting" ? "Looking up..." : "Lookup"}
    </button>
  </div>
  {actionData?.lookupError && (
    <p className="mt-2 text-sm text-red-600">{actionData.lookupError}</p>
  )}
</div>
```

#### New/Modified Data Functions

**lookup-data.ts** (`MediaSet.Remix/app/lookup-data.ts`)

**Function**: `lookup(entityType: Entity, identifierType: string, identifierValue: string)`
- Updates:
  - Extend return type: `BookEntity | MovieEntity | GameEntity | LookupError`
  - No other changes needed (already supports entity type parameter)
  - API endpoint already supports games

Type guard addition:
```typescript
export function isGameEntity(entity: any): entity is GameEntity {
  return entity && "platform" in entity && "developers" in entity;
}
```

#### Type Definitions

**models.ts** (`MediaSet.Remix/app/models.ts`)

Add new type:
```typescript
export type GameLookupResponse = {
  title: string;
  platform: string;
  platforms: string[];
  genres: string[];
  developers: string[];
  publishers: string[];
  releaseDate: string;
  rating: string;
  description: string;
  format: string;
};
```

Extend lookup return type:
```typescript
export type LookupResult = BookEntity | MovieEntity | GameEntity | LookupError;
```

### Testing Changes

#### Backend Tests (MediaSet.Api.Tests)

**Test Class**: `GameLookupStrategyTests.cs` (`MediaSet.Api.Tests/Services/GameLookupStrategyTests.cs`)
- Test scenarios:
  - `LookupAsync_WithValidUpc_ReturnsGameResponse`
  - `LookupAsync_WithValidEan_ReturnsGameResponse`
  - `LookupAsync_WithInvalidUpc_ReturnsNull`
  - `LookupAsync_WithNoGameApiResults_ReturnsNull`
  - `LookupAsync_ExtractsFormatFromTitle`
  - `CanHandle_WithGamesAndUpc_ReturnsTrue`
  - `CanHandle_WithGamesAndEan_ReturnsTrue`
  - `CanHandle_WithBooksAndUpc_ReturnsFalse`
  - `CanHandle_WithMoviesAndUpc_ReturnsFalse`
  - `CleanGameTitle_RemovesPlatformSuffixes`
  - `CleanGameTitle_RemovesEditionMarkers`
  - `ExtractGameFormat_ReturnsDiscForDiscGames`
  - `ExtractGameFormat_ReturnsCartridgeForCartridgeGames`
- Mock dependencies: `IUpcItemDbClient`, `IIgdbClient`
- Edge cases:
  - UPC lookup returns non-game item
  - Multiple game API matches (auto-select first)
  - Game API returns incomplete data
  - Title cleaning removes too much information

**Test Class**: `GiantBombClientTests.cs` (`MediaSet.Api.Tests/Clients/GiantBombClientTests.cs`)
- Test scenarios:
  - `SearchGameAsync_WithTitle_ReturnsResults`
  - `SearchGameAsync_WithNoResults_ReturnsEmptyList`
  - `GetGameDetailsAsync_WithId_ReturnsDetails`
  - `GetGameDetailsAsync_WithInvalidId_ReturnsNull`
  - HTTP error handling (401, 429, 500)
- Mock HTTP responses with realistic JSON

**Test Class**: `LookupApiTests.cs` (updates) (`MediaSet.Api.Tests/Lookup/LookupApiTests.cs`)
- New test scenarios:
  - `GetLookup_WithGamesAndUpc_ReturnsGame`
  - `GetLookup_WithGamesAndEan_ReturnsGame`
  - `GetLookup_WithGamesAndIsbn_ReturnsBadRequest`
  - `GetLookup_WithInvalidGameBarcode_ReturnsNotFound`

**Test Class**: `LookupStrategyFactoryTests.cs` (updates) (`MediaSet.Api.Tests/Services/LookupStrategyFactoryTests.cs`)
- New test scenario:
  - `GetStrategy_WithGamesAndUpc_ReturnsGameStrategy`

#### Frontend Tests (MediaSet.Remix)

Removed for now (not in scope for this iteration).

#### Integration Tests

**End-to-end scenarios**:
1. Game UPC lookup → resolves to title → game API search → displays game data
2. Game EAN lookup → same flow
3. Invalid game barcode → displays error
4. UPC lookup returns non-game item → displays appropriate error
5. Game API timeout → displays error message
6. Lookup populates all form fields correctly
7. User can edit lookup results before saving

**API contract validation**:
- Validate IGDB/RAWG API response structure
- Ensure response mapping doesn't lose data
- Test with real API keys in integration environment
- Verify rate limiting behavior

## Implementation Steps

1. **Confirm game metadata API**
  - Use GiantBomb Search API for game metadata lookup
  - Document API key setup and link to docs

2. **Create GiantBomb API client**
  - Create configuration model (`GiantBombConfiguration`)
  - Create interface `IGiantBombClient` with search and details methods
  - Implement `GiantBombClient` with HTTP client (API key header/param, format=json)
  - Add response models (`GiantBombSearchResult`, `GiantBombGameDetails`)
  - Write unit tests with mocked HTTP responses
  - Test with real API key (manual/integration test)

3. **Create GameResponse model**
   - Define record in `MediaSet.Api/Models/GameResponse.cs`
   - Include all game properties matching Game entity
   - Add XML documentation comments

4. **Implement GameLookupStrategy**
   - Create `GameLookupStrategy.cs` implementing `ILookupStrategy<GameResponse>`
  - Inject dependencies: `IUpcItemDbClient`, `IGiantBombClient`, `ILogger`
   - Implement `CanHandle` method (Games + UPC/EAN)
  - Implement `LookupAsync` method (UPC → title → GiantBomb search → response)
  - Add helper methods: `CleanGameTitleAndExtractEdition`, `ExtractGameFormat`, `ExtractPlatformFromBarcode`, `MapToGameResponse`
   - Write comprehensive unit tests
   - Register in DI container

5. **Update LookupApi endpoint**
   - Add Games case to switch statement
   - Update return type to include `Ok<GameResponse>`
   - Add GameResponse case to result mapping
   - Update validation error message to include Games
   - Update Swagger documentation

6. **Update backend configuration**
   - Add Igdb configuration section to `appsettings.json`
   - Register configuration in `Program.cs`
   - Register `IIgdbClient` and `IgdbClient` with HTTP client factory
   - Register `GameLookupStrategy` as scoped service
   - Document API key requirement in README

7. **Create/update backend tests**
   - Write `GameLookupStrategyTests` with all scenarios
  - Write `GiantBombClientTests` with API tests
   - Update `LookupApiTests` with game scenarios
   - Update `LookupStrategyFactoryTests` with game strategy resolution
   - Ensure >80% code coverage

8. **Update frontend TypeScript models**
   - Add `GameLookupResponse` type to `models.ts`
   - Extend `LookupResult` type to include `GameEntity`
   - Add `isGameEntity` type guard

9. **Update lookup data function**
   - Extend return type to include `GameEntity`
   - No other changes needed (already supports games entity type)
   - Update tests to cover game lookup

10. **Update game add route**
    - Add lookup button next to Barcode field in form
    - Update action handler to support "lookup" intent
    - Populate form fields with lookup results
    - Display lookup errors inline
    - Test form prefill and edit functionality

11. **Update game edit route**
    - Add lookup button next to Barcode field in form
    - Update action handler to support "lookup" intent
    - Populate form fields with lookup results
    - Preserve existing data if lookup fails
    - Test form prefill and edit functionality

12. [Removed] Frontend tests are out of scope for this iteration

13. **Integration testing**
    - Test game UPC lookup from add form end-to-end
    - Test game UPC lookup from edit form end-to-end
    - Test error scenarios (invalid codes, API failures)
    - Test that lookup results can be edited before saving
    - Test with various game barcodes (different platforms, formats)

14. **Documentation updates**
    - Update README with game barcode lookup capability
    - Document IGDB API key setup instructions
    - Add example game UPC codes for testing
    - Update API documentation with games support
    - Note any limitations (e.g., not all game barcodes in UPCitemdb)

## Acceptance Criteria

- [ ] Backend: Game metadata API client (IGDB or RAWG) successfully queries API
- [ ] Backend: `GameLookupStrategy` implements `ILookupStrategy<GameResponse>`
- [ ] Backend: `GameLookupStrategy` handles UPC/EAN lookups correctly
- [ ] Backend: Lookup API endpoint supports `entityType=games` with `identifierType=upc|ean`
- [ ] Backend: API returns strongly-typed `GameResponse`
- [ ] Backend: Helper methods clean game titles appropriately (remove platform suffixes, etc.)
- [ ] Backend: Helper methods extract game format from UPC title
- [ ] Backend: All unit tests pass with >80% coverage
- [ ] Backend: `LookupStrategyFactory` resolves `GameLookupStrategy` for games
- [ ] Configuration: API key/credentials configured in appsettings.json
- [ ] Frontend: `GameLookupResponse` type defined in models.ts
- [ ] Frontend: Lookup data function returns `GameEntity` for game lookups
- [ ] Frontend: Game add form has lookup button on Barcode field
- [ ] Frontend: Game edit form has lookup button on Barcode field
- [ ] Frontend: Lookup populates all game form fields correctly
- [ ] Frontend: User can edit lookup results before saving
- [ ] Frontend: Lookup errors display inline on form
- [ ] Frontend: All component tests pass
- [ ] Integration: Game UPC lookup from add form works end-to-end
- [ ] Integration: Game UPC lookup from edit form works end-to-end
- [ ] Integration: UPC → UPCitemdb → title → IGDB → game metadata flow works
- [ ] Integration: Lookup results populate form fields correctly (title, platform, genres, developers, publishers, release date, rating, description, format)
- [ ] Integration: Invalid barcodes display helpful error messages
- [ ] Integration: API failures handled gracefully
- [ ] Documentation: README updated with game barcode lookup feature
- [ ] Documentation: API key setup instructions documented

## Risks and Mitigations

| Risk | Impact | Likelihood | Mitigation |
|------|--------|------------|------------|
| Game barcodes not in UPCitemdb database | High | High | Clear messaging that not all barcodes are in database; provide manual entry option; suggest alternative lookup methods (title search) |
| UPCitemdb rate limits (100 req/day free tier) | Medium | Medium | Shared with movie lookups; implement caching; monitor usage; consider paid tier ($20/mo unlimited); use sparingly with clear user messaging |
| GiantBomb API key required | Medium | Low | Document API key setup; handle errors for invalid key (status_code 100); respect fair use policies |
| Game API returns multiple matches for title | Medium | High | Auto-select first match (best relevance); consider adding platform filtering; log when multiple matches found for future improvement |
| Game title from UPC is ambiguous | Medium | Medium | Implement title cleaning heuristics; test with variety of game barcodes; allow manual selection from multiple results (future enhancement) |
| Game API rate limits | Low | Low | Implement client-side rate limiting and caching; observe GiantBomb fair use guidelines |
| Platform mismatch (UPC for Xbox, API returns PlayStation) | Medium | Medium | Trust UPC data for format; use API for metadata; display all platforms from API; let user select correct one |
| ESRB rating mapping differences between APIs | Low | Medium | Normalize ratings (E, T, M, etc.); handle different rating systems (PEGI, etc.); default to empty if unmappable |
| Game API authentication complexity (OAuth for IGDB) | Medium | Medium | Implement token caching and refresh; clear error messages; document troubleshooting steps; ensure client ID/secret properly configured |
| UPC lookup returns wrong product type (e.g., toy, accessory) | Medium | Medium | Validate product category from UPC response; filter by category if available; display mismatch warning to user |

## Open Questions

1. **Which game metadata API should we use?**
   - Resolved: Use the GiantBomb API (Search endpoint) for this feature.

2. **How should we handle games with multiple platforms?**
   - Example: "The Legend of Zelda" exists on Switch, Wii U, etc.
   - UPC barcode is specific to one platform, but API may return multiple
   - Options:
     - A: Auto-select first platform from API
     - B: Use platform hint from UPC title (e.g., "Nintendo Switch" in title)
     - C: Display all platforms, let user select
   - **Decision**: Option B. Extract platform from the barcode/UPCitemdb result and use it for the GameResponse `Platform` field. Do not use the platform(s) from metadata. This ensures the platform matches the scanned product.

3. **Should we implement game title search as fallback if UPC lookup fails?**
   - Benefit: Allows lookup without barcode
   - Complexity: Requires UI for title input and result selection
   - **Recommendation**: Out of scope for this feature. Add as future enhancement. Focus on barcode lookup first.

4. **How should we handle game editions (Standard, Deluxe, GOTY, etc.)?**
   - UPC title may include edition ("Game Title - Deluxe Edition")
   - Game API may or may not distinguish editions
   - Options:
     - A: Ignore edition in API lookup, include in format field
     - B: Try to match edition in API search
     - C: Strip edition from search, add to description
   - **Decision**: Strip the edition from the search query to improve search accuracy, but add it back to the `Title` in the GameResponse so the title matches the barcode.

5. **Should we cache game metadata lookups?**
   - Given rate limits, caching would be valuable
   - Question: Cache duration? Storage mechanism (memory, database)?
   - **Recommendation**: Implement in-memory cache with 24-hour TTL, similar to other lookups. Evaluate for database cache if needed later.

6. **How should we handle region-specific ratings (ESRB vs PEGI vs CERO)?**
   - APIs may return multiple rating systems
   - Different regions use different standards
   - Options:
     - A: Display all ratings
     - B: Prefer ESRB, fallback to others
     - C: Detect user region and show appropriate rating
   - **Recommendation**: Option B. Store ESRB rating in Rating field, display others in description if needed. Most consistent with US-centric UPCitemdb.

7. **Should we support game-specific identifier types (e.g., GTIN, UPC-A, UPC-E)?**
   - UPC and EAN cover most cases
   - GTIN is superset of UPC/EAN
   - **Recommendation**: UPC and EAN sufficient for MVP. No new identifier types needed.

8. **How should we handle digital-only games (no physical barcode)?**
   - Digital games won't have UPC barcodes
   - Options:
     - A: Out of scope (barcode lookup only)
     - B: Add title search capability
     - C: Allow lookup by digital store ID
   - **Recommendation**: Option A. This feature is specifically for barcode lookup. Digital games added manually.

## Dependencies

### External Libraries/Packages

**Backend (.NET)**
- No new packages required (use existing HttpClient, System.Text.Json)
- Optional: `OneOf` library if discriminated unions needed

**Frontend (TypeScript/React)**
- No new packages required

### Third-Party API Integrations

**GiantBomb**
- API key required (sign up via GiantBomb account)
- Use Search endpoint with `resources=game`
- Documentation: https://www.giantbomb.com/api/documentation/#toc-0-41

**UPCitemdb.com** (existing)
- Already integrated for movie lookups
- Will be reused for game barcode identification
- Rate limits shared across movies and games

### Infrastructure Changes

**Configuration Management**
- Add game API credentials to environment variables or secure configuration
- Update deployment documentation

**Caching (optional for MVP)**
- If implementing cache, use existing in-memory cache pattern
- Consider Redis for production if needed

## References

### Similar Implementations
- `MovieLookupStrategy.cs` - very similar pattern (UPC → metadata API)
- `BookLookupStrategy.cs` - strategy pattern, multiple identifier types
- Existing `LookupStrategyFactory.cs` - factory pattern to follow

### API Documentation
- GiantBomb Search: https://www.giantbomb.com/api/documentation/#toc-0-41
- UPCitemdb API docs: https://www.upcitemdb.com/api/ (already integrated)

### Game Database Resources
- IGDB game example: https://www.igdb.com/games/the-legend-of-zelda-breath-of-the-wild
- RAWG game example: https://rawg.io/games/the-legend-of-zelda-breath-of-the-wild
- Comparison: Both have excellent coverage for modern games

### Related Issues/Design Docs
- GitHub Issue #187: Game barcode lookup
- Existing barcode lookup implementation plan: `/ImplementationDetails/BARCODE_LOOKUP_STRATEGY_PATTERN_IMPLEMENTATION.md`
- Current lookup implementation: `MediaSet.Api/Lookup/LookupApi.cs`
- Game entity model: `MediaSet.Api/Models/Game.cs`

### Code Style Guidelines
- Backend: `/.github/code-style-api.md`
- Frontend: `/.github/code-style-ui.md`
