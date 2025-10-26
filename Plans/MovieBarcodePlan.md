# Movie Barcode Lookup – Investigation and Plan

Date: 2025-10-24

## Goal
Add a barcode lookup flow for the Movie media type, analogous to the existing Book lookup by ISBN, to prefill movie details from a scanned or typed UPC/EAN barcode.

---

## What exists today (Books)
- Backend
  - `LookupApi` exposes `GET /lookup/{identifierType}/{identifierValue}` when OpenLibrary is configured, returning a `BookResponse` from `OpenLibraryClient`.
  - Supported identifier types are `isbn`, `lccn`, `oclc`, `olid` (`IdentifierType` + `IdentifierTypeExtensions`).
  - `OpenLibraryClient` calls OpenLibrary Read API to transform results into a normalized `BookResponse` (title, subtitle, authors, publishers, publish date, subjects, pages, format).
- Frontend
  - `app/lookup-data.ts` calls `/lookup/isbn/{barcode}` and maps the response into a `BookEntity`.

Implications for Movies:
- There’s no movie lookup endpoint today; movies have a `Barcode` field in `MediaSet.Api/Models/Movie.cs` but no external lookup.
- A similar pattern (minimal API + client + config + typed response + frontend data function) should be used.

---

## What we need for Movies
- New backend client(s) capable of looking up movie metadata from a UPC/EAN (12 or 13 digits).
- New endpoint(s) (e.g., `GET /lookup/movies/{barcode}` or `GET /lookup/{identifierType}/{identifierValue}` supporting `upc`/`ean`) that return a normalized Movie lookup response.
- Config-driven registration (like OpenLibrary) so the API wires up only when credentials are present.
- Frontend data function to call the new endpoint and map results into `MovieEntity` fields.
- Tests to validate mapping and endpoint behavior.

---

## Data we’d like to prefill for Movie
- Title
- Studios (aka production companies/publishers for discs)
- Genres
- Release date
- Rating (audience rating)
- Runtime (minutes)
- Plot/overview
- Format (e.g., Blu‑ray, DVD, 4K UHD)
- Is TV Series

A raw UPC/EAN provider usually returns only generic product information (title, brand, category). To get rich movie metadata, a two-step approach is often required:
1) UPC/EAN → basic product info (title, possibly year, sometimes format)
2) Then search a movie metadata provider (TMDb or OMDb) by title (+ year) to fetch structured movie details.

---

## Proposed backend design


### Identifier types & Endpoint
  - Add `upc` and `ean` support (validation: 12/13-digit numeric; optionally compute/verify check digit) to the existing lookup pattern.
  - Update the existing endpoint to: `GET /lookup/{entityType}/{identifierType}/{identifierValue}` (e.g., `/lookup/movies/upc/012345678912` or `/lookup/books/isbn/9780134685991`).
  - This single endpoint will handle lookups for all supported media types (books, movies, games, music) and return type-specific data.

### Service layer (LookupService with Strategy Pattern)
  - Add a unified `LookupService` class (with `ILookupService` interface) in the `Services` folder. This service orchestrates lookup requests by delegating to entity-specific strategy implementations.
  - **Strategy Pattern Implementation with Generics**:
    - Create a **dual interface pattern** for type safety:
      - `ILookupStrategy` (non-generic base interface) with methods:
        - `string EntityType { get; }` - identifies which entity type this strategy handles
        - `bool SupportsIdentifierType(string identifierType)` - validates supported identifier types
      - `ILookupStrategy<TResponse>` (generic interface) inherits from `ILookupStrategy` and adds:
        - `Task<TResponse> LookupAsync(string identifierType, string identifierValue, CancellationToken cancellationToken)` - performs the lookup with type-safe return
    - `LookupService` receives `IEnumerable<ILookupStrategy>` via DI, selects based on `entityType`, and casts to the appropriate generic interface
    - The lookup endpoint becomes thin: validates route parameters and delegates to `ILookupService`
  - **Strategy Implementations** (in `Services/Lookup/` folder):
    - `BookLookupStrategy` (implements `ILookupStrategy<BookResponse>`):
      - Handles book lookups for ISBN, LCCN, OCLC, OLID
      - Injects `IOpenLibraryClient`
      - Validates identifier, calls client, returns `BookResponse` (type-safe)
    - `MovieLookupStrategy` (implements `ILookupStrategy<MovieLookupResponse>`):
      - Handles movie lookups for UPC, EAN
      - Injects `IProductLookupClient` and `IMovieMetadataClient`
      - Coordinates: validate barcode → product lookup → normalize title/year → metadata lookup → map to `MovieLookupResponse` (type-safe)
    - Future strategies (Games, Music) can be added without modifying existing code, each with their own response type
  - **Caching and Error Handling**:
    - Each strategy is responsible for its own caching (via `ICacheService`), error handling, and logging
    - Common caching/logging patterns can be extracted to a base class or helper if needed
  - **Testing Structure**:
    - `LookupServiceTests` - test strategy selection, routing, and error handling when no strategy found
    - `BookLookupStrategyTests` - mock `IOpenLibraryClient`, test validation and mapping, verify `BookResponse` return type
    - `MovieLookupStrategyTests` - mock `IProductLookupClient` and `IMovieMetadataClient`, test coordination flow, title normalization, error handling, and verify `MovieLookupResponse` return type
    - Each strategy can be tested in complete isolation with focused, maintainable tests and full compile-time type safety

