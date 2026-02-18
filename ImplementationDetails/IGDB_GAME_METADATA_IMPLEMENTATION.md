# IGDB Game Metadata Implementation (Issue #478)

## Background

GiantBomb's public API is currently non-operational. The existing `GameLookupStrategy` depends on GiantBomb as the second stage of a two-step lookup (UPCitemdb barcode → GiantBomb metadata), making game barcode lookups completely broken. IGDB (Internet Game Database) has been selected as the replacement.

## Selected Provider: IGDB

**Website:** https://www.igdb.com
**API Docs:** https://api-docs.igdb.com

IGDB is community-driven and owned by Twitch/Amazon. It is the most comprehensive freely accessible game database available today and is actively maintained.

**Data Available:**
- Game name, summary, storyline
- Genres, themes, game modes
- Developer and publisher companies
- Platforms (with abbreviations)
- First/global release dates
- Age ratings (ESRB, PEGI, BBFC, etc.)
- Cover images and screenshots
- Alternative titles

**Authentication:**
Requires a Twitch Developer account. Auth flow:
1. Register an app at https://dev.twitch.tv/console
2. Obtain `client_id` and `client_secret`
3. Exchange credentials for an OAuth2 access token via `POST https://id.twitch.tv/oauth2/token?client_id=...&client_secret=...&grant_type=client_credentials`
4. Token is included in requests as `Authorization: Bearer <token>` with `Client-ID: <client_id>` header

Tokens expire and must be refreshed (the API returns `expires_in` on the token response).

**API Style:**
POST-based with a custom query language (Apicalypse). Example:
```
POST https://api.igdb.com/v4/games
Body: fields name,summary,genres.name,involved_companies.company.name,platforms.name,first_release_date,rating,cover.url; search "Elden Ring"; limit 5;
```

**Rate Limits (Free Tier):**
- 4 requests per second
- No monthly cap advertised for non-commercial use
- Token refresh is required (tokens are short-lived, typically ~60 days)

---

## Implementation Plan

### Overview of Changes

The migration replaces `GiantBombClient` / `IGiantBombClient` with `IgdbClient` / `IIgdbClient` while preserving all existing abstractions (strategy pattern, `GameResponse`, `GameLookupStrategy`). The two-stage flow (UPCitemdb → metadata provider) is retained.

---

### 1. New Configuration Model

**File:** `MediaSet.Api/Infrastructure/Lookup/Models/IgdbConfiguration.cs`

```csharp
public class IgdbConfiguration
{
    public string BaseUrl { get; set; } = "https://api.igdb.com/v4/";
    public string TokenUrl { get; set; } = "https://id.twitch.tv/oauth2/token";
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public int Timeout { get; set; } = 30;
}
```

**Environment variable names (docker-compose):**
```
IgdbConfiguration__ClientId
IgdbConfiguration__ClientSecret
IgdbConfiguration__BaseUrl        (optional)
IgdbConfiguration__TokenUrl       (optional)
IgdbConfiguration__Timeout        (optional)
```

---

### 2. Token Service

IGDB access tokens expire (returned `expires_in` seconds). A lightweight in-process token cache using `IMemoryCache` avoids re-requesting on every call.

**File:** `MediaSet.Api/Infrastructure/Lookup/Clients/Igdb/IgdbTokenService.cs`

```csharp
public interface IIgdbTokenService
{
    Task<string> GetAccessTokenAsync(CancellationToken cancellationToken);
}
```

Implementation:
- POST to `IgdbConfiguration.TokenUrl` with `client_id`, `client_secret`, `grant_type=client_credentials`
- Cache the resulting `access_token` for `expires_in - 60` seconds (60 second buffer)
- Use `IMemoryCache.GetOrCreateAsync` guarded by a `SemaphoreSlim` to ensure only one token fetch is in-flight at a time on a cache miss — without this, concurrent requests at startup (or after expiry) could all race to fetch a new token simultaneously

---

### 3. IGDB Data Models

**File:** `MediaSet.Api/Infrastructure/Lookup/Models/IgdbModels.cs`

Key response structures needed:

```csharp
// Game search / details (single endpoint covers both)
public record IgdbGame(
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("summary")] string? Summary,
    [property: JsonPropertyName("first_release_date")] long? FirstReleaseDate,  // Unix timestamp
    [property: JsonPropertyName("genres")] List<IgdbNamedObject>? Genres,
    [property: JsonPropertyName("involved_companies")] List<IgdbInvolvedCompany>? InvolvedCompanies,
    [property: JsonPropertyName("platforms")] List<IgdbPlatform>? Platforms,
    [property: JsonPropertyName("age_ratings")] List<IgdbAgeRating>? AgeRatings,
    [property: JsonPropertyName("cover")] IgdbCover? Cover
);

public record IgdbNamedObject(
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("name")] string Name
);

public record IgdbInvolvedCompany(
    [property: JsonPropertyName("company")] IgdbNamedObject? Company,
    [property: JsonPropertyName("developer")] bool Developer,
    [property: JsonPropertyName("publisher")] bool Publisher
);

public record IgdbPlatform(
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("abbreviation")] string? Abbreviation
);

// IGDB age_ratings uses category + rating enums, not free-form strings
// Category 1 = ESRB, 2 = PEGI; rating maps to a numeric enum
public record IgdbAgeRating(
    [property: JsonPropertyName("category")] int Category,
    [property: JsonPropertyName("rating")] int Rating
);

public record IgdbCover(
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("url")] string? Url
);
```

IGDB age rating enums must be decoded to strings (e.g. ESRB category=1, rating=6 → "T", rating=8 → "M"). A lookup table will be included in the client.

---

### 4. IGDB Client

**File:** `MediaSet.Api/Infrastructure/Lookup/Clients/Igdb/IgdbClient.cs`
**Interface:** `MediaSet.Api/Infrastructure/Lookup/Clients/Igdb/IIgdbClient.cs`

```csharp
public interface IIgdbClient
{
    Task<List<IgdbGame>?> SearchGameAsync(string title, CancellationToken cancellationToken);
    Task<IgdbGame?> GetGameDetailsAsync(int igdbId, CancellationToken cancellationToken);
}
```

Implementation notes:
- All requests are `POST` with `Content-Type: text/plain`
- Body is an Apicalypse query string
- Search query: `fields name,summary,first_release_date,genres.name,involved_companies.company.name,involved_companies.developer,involved_companies.publisher,platforms.name,platforms.abbreviation,age_ratings.category,age_ratings.rating,cover.url; search "{title}"; limit 10;`
- Details query (by ID): `fields ...; where id = {id};`
- Token and Client-ID are injected as headers per request via `IIgdbTokenService`
- Rate limit (429) handling matches the existing GiantBomb pattern (throw `HttpRequestException`)
- Cover image URLs from IGDB use `//images.igdb.com/...` (protocol-relative); prepend `https:` and replace `t_thumb` with `t_cover_big` for the best quality image

---

### 5. Update `GameLookupStrategy`

**File:** `MediaSet.Api/Infrastructure/Lookup/Strategies/GameLookupStrategy.cs`

Replace the `IGiantBombClient` dependency with `IIgdbClient`. Update:
- Stage 2: call `_igdbClient.SearchGameAsync(cleanedTitle, ct)` → returns `List<IgdbGame>?`
- `FindBestMatch` adapts to `IgdbGame` instead of `GiantBombSearchResult` (same scoring logic, different type)
- `MapToGameResponse` adapts to decode IGDB's age rating enums and company role flags
- `DeriveFormatFromPlatforms` updates to accept `List<IgdbPlatform>?` (structurally identical, just a different type)

All title-cleaning helpers (`CleanGameTitleAndExtractEdition`, `ExtractGameFormat`, `ExtractPlatformFromBarcode`) are UPCitemdb-derived and **do not change**.

---

### 6. Update `Program.cs` DI Registration

Replace the GiantBomb configuration block:

```csharp
// IGDB configuration
var igdbConfig = builder.Configuration.GetSection(nameof(IgdbConfiguration));
if (igdbConfig.Exists())
{
    builder.Services.Configure<IgdbConfiguration>(igdbConfig);
    builder.Services.AddMemoryCache(); // already registered, idempotent
    builder.Services.AddSingleton<IIgdbTokenService, IgdbTokenService>();
    builder.Services.AddHttpClient<IIgdbClient, IgdbClient>((sp, client) =>
    {
        var cfg = sp.GetRequiredService<IOptions<IgdbConfiguration>>().Value;
        client.BaseAddress = new Uri(cfg.BaseUrl);
        client.Timeout = TimeSpan.FromSeconds(cfg.Timeout);
    });
}

// Game lookup strategy requires both UPCitemdb and IGDB
if (upcItemDbConfig.Exists() && igdbConfig.Exists())
{
    builder.Services.AddScoped<ILookupStrategy<GameResponse>, GameLookupStrategy>();
}
```

---

### 7. Remove GiantBomb Files

Delete the following (no longer needed):
- `MediaSet.Api/Infrastructure/Lookup/Clients/GiantBomb/IGiantBombClient.cs`
- `MediaSet.Api/Infrastructure/Lookup/Clients/GiantBomb/GiantBombClient.cs`
- `MediaSet.Api/Infrastructure/Lookup/Models/GiantBombModels.cs`
- `MediaSet.Api/Infrastructure/Lookup/Models/GiantBombConfiguration.cs`
- `MediaSet.Api.Tests/Infrastructure/Lookup/Clients/GiantBomb/GiantBombClientTests.cs`

---

### 8. Update Tests

- Add `IgdbClientTests.cs` covering `SearchGameAsync` and `GetGameDetailsAsync` with mock HTTP responses
- Update `GameLookupStrategyTests.cs` to inject `IIgdbClient` mock instead of `IGiantBombClient`
- Add tests for IGDB age rating enum decoding
- Add tests for `IgdbTokenService` (cache hit, cache miss, token expiry buffer)
- Keep all existing `CleanGameTitleAndExtractEdition`, `ExtractGameFormat`, `ExtractPlatformFromBarcode`, `DeriveFormatFromPlatforms`, and `FindBestMatch` tests (logic unchanged)
- Update test `appsettings.Testing.json` to replace GiantBomb section with IGDB section

---

### 9. Setup Documentation

**Replace** `Setup/GIANTBOMB_SETUP.md` with `Setup/IGDB_SETUP.md` following the same structure as `TMDB_SETUP.md`:

1. Register a Twitch Developer application at https://dev.twitch.tv/console
2. Copy `client_id` and `client_secret`
3. Configure `docker-compose.prod.yml`:
   ```bash
   # IGDB configuration (for game lookup)
   # IgdbConfiguration__ClientId: "[ReplaceThis]"
   # IgdbConfiguration__ClientSecret: "[ReplaceThis]"
   # IgdbConfiguration__BaseUrl: "https://api.igdb.com/v4/"     (optional)
   # IgdbConfiguration__Timeout: "10"                            (optional)
   ```
4. Restart the stack
5. UPCitemdb is still required for barcode-based lookup (same as before)
6. Verification steps (same pattern as TMDB setup doc)

Include an **Attribution** note — IGDB's terms require attribution:

> Game data provided by [IGDB](https://www.igdb.com)

This attribution should also appear somewhere in the UI (footer or About page) when IGDB is configured.

---

### 10. Attribution in the UI

IGDB's terms of service require that applications displaying IGDB data include visible attribution. Add an "IGDB" attribution to the footer or settings/integrations page when the IGDB integration is active. The integrations endpoint (`/lookup-capabilities`) already controls which provider badges/links are shown, so this follows the existing pattern.

---

## File Change Summary

| Action | File |
|--------|------|
| **Delete** | `MediaSet.Api/Infrastructure/Lookup/Clients/GiantBomb/IGiantBombClient.cs` |
| **Delete** | `MediaSet.Api/Infrastructure/Lookup/Clients/GiantBomb/GiantBombClient.cs` |
| **Delete** | `MediaSet.Api/Infrastructure/Lookup/Models/GiantBombModels.cs` |
| **Delete** | `MediaSet.Api/Infrastructure/Lookup/Models/GiantBombConfiguration.cs` |
| **Delete** | `MediaSet.Api.Tests/Infrastructure/Lookup/Clients/GiantBomb/GiantBombClientTests.cs` |
| **Delete** | `Setup/GIANTBOMB_SETUP.md` |
| **Create** | `MediaSet.Api/Infrastructure/Lookup/Clients/Igdb/IIgdbClient.cs` |
| **Create** | `MediaSet.Api/Infrastructure/Lookup/Clients/Igdb/IgdbClient.cs` |
| **Create** | `MediaSet.Api/Infrastructure/Lookup/Clients/Igdb/IIgdbTokenService.cs` |
| **Create** | `MediaSet.Api/Infrastructure/Lookup/Clients/Igdb/IgdbTokenService.cs` |
| **Create** | `MediaSet.Api/Infrastructure/Lookup/Models/IgdbConfiguration.cs` |
| **Create** | `MediaSet.Api/Infrastructure/Lookup/Models/IgdbModels.cs` |
| **Create** | `MediaSet.Api.Tests/Infrastructure/Lookup/Clients/Igdb/IgdbClientTests.cs` |
| **Create** | `Setup/IGDB_SETUP.md` |
| **Modify** | `MediaSet.Api/Infrastructure/Lookup/Strategies/GameLookupStrategy.cs` |
| **Modify** | `MediaSet.Api/Program.cs` |
| **Modify** | `MediaSet.Api.Tests/Infrastructure/Lookup/Strategies/GameLookupStrategyTests.cs` |
| **Modify** | `MediaSet.Api.Tests/appsettings.Testing.json` |
| **Modify** | UI footer/integrations — add IGDB attribution |

---

## Key Technical Considerations

**Token Lifecycle:** IGDB access tokens expire. The token service must handle expiry gracefully. A `IMemoryCache` entry with a TTL of `expires_in - 60` seconds is the simplest approach. If a request fails with 401, the cached token should be evicted and a fresh one fetched before retrying once.

**Age Rating Mapping:** GiantBomb returned rating names as strings (`"T - Teen"`). IGDB returns numeric enums. The client must maintain a mapping table:

| Category | IGDB Rating Int | String |
|----------|-----------------|--------|
| ESRB (1) | 6 | RP |
| ESRB (1) | 7 | EC |
| ESRB (1) | 8 | E |
| ESRB (1) | 9 | E10+ |
| ESRB (1) | 10 | T |
| ESRB (1) | 11 | M |
| ESRB (1) | 12 | AO |
| PEGI (2) | 1 | 3 |
| PEGI (2) | 2 | 7 |
| PEGI (2) | 3 | 12 |
| PEGI (2) | 4 | 16 |
| PEGI (2) | 5 | 18 |

Prefer ESRB when available (same as the existing GiantBomb implementation).

**Cover Image URL Format:** IGDB image URLs look like `//images.igdb.com/igdb/image/upload/t_thumb/co1wyy.jpg`. To get a full-size cover: prefix with `https:` and replace the size token (`t_thumb`) with `t_cover_big` (264×374) or `t_cover_small` (90×128). The existing `ImageUrl` field in `GameResponse` accepts this transformed URL as-is — no `IImageService` changes needed.

**Apicalypse Query Language:** All IGDB API calls use POST with a plain-text body. This is different from the GiantBomb GET-based approach but is trivially handled by `HttpClient.PostAsync(url, new StringContent(query, Encoding.UTF8, "text/plain"))`.

**No `GameResponse` model changes:** The existing `GameResponse` record maps cleanly from IGDB data. No changes to the entity model or frontend are required.

---

## Other Providers Considered

### RAWG

**Website:** https://rawg.io
**API Docs:** https://api.rawg.io/docs/

REST-based API with simple API key authentication covering 500,000+ games.

**Data Available:** Game name, description, release date, genres, tags, platforms, developers, publishers, Metacritic score, cover image.

**Why not chosen:**
- Reported as largely unmaintained/abandonware as of mid-2024 (frequent downtime, stale data, broken registration)
- No ESRB/PEGI age rating data
- Less comprehensive metadata than IGDB
- Unclear long-term viability

---

### MobyGames

**Website:** https://www.mobygames.com
**API Docs:** https://www.mobygames.com/info/api/

The oldest game database with very deep historical/retro catalog coverage.

**Data Available:** Game name, description, genres, developers, publishers, platforms, release dates, cover art, age ratings.

**Why not chosen:**
- API access now requires a paid **MobyPro subscription** — not suitable for a free personal-use project
- Previously free access has been deprecated
