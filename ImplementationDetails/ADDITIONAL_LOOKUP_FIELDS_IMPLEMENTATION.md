# Additional Lookup Fields Implementation (Issue #536)

## Overview

Enhance title lookup to support additional search fields for music (Artist) and books (Author).
Redesign the lookup API endpoint to accept a flexible property dictionary instead of a single identifier value.
Also reorder both forms so lookup-related fields are grouped together at the top.

---

## New API Design

### Unchanged route structure
```
GET /lookup/{entityType}/{identifierType}/{value?}
```

The route shape is the same as today. The change is that `identifierType` gains a new special value — `entity` — which signals a multi-property search. When `identifierType` is `entity`, `{value}` is absent and search properties are passed as query string parameters instead. For all other identifier types the `{value}` path segment remains required.

### Two modes of the same endpoint

**Simple lookup** — single identifier value in the path (unchanged behavior):
```
GET /lookup/{entityType}/{identifierType}/{value}

GET /lookup/books/isbn/9780439708180
GET /lookup/musics/barcode/5099747289652
GET /lookup/movies/upc/025192251047
GET /lookup/games/upc/045496590406
```

**Entity lookup** — multi-property search via query string (new):
```
GET /lookup/{entityType}/entity?property=value&...

GET /lookup/musics/entity?title=Dark+Side+of+the+Moon&artist=Pink+Floyd
GET /lookup/books/entity?title=Harry+Potter&author=Rowling
GET /lookup/movies/entity?title=The+Matrix
GET /lookup/games/entity?title=Halo
```

### Validation
- If `identifierType` is **not** `entity`: `{value}` is required; query params are ignored.
- If `identifierType` is `entity`: at least one query param must be present; all param keys must be valid properties for that entity type; `{value}` is absent/ignored.

### Valid entity lookup properties per entity type

Valid properties are no longer a hardcoded list — they are derived at runtime from `[LookupProperty]` attributes on per-entity lookup params classes (see Backend Changes). Adding a new lookup field in the future only requires decorating a property; no other changes needed.

> **Note:** The existing `IdentifierType` enum stays as-is for strategies' internal use. `entity` is handled as a plain string check at the endpoint level before enum parsing — no new enum value needed.

---

## Backend Changes

### 1. `IdentifierType` enum
**File:** wherever `IdentifierType` is defined

Replace `Title` with `Entity`. Title lookup is now always done via the entity route with a `title` query param, so `Title` as a standalone identifier type is obsolete:

```csharp
public enum IdentifierType
{
    Isbn, Lccn, Oclc, Olid, Upc, Ean, Entity  // Title removed, Entity added
}
```

Any existing references to `IdentifierType.Title` throughout strategies and extension methods get replaced with `IdentifierType.Entity`.

### 2. `[LookupPropertyAttribute]`
**File:** `MediaSet.Api/Infrastructure/Lookup/Attributes/LookupPropertyAttribute.cs` (new)

Follows the same pattern as existing model attributes (`[LookupIdentifier]`, `[Upload]`). Applied selectively to properties on the shared entity models that are valid lookup keys. The optional `Name` allows overriding the query param key name (defaults to the property name lowercased):

```csharp
[AttributeUsage(AttributeTargets.Property)]
public sealed class LookupPropertyAttribute : Attribute
{
    public string? Name { get; }
    public LookupPropertyAttribute() { }
    public LookupPropertyAttribute(string name) { Name = name; }
}
```

### 3. Apply `[LookupProperty]` to shared entity models
**Files:** existing shared entity model classes

Decorate the relevant properties on each existing shared entity model. The base class reflects on the model to build `SupportedProperties` automatically — no separate params classes needed:

```csharp
// Music entity model
public class MusicEntity
{
    [LookupProperty] public string Title { get; set; }
    [LookupProperty] public string Artist { get; set; }
    [LookupProperty] public string Barcode { get; set; }
    // ... other non-lookup properties unchanged
}

// Book entity model
public class BookEntity
{
    [LookupProperty] public string Title { get; set; }
    [LookupProperty("author")] public string Authors { get; set; }  // name override if property name doesn't match desired key
    [LookupProperty] public string Isbn { get; set; }
    [LookupProperty] public string Lccn { get; set; }
    [LookupProperty] public string Oclc { get; set; }
    [LookupProperty] public string Olid { get; set; }
    // ... other non-lookup properties unchanged
}

// Movie / Game entity models — add [LookupProperty] to Title, Upc, Ean as appropriate
```