### Clients and configuration
  - `IProductLookupClient`: abstract UPC/EAN lookup returning a small normalized product payload (title, brand, category, images, possibly raw JSON). Pluggable implementation. Should live in the `Clients` folder.
  - `IMovieMetadataClient`: e.g., TMDb or OMDb client for title/year search and detail fetch. Should live in the `Clients` folder.
  - Configuration sections (like `OpenLibraryConfiguration`) for each provider with base URL, API key, timeout, contact email.

### Endpoint contract (suggested)
  - Request: `GET /lookup/{entityType}/{identifierType}/{identifierValue}`
    - `entityType`: books, movies, games, music
    - `identifierType`: isbn, upc, ean, lccn, oclc, olid (as supported per entity)
    - `identifierValue`: barcode or identifier value
  - Response: 200 OK with a type-specific response (e.g., `MovieLookupResponse` for movies, `BookResponse` for books)
    - For movies, return:
      - title: string
      - studios: string[]
      - genres: string[]
      - releaseDate: string
      - rating: string
      - runtime: number | null
      - plot: string
      - format: string | null
      - isTvSeries: boolean
    - For books, return the existing `BookResponse` shape
  - 404 when no result (or result cannot be confidently matched)
  - 400 when identifier is invalid

### Behavior
  - Validate identifier (length, format, optional check digit for barcodes).
  - Call appropriate provider(s) based on entityType and identifierType.
  - For movies: UPC/EAN provider → normalize title → TMDb/OMDb lookup → map to `MovieLookupResponse`.
  - For books: OpenLibrary lookup as today.
  - Cache positive and negative results (existing `ICacheService`) to reduce provider calls and rate-limit pressure.
  - Structured logging with correlation id (align with existing logging).

---

## Provider options (free or free-tier)

Note: Availability, quotas, and terms change; verify current limits and ToS before implementation.

### UPC/EAN provider (for barcode → product title)
- **Barcode Lookup (barcodelookup.com)**
  - Commercial API with trial options; best coverage and categorization for DVDs/Blu-rays.
  - Returns product data (title, brand, category, images). Metadata is typically sufficient for movie identification.
  - Pros: Reliable coverage, robust categorization, preferred for this use case.
  - Cons: Paid for sustained use; confirm trial/usage terms for personal/non-commercial projects.

### Movie metadata provider (for rich details once we have a probable title)
- **TMDb (themoviedb.org)**
  - Free for non‑commercial use with API key and attribution; robust movie/TV metadata.
  - Search by title/year, then fetch detail (studios, genres, runtime, overview, release dates, certifications for ratings).
  - Pros: High‑quality, well‑documented, great coverage, preferred for movie metadata enrichment.
  - Cons: Requires key; must follow attribution and rate limits.

Summary: The recommended approach is to use Barcode Lookup for UPC/EAN product identification, followed by TMDb for rich movie metadata. Other providers are not considered for this implementation.

---

## Mapping and normalization
- Title normalization
  - Remove format markers in titles from product feeds: e.g., “Movie Title (Blu-ray + Digital)”.
  - Trim edition descriptors that hinder search.
- Year extraction
  - If the product title contains a year in parentheses, extract it to bias TMDb/OMDb search.
- Format inference
  - Use product category and title markers to infer `format` (Blu‑ray, DVD, 4K UHD). TMDb/OMDb don’t carry disc format.
- Studios/production companies
  - From TMDb details: `production_companies[].name`.
- Genres
  - From TMDb or OMDb genres; map directly to `Movie` genres list.
- Rating
  - TMDb has release certifications by country; pick US if available to map to `Rating`.
  - OMDb sometimes returns `Rated` (e.g., “PG‑13”).
- Runtime
  - Prefer TMDb `runtime` in minutes; fallback OMDb.
- Plot
  - Prefer TMDb `overview`; fallback OMDb `Plot`.

---

