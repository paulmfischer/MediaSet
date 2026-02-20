# Title Lookup Implementation Plan

## Overview

This feature adds the ability for users to search for media metadata by title, complementing the existing barcode/identifier lookup. When a user types a title and clicks "Lookup", the API searches the relevant external service and returns up to 10 matching results. The UI then decides what to do with the array: for a barcode lookup it always takes the first result and populates the form; for a title lookup it shows a selection dialog if there are multiple results, otherwise auto-populates.

The implementation reuses the existing strategy pattern and endpoint — no new interface methods, no new factory methods, and no new API endpoint. The key changes are:
- Add `Title` as a new `IdentifierType`
- Change `LookupAsync` return type from `TResponse?` to `IReadOnlyList<TResponse>` (breaking change, propagated through to the API response)
- Each strategy branches internally on `IdentifierType.Title` vs identifier-based lookup
- The existing endpoint now always returns an array

Additionally, the **barcode input will be repositioned** to appear after the title input on all four entity forms, per the issue requirement.

Related issue: [#503 — Create the ability to add by title lookup](https://github.com/paulmfischer/MediaSet/issues/503)

## Related Existing Functionality

### Backend Components

**Lookup Strategy Interface** (`MediaSet.Api/Infrastructure/Lookup/Strategies/ILookupStrategy.cs`)
- `ILookupStrategyBase.CanHandle(MediaTypes, IdentifierType)` — strategy selection predicate; will be updated to accept `IdentifierType.Title`
- `ILookupStrategy<TResponse>.LookupAsync(...)` — currently returns `TResponse?`; return type changes to `IReadOnlyList<TResponse>`

**Existing strategy implementations** (all in `MediaSet.Api/Infrastructure/Lookup/Strategies/`)
- `BookLookupStrategy` — uses `IOpenLibraryClient` and `IUpcItemDbClient`
- `MovieLookupStrategy` — uses `IUpcItemDbClient` then `ITmdbClient.SearchAsync(title)`; the TMDB title search already exists internally
- `GameLookupStrategy` — uses `IUpcItemDbClient` then IGDB title search; the IGDB title search already exists internally
- `MusicLookupStrategy` — uses `IMusicBrainzClient` (barcode search)

**LookupStrategyFactory** (`MediaSet.Api/Infrastructure/Lookup/Strategies/LookupStrategyFactory.cs`)
- Resolves the correct strategy via `CanHandle` at runtime
- No changes required — `CanHandle` already takes `IdentifierType`, so `Title` is handled automatically once strategies declare they can handle it

**Lookup API endpoint** (`MediaSet.Api/Features/Lookup/Endpoints/LookupApi.cs`)
- Existing: `GET /lookup/{entityType}/{identifierType}/{identifierValue}` — currently returns single result
- After change: same route, returns `IReadOnlyList<T>` (breaking change)
- `identifierType` of `title` is now valid; `identifierValue` carries the title string

**Response models** (`MediaSet.Api/Infrastructure/Lookup/Models/`)
- `BookResponse`, `MovieResponse`, `GameResponse`, `MusicResponse` — unchanged, reused as array elements

**`IdentifierType` enum** (`MediaSet.Api/Infrastructure/Lookup/Models/IdentifierType.cs`)
- Currently: `Isbn`, `Lccn`, `Oclc`, `Olid`, `Upc`, `Ean`
- Will add: `Title`

### Frontend Components

**lookup-data.server.ts** (`MediaSet.Remix/app/api/lookup-data.server.ts`)
- `lookup()` — currently calls the endpoint and returns a single entity; will be updated to return an array
- Callers decide what to do with the array based on what triggered the lookup (barcode vs title)

**Entity form components** (`MediaSet.Remix/app/components/`)
- `book-form.tsx`, `movie-form.tsx`, `game-form.tsx`, `music-form.tsx`
- Each has a barcode/ISBN input with Lookup + Scan buttons
- Title input currently has no lookup capability
- Field order will change: title → barcode (per issue)

**Route action handlers**
- `MediaSet.Remix/app/routes/$entity.$entityId_.edit/route.tsx` — handles `intent="lookup"`, returns `lookupResult` to form
- `MediaSet.Remix/app/routes/$entity_.add/route.tsx` — same pattern
- Both will use the same `intent="lookup"` for title lookup; the `fieldName` distinguishes barcode from title at the call site

## Requirements

### Functional Requirements

1. **`IdentifierType.Title` support**
   - Add `Title` to the `IdentifierType` enum
   - All four strategies declare `CanHandle` true for their entity type + `IdentifierType.Title`
   - `LookupAsync` branches internally: `IdentifierType.Title` → title search path; all other types → existing identifier path

2. **Return type change (breaking)**
   - `LookupAsync` returns `IReadOnlyList<TResponse>` instead of `TResponse?`
   - Identifier-based lookups (barcode, ISBN, etc.) return a single-element list on success or empty list on not-found
   - Title-based lookups return up to 10 results
   - The API endpoint returns the array directly

3. **Title search per entity type**
   - **Books** — search OpenLibrary `/search.json?title=` endpoint; map each result to `BookResponse`
   - **Movies** — call `ITmdbClient.SearchAsync(title)` directly, bypassing UpcItemDb; map each result to `MovieResponse`
   - **Games** — call IGDB title search directly, bypassing UpcItemDb and dropping best-match scoring; map each result to `GameResponse`
   - **Music** — search MusicBrainz release by title; map each result to `MusicResponse`
   - All strategies cap title search results at 10

4. **Frontend barcode lookup behavior (unchanged)**
   - API now returns an array; frontend takes `results[0]` and populates the form
   - Same re-mount key trick as today

5. **Frontend title lookup behavior**
   - Multiple results → show `TitleLookupResultsDialog` modal; user selects a row → form populated
   - Single result → auto-populate form (same as barcode)
   - Zero results → show error at the top of the form (reuses existing lookup error display behavior)

6. **Form field reordering**
   - On all four forms: title input (with new Lookup button) must appear **before** barcode input

7. **Lookup button on title field**
   - Shown conditionally using the same `*LookupAvailable` props already passed to forms
   - Enter key on title field triggers title lookup (same keyboard pattern as barcode field)

### Non-Functional Requirements

- No new interface methods, factory methods, or API endpoints
- No new HTTP clients required
- No new npm packages or NuGet packages required
- Follow existing code style per `.github/instructions/`

## Proposed Changes

### Backend Changes (MediaSet.Api)

#### Modified `IdentifierType` Enum

**`IdentifierType.cs`** (`MediaSet.Api/Infrastructure/Lookup/Models/IdentifierType.cs`)

Add a new value:

```csharp
[Description("title")]
Title,
```

Update `TryParseIdentifierType` (or equivalent extension) to parse `"title"` → `IdentifierType.Title`.

#### Modified Interface

**`ILookupStrategy.cs`** (`MediaSet.Api/Infrastructure/Lookup/Strategies/ILookupStrategy.cs`)

Change the return type of `LookupAsync` from `Task<TResponse?>` to `Task<IReadOnlyList<TResponse>>`. No new methods. No new predicate on `ILookupStrategyBase`.

```csharp
Task<IReadOnlyList<TResponse>> LookupAsync(IdentifierType identifierType, string identifierValue, CancellationToken cancellationToken);
```

#### Modified Strategy Implementations

**`BookLookupStrategy`**
- `CanHandle`: add `IdentifierType.Title` to the set of handled identifier types
- `LookupAsync`:
  - When `identifierType == IdentifierType.Title`: call `IOpenLibraryClient` title search (`/search.json?title={identifierValue}&limit=10`); map `docs` array to `IReadOnlyList<BookResponse>`
  - All other identifier types: existing logic, wrapped in a single-element list on success or empty list on not-found
- `IOpenLibraryClient` needs a new `SearchByTitleAsync(string title, CancellationToken)` method added to the interface and implementation

**`MovieLookupStrategy`**
- `CanHandle`: add `IdentifierType.Title`
- `LookupAsync`:
  - When `identifierType == IdentifierType.Title`: call `ITmdbClient.SearchAsync(identifierValue)` directly (no UpcItemDb stage); map each result to `MovieResponse`; return up to 10
  - All other identifier types: existing UPC → UpcItemDb → TMDB logic, wrapped in a single-element list or empty list
- No changes to `ITmdbClient` — `SearchAsync` already returns a list internally

**`GameLookupStrategy`**
- `CanHandle`: add `IdentifierType.Title`
- `LookupAsync`:
  - When `identifierType == IdentifierType.Title`: call IGDB title search directly (no UpcItemDb stage, no best-match scoring filter); map each result to `GameResponse`; return up to 10
  - All other identifier types: existing UPC → UpcItemDb → IGDB logic, wrapped in a single-element list or empty list
- No changes to `IIgdbClient`

**`MusicLookupStrategy`**
- `CanHandle`: add `IdentifierType.Title`
- `LookupAsync`:
  - When `identifierType == IdentifierType.Title`: call `IMusicBrainzClient` release title search; map each result to `MusicResponse`; return up to 10
  - All other identifier types: existing barcode → MusicBrainz logic, wrapped in a single-element list or empty list
- `IMusicBrainzClient` needs a new `SearchByTitleAsync(string title, CancellationToken)` method added to the interface and implementation

#### Modified LookupStrategyFactory

**`LookupStrategyFactory`** (`MediaSet.Api/Infrastructure/Lookup/Strategies/LookupStrategyFactory.cs`)

No changes required. `CanHandle(entityType, IdentifierType.Title)` will resolve correctly once the strategies are updated.

#### Modified API Endpoint

**`LookupApi.cs`** (`MediaSet.Api/Features/Lookup/Endpoints/LookupApi.cs`)

The existing `GET /lookup/{entityType}/{identifierType}/{identifierValue}` endpoint:
- No route change
- `identifierType` of `"title"` is now valid (parsed to `IdentifierType.Title`)
- Return type changes from single `T?` to `IReadOnlyList<T>` — return `200 OK` with the array directly
- Empty array replaces the previous `404 Not Found` for no results (simplifies frontend handling)
- `400 Bad Request` for invalid `entityType` or `identifierType` unchanged

This is a **breaking change** to the API contract.

### Frontend Changes (MediaSet.Remix)

#### Modified Data Functions

**`lookup-data.server.ts`** (`MediaSet.Remix/app/api/lookup-data.server.ts`)

Update `lookup()` (or `getIdentifierTypeForField()` + `lookup()`) to:
- Accept the same parameters as today
- Map `identifierType` of `"title"` for title field lookups
- Deserialise the response as an array and return `Array<EntityType> | LookupError`

No separate `searchByTitle()` function is needed — `lookup()` with `fieldName="title"` and `identifierType="title"` handles it.

#### New Component: `TitleLookupResultsDialog`

**`MediaSet.Remix/app/components/title-lookup-results-dialog.tsx`** (new file)

A modal dialog that:
- Accepts `results`, `onSelect(result)`, and `onClose()` props
- Renders a scrollable list of up to 10 result rows
- Per-entity row display:
  - **Books**: title, author(s), publish date
  - **Movies**: title, release date, genres
  - **Games**: title, platform, release date
  - **Music**: title, artist, release date
- Clicking a row calls `onSelect(result)` — parent handles form population and dialog close
- Dismissing (close button or Escape key) calls `onClose()`
- Uses existing dialog/modal styling patterns from the codebase

#### Modified Form Components

All four form components need two changes: add a title Lookup button and reorder barcode below title.

**`book-form.tsx`**
- Add a "Lookup" button next to the title field (shown when `isbnLookupAvailable` is true)
- Move the ISBN/barcode input section to appear **after** the title input section
- Wire up `handleTitleLookup()` that submits with `intent="lookup"`, `fieldName="title"`, `identifierValue={title}`
- Add `onKeyDown` to title input that calls `handleTitleLookup()` on Enter

**`movie-form.tsx`**, **`game-form.tsx`**, **`music-form.tsx`**
- Same changes: title Lookup button (gated on `barcodeLookupAvailable`), barcode field moved below title, keyboard + button handlers

#### Modified Route Action Handlers

Both `$entity.$entityId_.edit/route.tsx` and `$entity_.add/route.tsx`.

The existing `intent="lookup"` branch already calls `lookup()` and returns the result. After this change:
- `lookup()` always returns an array
- Action returns `{ lookupResults: Array<EntityType>, fieldName, identifierValue, lookupTimestamp }`
- Component reads `lookupResults` from `actionData` and inspects `fieldName` to determine context:
  - `fieldName !== "title"` (barcode/ISBN): take `lookupResults[0]`, re-mount form (same as today but now always from array)
  - `fieldName === "title"` and `lookupResults.length > 1`: render `<TitleLookupResultsDialog>`
  - `fieldName === "title"` and `lookupResults.length === 1`: take `lookupResults[0]`, re-mount form
  - `lookupResults.length === 0`: display error at the top of the form (reuse existing lookup error display behavior)
- `onSelect(result)` handler: merge selection into form state using the re-mount key trick

### Testing Changes

#### Backend Tests (MediaSet.Api.Tests)

**`BookLookupStrategyTests`** (existing, extend)
- `LookupAsync_WithTitle_ReturnsUpToTenResults`
- `LookupAsync_WithTitle_ReturnsEmptyListWhenNotFound`
- `LookupAsync_WithTitle_MapsFieldsCorrectly`
- `LookupAsync_WithIsbn_ReturnsSingleElementList`
- `LookupAsync_WithIsbn_ReturnsEmptyListWhenNotFound`
- `CanHandle_WithBooksAndTitle_ReturnsTrue`

**`MovieLookupStrategyTests`** (existing, extend)
- `LookupAsync_WithTitle_ReturnsUpToTenResults`
- `LookupAsync_WithTitle_BypassesUpcItemDb`
- `LookupAsync_WithTitle_ReturnsEmptyListWhenNotFound`
- `LookupAsync_WithUpc_ReturnsSingleElementList`
- `CanHandle_WithMoviesAndTitle_ReturnsTrue`

**`GameLookupStrategyTests`** (existing, extend)
- `LookupAsync_WithTitle_ReturnsUpToTenResults`
- `LookupAsync_WithTitle_DoesNotApplyBestMatchScoring`
- `LookupAsync_WithTitle_ReturnsEmptyListWhenNotFound`
- `LookupAsync_WithUpc_ReturnsSingleElementList`
- `CanHandle_WithGamesAndTitle_ReturnsTrue`

**`MusicLookupStrategyTests`** (existing, extend)
- `LookupAsync_WithTitle_ReturnsUpToTenResults`
- `LookupAsync_WithTitle_ReturnsEmptyListWhenNotFound`
- `LookupAsync_WithUpc_ReturnsSingleElementList`
- `CanHandle_WithMusicsAndTitle_ReturnsTrue`

**`LookupApiTests`** (existing, extend)
- `GetLookup_WithBooksAndTitle_ReturnsArray`
- `GetLookup_WithMoviesAndTitle_ReturnsArray`
- `GetLookup_WithBooksAndIsbn_ReturnsSingleElementArray`
- `GetLookup_WithInvalidIdentifierType_ReturnsBadRequest`
- `GetLookup_WithNoResults_ReturnsEmptyArray`

#### Frontend Tests (MediaSet.Remix)

**`lookup-data.test.ts`** (existing, extend)
- `lookup returns array for valid barcode`
- `lookup returns array for valid title`
- `lookup returns LookupError on API failure`
- `lookup maps fieldName "title" to identifierType "title"`

**`title-lookup-results-dialog.test.tsx`** (new)
- Renders list of result rows
- Calls `onSelect` with correct result when row clicked
- Calls `onClose` when dismissed
- Displays correct fields per entity type

**Route tests** (existing, extend)
- Barcode lookup takes first element from array and populates form
- Title lookup with single result auto-populates form
- Title lookup with multiple results renders `TitleLookupResultsDialog`
- Selecting from dialog populates form
- Title lookup with no results shows error at top of form
- Barcode field appears after title field

## Implementation Steps

1. **Add `Title` to `IdentifierType` enum**
   - Update `IdentifierType.cs` and `TryParseIdentifierType`

2. **Change `LookupAsync` return type in `ILookupStrategy<TResponse>`**
   - `Task<TResponse?>` → `Task<IReadOnlyList<TResponse>>`

3. **Add title search to `IOpenLibraryClient` + `OpenLibraryClient`**
   - Add `SearchByTitleAsync(string title, CancellationToken)` method
   - Call OpenLibrary `/search.json?title={title}&limit=10`
   - Map `docs` array to `IReadOnlyList<BookResponse>`

4. **Add title search to `IMusicBrainzClient` + `MusicBrainzClient`**
   - Add `SearchByTitleAsync(string title, CancellationToken)` method
   - Call MusicBrainz release title search endpoint
   - Map results to `IReadOnlyList<MusicResponse>`

5. **Update `BookLookupStrategy`**
   - Add `IdentifierType.Title` to `CanHandle`
   - Branch in `LookupAsync` on `IdentifierType.Title`
   - Wrap existing identifier paths in single-element list / empty list
   - Write/extend unit tests

6. **Update `MovieLookupStrategy`**
   - Add `IdentifierType.Title` to `CanHandle`
   - Branch in `LookupAsync` on `IdentifierType.Title`
   - Wrap existing UPC path in single-element list / empty list
   - Write/extend unit tests

7. **Update `GameLookupStrategy`**
   - Add `IdentifierType.Title` to `CanHandle`
   - Branch in `LookupAsync` on `IdentifierType.Title` (no best-match scoring)
   - Wrap existing UPC path in single-element list / empty list
   - Write/extend unit tests

8. **Update `MusicLookupStrategy`**
   - Add `IdentifierType.Title` to `CanHandle`
   - Branch in `LookupAsync` on `IdentifierType.Title`
   - Wrap existing barcode path in single-element list / empty list
   - Write/extend unit tests

9. **Update API endpoint in `LookupApi.cs`**
   - Change return type to array
   - Write/extend API tests

10. **Update `lookup-data.server.ts`**
    - Handle array response
    - Map `fieldName="title"` to `identifierType="title"`
    - Write unit tests

11. **Create `TitleLookupResultsDialog` component**
    - Implement with per-entity row display
    - Write component tests

12. **Update all four form components**
    - Add title Lookup button
    - Move barcode field below title field
    - Add keyboard handler on title input

13. **Update route action handlers (edit + add routes)**
    - Handle array `lookupResults`
    - Inspect `fieldName` to drive barcode-first vs title-dialog behavior
    - Handle zero-results by reusing existing top-of-form error display

14. **Integration testing**
    - All four entity types: title lookup → multiple results → dialog → select → form populated
    - All four entity types: title lookup → single result → form auto-populated
    - All four entity types: title lookup → no results → error displayed at top of form
    - All four entity types: barcode lookup → takes first result → form populated (unchanged UX)
    - Barcode field appears after title field on all forms

## Acceptance Criteria

- [ ] Backend: `IdentifierType` enum includes `Title`
- [ ] Backend: `LookupAsync` return type is `IReadOnlyList<TResponse>` on all four strategies
- [ ] Backend: all four strategies handle `IdentifierType.Title` in `CanHandle` and `LookupAsync`
- [ ] Backend: title lookup returns up to 10 results per strategy
- [ ] Backend: identifier-based lookup returns single-element list on success, empty list on not-found
- [ ] Backend: `GameLookupStrategy` title path does not apply best-match scoring
- [ ] Backend: `MovieLookupStrategy` and `GameLookupStrategy` title paths bypass UpcItemDb
- [ ] Backend: existing `GET /lookup/{entityType}/{identifierType}/{identifierValue}` endpoint returns array
- [ ] Backend: endpoint returns empty array (not 404) when no results found
- [ ] Backend: all new/modified unit tests pass
- [ ] Frontend: `lookup()` in `lookup-data.server.ts` returns array; `fieldName="title"` maps to `identifierType="title"`
- [ ] Frontend: `TitleLookupResultsDialog` renders per-entity row info and calls `onSelect`/`onClose` correctly
- [ ] Frontend: all four forms have a Lookup button on the title field, gated on the existing lookup-available capability flag
- [ ] Frontend: Enter key on title field triggers title lookup
- [ ] Frontend: barcode field appears **after** title field on all four entity forms
- [ ] Frontend: barcode lookup takes `results[0]` and populates form (UX unchanged)
- [ ] Frontend: title lookup with single result auto-populates form
- [ ] Frontend: title lookup with multiple results opens `TitleLookupResultsDialog`; selecting a row populates form
- [ ] Frontend: title lookup with zero results shows error at top of form (existing lookup error behavior)
- [ ] Frontend: all new/modified component and data tests pass