> **Note on `Upc`/`Ean` for books:** If these aren't stored on `BookEntity`, the `[LookupProperty]` attribute can be applied to non-persisted properties (no `[BsonElement]`) — they'd exist solely to declare the valid lookup key. Alternatively, they can be added as stored fields if there's value in retaining them. Confirm during implementation.

### 4. `ILookupStrategy<T>` interface + `LookupStrategyBase<TEntity, TResponse>`
**Files:**
- `MediaSet.Api/Infrastructure/Lookup/Strategies/ILookupStrategy.cs` (updated)
- `MediaSet.Api/Infrastructure/Lookup/Strategies/LookupStrategyBase.cs` (new)

`CanHandle` retains both `MediaTypes` and `IdentifierType`. `string identifierValue` is replaced by `IReadOnlyDictionary<string, string>`. `SupportedProperties` moves to the abstract base class, built via reflection on the shared entity model — no hardcoded strings in strategies:

```csharp
public interface ILookupStrategyBase
{
    bool CanHandle(MediaTypes entityType, IdentifierType identifierType);
    IReadOnlySet<string> SupportedProperties { get; }
}

public interface ILookupStrategy<TResponse> : ILookupStrategyBase where TResponse : class
{
    Task<IReadOnlyList<TResponse>> LookupAsync(
        IdentifierType identifierType,
        IReadOnlyDictionary<string, string> searchParams,
        CancellationToken cancellationToken);
}

public abstract class LookupStrategyBase<TEntity, TResponse> : ILookupStrategy<TResponse>
    where TEntity : class
    where TResponse : class
{
    // Built once per concrete strategy type, cached statically
    private static readonly IReadOnlySet<string> _supportedProperties = BuildSupportedProperties();

    public IReadOnlySet<string> SupportedProperties => _supportedProperties;

    private static IReadOnlySet<string> BuildSupportedProperties() =>
        typeof(TEntity)
            .GetProperties()
            .Select(p => (prop: p, attr: p.GetCustomAttribute<LookupPropertyAttribute>()))
            .Where(x => x.attr != null)
            .Select(x => x.attr!.Name ?? x.prop.Name.ToLowerInvariant())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

    public abstract bool CanHandle(MediaTypes entityType, IdentifierType identifierType);

    public abstract Task<IReadOnlyList<TResponse>> LookupAsync(
        IdentifierType identifierType,
        IReadOnlyDictionary<string, string> searchParams,
        CancellationToken cancellationToken);
}
```

For simple lookups the endpoint builds a single-entry dict (e.g. `{ "isbn": "9780439708180" }`).
For entity lookups it passes the query params dict directly with `IdentifierType.Entity`.

`SupportedProperties` is used by the endpoint to validate entity lookup keys — only relevant for `IdentifierType.Entity`.

### 3. `LookupStrategyFactory`
**File:** `MediaSet.Api/Infrastructure/Lookup/Strategies/LookupStrategyFactory.cs`

`CanHandle` still takes both parameters — unchanged from today. Add a non-generic overload specifically for `Entity` lookup validation (needs `SupportedProperties` before the typed dispatch):

```csharp
public ILookupStrategy<TResponse> GetStrategy<TResponse>(MediaTypes mediaType, IdentifierType identifierType)
    where TResponse : class
{
    var strategies = _serviceProvider.GetServices<ILookupStrategy<TResponse>>();
    return strategies.FirstOrDefault(s => s.CanHandle(mediaType, identifierType))
        ?? throw new NotSupportedException($"No strategy found for {mediaType} with {identifierType}");
}

// Used only for Entity lookup key validation
public ILookupStrategyBase GetStrategyBase(MediaTypes mediaType)
{
    // Finds the strategy that handles IdentifierType.Entity for this entity type
    // All entity types support Entity, so this always succeeds
}
```

### 4. `LookupApi` endpoint
**File:** `MediaSet.Api/Features/Lookup/Endpoints/LookupApi.cs`

The route signature gains an optional `{value}` segment. The handler branches on whether `identifierType == "entity"`:

```csharp
group.MapGet("/{entityType}/{identifierType}/{value?}", async (
    LookupStrategyFactory strategyFactory,
    string entityType,
    string identifierType,
    string? value,
    HttpRequest request,
    CancellationToken cancellationToken) =>
{
    if (!Enum.TryParse<MediaTypes>(entityType, true, out var parsedEntityType))
        return TypedResults.BadRequest($"Invalid entity type: {entityType}.");

    IReadOnlyDictionary<string, string> searchParams;

    IdentifierType parsedIdentifierType;
    IReadOnlyDictionary<string, string> searchParams;

    if (identifierType.Equals("entity", StringComparison.OrdinalIgnoreCase))
    {
        parsedIdentifierType = IdentifierType.Entity;

        searchParams = request.Query
            .Where(q => !string.IsNullOrWhiteSpace(q.Value))
            .ToDictionary(q => q.Key.ToLowerInvariant(), q => q.Value.ToString());

        if (searchParams.Count == 0)
            return TypedResults.BadRequest("At least one search parameter is required for entity lookup.");

        var baseStrategy = strategyFactory.GetStrategyBase(parsedEntityType);
        var invalidKeys = searchParams.Keys.Except(baseStrategy.SupportedProperties, StringComparer.OrdinalIgnoreCase).ToList();
        if (invalidKeys.Count > 0)
            return TypedResults.BadRequest(
                $"Invalid properties for {entityType}: {string.Join(", ", invalidKeys)}. " +
                $"Valid properties: {string.Join(", ", baseStrategy.SupportedProperties)}");
    }
    else
    {
        if (!IdentifierTypeExtensions.TryParseIdentifierType(identifierType, out parsedIdentifierType))
            return TypedResults.BadRequest($"Invalid identifier type: {identifierType}.");

        if (string.IsNullOrWhiteSpace(value))
            return TypedResults.BadRequest($"A value is required for identifier type '{identifierType}'.");

        // Single-entry dict — strategies read from it by key name
        searchParams = new Dictionary<string, string> { [identifierType.ToLowerInvariant()] = value };
    }

    // Dispatch — IdentifierType and dict both passed through to strategy
    switch (parsedEntityType)
    {
        case MediaTypes.Books:
            var books = await strategyFactory.GetStrategy<BookResponse>(parsedEntityType)
                .LookupAsync(parsedIdentifierType, searchParams, cancellationToken);
            return TypedResults.Ok(books);
        // ... Movies, Games, Musics
    }
});
```

`LookupStrategyFactory` gains a non-generic `GetStrategyBase(MediaTypes)` method returning `ILookupStrategyBase` for the validation step — avoids generic gymnastics while keeping `SupportedProperties` validation in one place.

### 5. `MusicLookupStrategy`
**File:** `MediaSet.Api/Infrastructure/Lookup/Strategies/MusicLookupStrategy.cs`

Extends `LookupStrategyBase<MusicEntity, MusicResponse>` — `SupportedProperties` is inherited and reflection-derived from `[LookupProperty]` attributes on `MusicEntity`. `Title` replaced with `Entity` in supported identifier types:

```csharp
public class MusicLookupStrategy : LookupStrategyBase<MusicEntity, MusicResponse>
{
private static readonly IdentifierType[] _supportedIdentifierTypes =
    [IdentifierType.Upc, IdentifierType.Ean, IdentifierType.Entity];

public override bool CanHandle(MediaTypes entityType, IdentifierType identifierType) =>
    entityType == MediaTypes.Musics && _supportedIdentifierTypes.Contains(identifierType);

public override async Task<IReadOnlyList<MusicResponse>> LookupAsync(
    IdentifierType identifierType,
    IReadOnlyDictionary<string, string> searchParams,
    CancellationToken cancellationToken)
{
    return identifierType switch
    {
        IdentifierType.Entity =>
            await SearchByEntityPropertiesAsync(searchParams, cancellationToken),
        IdentifierType.Upc or IdentifierType.Ean =>
            await LookupByBarcodeAsync(
                searchParams.GetValueOrDefault("upc") ?? searchParams.GetValueOrDefault("ean") ?? string.Empty,
                cancellationToken),
        _ => throw new NotSupportedException(...)
    };
}
}
```

### 6. `BookLookupStrategy`
**File:** `MediaSet.Api/Infrastructure/Lookup/Strategies/BookLookupStrategy.cs`

Same — extends `LookupStrategyBase<BookEntity, BookResponse>`:

```csharp
public class BookLookupStrategy : LookupStrategyBase<BookEntity, BookResponse>
{
private static readonly IdentifierType[] _supportedIdentifierTypes =
    [IdentifierType.Isbn, IdentifierType.Lccn, IdentifierType.Oclc, IdentifierType.Olid,
     IdentifierType.Upc, IdentifierType.Ean, IdentifierType.Entity];

public override bool CanHandle(MediaTypes entityType, IdentifierType identifierType) =>
    entityType == MediaTypes.Books && _supportedIdentifierTypes.Contains(identifierType);

public override async Task<IReadOnlyList<BookResponse>> LookupAsync(
    IdentifierType identifierType,
    IReadOnlyDictionary<string, string> searchParams,
    CancellationToken cancellationToken)
{
    return identifierType switch
    {
        IdentifierType.Entity =>
            await _openLibraryClient.SearchByEntityPropertiesAsync(searchParams, cancellationToken),
        IdentifierType.Isbn =>
            await LookupSingleAsync(
                await _openLibraryClient.GetReadableBookByIsbnAsync(searchParams["isbn"], cancellationToken)),
        // lccn, oclc, olid, upc, ean — same pattern, unchanged logic
        _ => throw new NotSupportedException(...)
    };
}
}
```

### 7. `MovieLookupStrategy` and `GameLookupStrategy`
**Files:** `...Strategies/MovieLookupStrategy.cs`, `...Strategies/GameLookupStrategy.cs`

Extend `LookupStrategyBase<MovieEntity, MovieResponse>` / `LookupStrategyBase<GameEntity, GameResponse>`. `Title` → `Entity`; `string identifierValue` → dict. Internal logic unchanged:

```csharp
public class MovieLookupStrategy : LookupStrategyBase<MovieEntity, MovieResponse>
{
private static readonly IdentifierType[] _supportedIdentifierTypes =
    [IdentifierType.Upc, IdentifierType.Ean, IdentifierType.Entity];

public override bool CanHandle(MediaTypes entityType, IdentifierType identifierType) =>
    entityType == MediaTypes.Movies && _supportedIdentifierTypes.Contains(identifierType); // or Games

public override async Task<IReadOnlyList<MovieResponse>> LookupAsync(
    IdentifierType identifierType,
    IReadOnlyDictionary<string, string> searchParams,
    CancellationToken cancellationToken)
{
    return identifierType switch
    {
        IdentifierType.Entity =>
            await SearchByEntityPropertiesAsync(searchParams["title"], cancellationToken),
        IdentifierType.Upc or IdentifierType.Ean =>
            await LookupByUpcAsync(searchParams.GetValueOrDefault("upc") ?? searchParams["ean"], cancellationToken) is { } result
                ? [result] : [],
        _ => throw new NotSupportedException(...)
    };
}
```

### 7. `IMusicBrainzClient` + `MusicBrainzClient`
**Files:** `...Clients/MusicBrainz/IMusicBrainzClient.cs`, `MusicBrainzClient.cs`

`SearchByEntityPropertiesAsync` takes the full dict — the client extracts what it needs. Adding new lookup properties in the future requires no signature change:

```csharp
Task<IReadOnlyList<MusicBrainzRelease>> SearchByEntityPropertiesAsync(
    IReadOnlyDictionary<string, string> searchParams,
    CancellationToken cancellationToken);
```

Build query in client — reads `title` and `artist` from the dict, constructs Lucene syntax when both present:
```csharp
searchParams.TryGetValue("title", out var title);
searchParams.TryGetValue("artist", out var artist);

// Title only: query={encodedTitle}
// Title + artist: query=release:"{title}" AND artist:"{artist}"
var query = string.IsNullOrWhiteSpace(artist)
    ? Uri.EscapeDataString(title ?? string.Empty)
    : $"release:\"{Uri.EscapeDataString(title!)}\" AND artist:\"{Uri.EscapeDataString(artist)}\"";
```

### 8. `IOpenLibraryClient` + `OpenLibraryClient`
**Files:** `...Clients/OpenLibrary/IOpenLibraryClient.cs`, `OpenLibraryClient.cs`

Same approach — dict in, client extracts what it needs:

```csharp
Task<IReadOnlyList<BookResponse>> SearchByEntityPropertiesAsync(
    IReadOnlyDictionary<string, string> searchParams,
    CancellationToken cancellationToken);
```