## Error handling, rate limiting, and caching
- Validate barcode early; return 400 on invalid input.
- Distinguish 404 (no match) from upstream 5xx (transient upstream failure → 502/503 or 504 with Retry-After).
- Cache:
  - Positive results keyed by barcode for days.
  - Negative results for short TTL to avoid hammering providers.
  - Consider caching title→TMDb id to stabilize mapping.
- Respect provider rate limits; add small jittered backoff on 429/5xx.

---

## Security and configuration
- Add strongly-typed config sections for:
  - `UpcLookupConfiguration` (base URL, key, timeout, contact)
  - `TmdbConfiguration` or `OmdbConfiguration` (key, base URL, timeout)
- Only register the lookup endpoint when required configs exist (mirrors OpenLibrary gating in `Program.cs`).
- Do not log API keys; scrub sensitive headers.

---

## Frontend changes (Remix)


### Lookup data functions
  - Update or extend the lookup data functions (e.g., `app/lookup-data.ts`) to call the unified endpoint:
    - For books: `GET /lookup/books/isbn/{barcode}`
    - For movies: `GET /lookup/movies/upc/{barcode}` or `GET /lookup/movies/ean/{barcode}`
  - Branch logic in the data function based on `entityType` to construct the correct endpoint and map the response into the appropriate entity fields (`BookEntity`, `MovieEntity`).
  - Handle 404 (show “No {entityType} found for {identifierType} {barcode}”).

### UI integration
  - Remove the top ISBN lookup input from the Book add/edit form. Instead, add a “Lookup” button directly next to the ISBN field in the Book form and next to the Barcode field in the Movie form. This button should exist for both add and edit forms.
  - When clicked, the button will fetch data for the entered identifier (ISBN for books, Barcode for movies) using the unified lookup endpoint, and prefill the form fields with the returned entity data.
  - Show loading and error states as appropriate (e.g., spinner, error message if not found).
  - Ensure types in `app/models.ts` are kept in sync with the new response shapes if shared types are introduced.

---

## Testing
- Backend
  - Unit tests for barcode validation (UPC/EAN), mapping logic (title normalization, format inference).
  - Endpoint tests similar to `LookupApiTests`, mocking `IProductLookupClient` and `IMovieMetadataClient`.
  - Integration test happy path: UPC → product → TMDb match → mapped response.
- Frontend
  - Data function tests (Vitest) to assert mapping to `MovieEntity` and error handling.

---

## Incremental implementation steps
1) Backend scaffolding
   - Define response contract `MovieLookupResponse` and new endpoint (`/lookup/movies/{barcode}` or extend existing pattern with `upc`/`ean`).
   - Add interfaces + configs: `IProductLookupClient` (UPCItemDB/EAN-Search impl), `IMovieMetadataClient` (TMDb or OMDb impl).
   - Wire DI and conditional registration (config-gated) in `Program.cs`.
2) Core logic
   - Validate barcode, call product client, normalize title/year, call TMDb/OMDb, map to `MovieLookupResponse`.
   - Add caching via existing `ICacheService`.
3) Tests
   - Add NUnit tests for endpoint + mapping; mock clients.
4) Frontend
   - Implement data function and integrate with `MovieForm` UX.
5) Docs
   - Update README with configuration sections and attribution requirements for chosen providers.

---

## Risks and mitigations
- Coverage gaps for UPC/EAN provider → fallback: allow manual title override; retry with OMDb directly by title.
- Ambiguous titles (remakes) → bias search with year parsing or product category cues (e.g., “4K UHD (2018)”).
- Rate limits → cache, backoff, UI messaging on throttling.
- Non‑commercial usage terms → ensure compliance with provider ToS and attribution.

---

## Recommendation (initial choice)
- UPC/EAN: Start with UPCItemDB (simple, has free tier; confirm current limits).
- Movie metadata: TMDb (rich metadata) with OMDb as optional fallback.
- Endpoint: `GET /lookup/movies/{barcode}` returning a `MovieLookupResponse`.
- Add `upc`/`ean` identifier support later if we want a single generic `/lookup/{identifierType}/{value}` endpoint across entities.

---

## Acceptance criteria
- Given a valid UPC/EAN of a common Blu‑ray/DVD, calling `GET /lookup/movies/{barcode}` returns 200 with populated fields (title, runtime, plot, studios, genres, format inferred, release date, rating when available).
- Invalid barcode returns 400.
- Unknown barcode returns 404.
- Frontend can prefill the Movie form using the result.

---

## Open questions
- Which provider(s) do we want to commit to for long‑term usage (quota, attribution, ToS)?
- Should we support a fallback flow by title only when barcode fails (direct TMDb/OMDb search)?
- Do we want to store a mapping table (barcode → TMDb/IMDb id) in Mongo for faster subsequent lookups?