Build URL in client:
```csharp
searchParams.TryGetValue("title", out var title);
searchParams.TryGetValue("author", out var author);

// Title only: search.json?q={title}&limit=10
// Title + author: search.json?title={title}&author={author}&limit=10
var url = string.IsNullOrWhiteSpace(author)
    ? $"search.json?q={Uri.EscapeDataString(title ?? string.Empty)}&limit=10"
    : $"search.json?title={Uri.EscapeDataString(title!)}&author={Uri.EscapeDataString(author)}&limit=10";
```

---

## Frontend Changes

### 9. `lookup-data.server.ts`
**File:** `MediaSet.Remix/app/api/lookup-data.server.ts`

Replace `identifierValue: string` with `searchParams: Record<string, string>`. The function builds the URL from `identifierType` + the dict — consistent with how the backend always receives a dict regardless of lookup mode:

```typescript
export async function lookup(
  entityType: string,
  identifierType: string,  // e.g. 'isbn', 'barcode', 'entity'
  searchParams: Record<string, string>
) {
  let url: string;
  if (identifierType === 'entity') {
    const qs = new URLSearchParams(searchParams).toString();
    url = `${apiUrl}/lookup/${entityType}/entity?${qs}`;
  } else {
    // Single-entry dict — extract the value by the identifier type key
    const value = searchParams[identifierType];
    url = `${apiUrl}/lookup/${entityType}/${identifierType}/${value}`;
  }
  // ... rest of function (response mapping) unchanged
}
```

All callers pass a dict. Simple lookups pass a single-entry dict (e.g. `{ isbn: '9780439708180' }`), entity lookups pass the full property dict.

### 10. Add route action (`$entity.add/route.tsx`)
**File:** `MediaSet.Remix/app/routes/$entity.add/route.tsx`

The route stays entity-agnostic — it has no knowledge of `artist`, `author`, or any entity-specific field. Additional lookup params are collected generically using a `lookupParam.{key}` naming convention in the FormData. Each form decides which of its fields to include; the route just harvests them all:

```typescript
const fieldName = formData.get('fieldName') as string;  // 'title', 'isbn', 'barcode', etc.
const identifierValue = formData.get('identifierValue') as string;

// Collect all lookupParam.* entries — entity-agnostic, forms decide what to submit
const searchParams: Record<string, string> = { [fieldName]: identifierValue };
for (const [key, value] of formData.entries()) {
  if (key.startsWith('lookupParam.') && value) {
    searchParams[key.slice('lookupParam.'.length)] = value as string;
  }
}

// Title always goes through the entity route; everything else uses its identifier type
const identifierType = fieldName === 'title'
  ? 'entity'
  : getIdentifierTypeForField(entityType, fieldName);

results = await lookup(entityType, identifierType, searchParams);
```

### 11. `music-form.tsx` — Form reorganization + artist lookup field

**Lookup group (moved to top of form):**
1. Title (with Lookup button)
2. Artist — included in lookup via `lookupParam.artist` convention
3. Barcode (with Lookup + Scan buttons)

`handleTitleLookup` uses the `lookupParam.` prefix — the route collects it without knowing what it is:
```tsx
formData.append('intent', 'lookup');
formData.append('fieldName', 'title');
formData.append('identifierValue', titleValue);
if (artistValue) formData.append('lookupParam.artist', artistValue);
```

**Remaining fields** (below lookup group): Image Upload, Image URL, Format, Release Date, Genres, Duration, Label, Tracks, Discs, Disc List.

> The Artist field does double duty: used for lookup when filled before clicking Lookup, and also populates the saved entity (`name="artist"`). Reading its value for the `lookupParam.artist` append happens inside `handleTitleLookup`.

### 12. `book-form.tsx` — Form reorganization + author lookup field

**Lookup group (moved to top of form):**
1. Title (with Lookup button)
2. Author — a plain text input that persists with the entity AND is used for lookup
3. ISBN (with Lookup + Scan buttons)

`handleTitleLookup` reads the author field value and appends it as a `lookupParam.`:
```tsx
formData.append('intent', 'lookup');
formData.append('fieldName', 'title');
formData.append('identifierValue', titleValue);
if (authorValue) formData.append('lookupParam.author', authorValue);
```

> The Author field serves double duty — it narrows the lookup when filled in before clicking Lookup, and its value is saved with the entity. Labeled simply "Author".

**Remaining fields** (below lookup group): Image Upload, Image URL, Subtitle, Format, Pages, Publication Date, Authors (multiselect), Genres, Publisher, Plot.

---

## File Change Summary

| File | Change |
|------|--------|
| `MediaSet.Api/Shared/Models/IdentifierType.cs` (or wherever enum lives) | Replace `Title` with `Entity` |
| `MediaSet.Api/Infrastructure/Lookup/Attributes/LookupPropertyAttribute.cs` | **New** — marker attribute with optional `Name` override |
| Shared entity model files (`MusicEntity`, `BookEntity`, `MovieEntity`, `GameEntity`) | Add `[LookupProperty]` to applicable properties |
| `MediaSet.Api/Infrastructure/Lookup/Strategies/ILookupStrategy.cs` | Keep `CanHandle(MediaTypes, IdentifierType)` unchanged; replace `string identifierValue` with `IReadOnlyDictionary<string,string> searchParams` in `LookupAsync`; add `SupportedProperties` to base interface |
| `MediaSet.Api/Infrastructure/Lookup/Strategies/LookupStrategyBase.cs` | **New** — abstract base class; builds `SupportedProperties` via reflection on `TEntity` |
| `MediaSet.Api/Infrastructure/Lookup/Strategies/LookupStrategyFactory.cs` | Unchanged for `GetStrategy`; add non-generic `GetStrategyBase(MediaTypes)` for entity lookup validation |
| `MediaSet.Api/Infrastructure/Lookup/Strategies/MusicLookupStrategy.cs` | Implement new interface; pass `artist` to MusicBrainz client |
| `MediaSet.Api/Infrastructure/Lookup/Strategies/BookLookupStrategy.cs` | Implement new interface; pass `author` to OpenLibrary client |
| `MediaSet.Api/Infrastructure/Lookup/Strategies/MovieLookupStrategy.cs` | Implement new interface; no behavior change |
| `MediaSet.Api/Infrastructure/Lookup/Strategies/GameLookupStrategy.cs` | Implement new interface; no behavior change |
| `MediaSet.Api/Infrastructure/Lookup/Clients/MusicBrainz/IMusicBrainzClient.cs` | Rename `SearchByTitleAsync` → `SearchByEntityPropertiesAsync`; signature takes `IReadOnlyDictionary<string,string>` |
| `MediaSet.Api/Infrastructure/Lookup/Clients/MusicBrainz/MusicBrainzClient.cs` | Same rename; reads `title`/`artist` from dict, builds Lucene query |
| `MediaSet.Api/Infrastructure/Lookup/Clients/OpenLibrary/IOpenLibraryClient.cs` | Rename `SearchByTitleAsync` → `SearchByEntityPropertiesAsync`; signature takes `IReadOnlyDictionary<string,string>` |
| `MediaSet.Api/Infrastructure/Lookup/Clients/OpenLibrary/OpenLibraryClient.cs` | Same rename; reads `title`/`author` from dict, builds URL |
| `MediaSet.Api/Features/Lookup/Endpoints/LookupApi.cs` | Make `{value}` optional; branch on `identifierType == "entity"` for query-param dict path |
| `MediaSet.Remix/app/api/lookup-data.server.ts` | Replace `identifierValue: string` with `searchParams: Record<string,string>`; build `/entity?...` URL when identifierType is `'entity'`, otherwise extract single value from dict |
| `MediaSet.Remix/app/routes/$entity.add/route.tsx` | Collect `lookupParam.*` entries generically; title always routes to `'entity'` identifierType |
| `MediaSet.Remix/app/components/music-form.tsx` | Reorder fields; append `lookupParam.artist` in `handleTitleLookup` |
| `MediaSet.Remix/app/components/book-form.tsx` | Reorder fields; add plain-text Author field (persists + used for lookup); append `lookupParam.author` in `handleTitleLookup` |

---

## Resolved Design Decisions

1. **Author field** — persists with the entity. Labeled "Author", not "Author (for search)". Same double-duty pattern as Artist on the music form.
2. **Title-only routing** — all title lookups go through `/entity?title=value`, with or without additional params. The old `IdentifierType.Title` / `/title/{value}` path is completely removed. `fieldName === 'title'` always maps to `identifierType = 'entity'` in the route action.
