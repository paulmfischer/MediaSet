# Image Folder Statistics Implementation (Issue #526)

## Context
Add a feature to view statistics about images stored on the filesystem. This includes total file count/size, per-entity-type breakdowns, orphaned files (files on disk without an entity reference), and broken links (entities with a `CoverImage.FilePath` pointing to a missing file). Data is computed on-demand and cached (same pattern as `StatsService`) — no background service needed since operations are lightweight for a personal media collection. A new UI screen displays these stats, including actionable lists of broken links and orphaned files.

---

## Backend Changes

### 1. Add `ListFiles()` to `IImageStorageProvider`
**File:** `MediaSet.Api/Infrastructure/Storage/IImageStorageProvider.cs`
**File:** `MediaSet.Api/Infrastructure/Storage/LocalFileStorageProvider.cs`

Add method:
```csharp
IEnumerable<(string RelativePath, long SizeBytes)> ListFiles();
```
Implementation in `LocalFileStorageProvider`: enumerate all files recursively under `_rootPath`, return relative path (using `Path.GetRelativePath(_rootPath, file)`) and `FileInfo.Length`.

---

### 2. New supporting records

**File:** `MediaSet.Api/Features/Statistics/Models/ImageStats.cs`

```csharp
// An entity whose CoverImage.FilePath points to a file that doesn't exist
public record BrokenImageLink(
    string EntityId,
    string EntityType,
    string Title,
    string MissingFilePath
);

// A file on disk not referenced by any entity's CoverImage.FilePath
public record OrphanedImageFile(
    string RelativePath,
    long SizeBytes
);

public record ImageStats(
    int TotalFiles,
    long TotalSizeBytes,
    Dictionary<string, int> FilesByEntityType,
    Dictionary<string, long> SizeByEntityType,
    IReadOnlyList<BrokenImageLink> BrokenLinks,
    IReadOnlyList<OrphanedImageFile> OrphanedFiles,
    DateTime LastUpdated
);
```

---

### 3. New service interface: `IImageStatsService`
**File:** `MediaSet.Api/Features/Statistics/Services/IImageStatsService.cs`
```csharp
public interface IImageStatsService
{
    Task<ImageStats?> GetImageStatsAsync(CancellationToken cancellationToken = default);
}
```

---

### 4. New service: `ImageStatsService`
**File:** `MediaSet.Api/Features/Statistics/Services/ImageStatsService.cs`

**Dependencies:** `IImageStorageProvider`, `IEntityService<Book>`, `IEntityService<Movie>`, `IEntityService<Game>`, `IEntityService<Music>`, `ICacheService`, `IOptions<CacheSettings>`, `ILogger<ImageStatsService>`

**`GetImageStatsAsync`:** Check cache key `"image-stats"`, return cached if found. Otherwise:
1. Call `storageProvider.ListFiles()` → get all files with sizes
2. Group by first path segment (entity type directory, e.g. `"books"`) for `FilesByEntityType` and `SizeByEntityType`
3. Compute `TotalFiles`, `TotalSizeBytes`
4. Load all entities via `GetListAsync()` for all 4 types
5. **Broken links:** for each entity with `CoverImage != null`, check `!storageProvider.Exists(CoverImage.FilePath)` → build `BrokenImageLink` list with `EntityId`, `EntityType` (lowercased media type), `Title`, `MissingFilePath`
6. **Orphaned files:** build a `HashSet` of all referenced `CoverImage.FilePath` values, then find storage files not in that set → build `OrphanedImageFile` list with `RelativePath` and `SizeBytes`
7. Cache result using `CacheSettings.StatsCacheDurationMinutes`
8. Return `ImageStats`

---

### 5. Update `StatsApi.cs`
**File:** `MediaSet.Api/Features/Statistics/Endpoints/StatsApi.cs`

Add to existing `/stats` group:
```csharp
group.MapGet("/images", async (IImageStatsService imageStatsService, CancellationToken ct) =>
{
    var stats = await imageStatsService.GetImageStatsAsync(ct);
    return stats is not null ? Results.Ok(stats) : Results.NoContent();
});
```

---

### 6. Update `Program.cs`
**File:** `MediaSet.Api/Program.cs`

- `builder.Services.AddScoped<IImageStatsService, ImageStatsService>()`

---

## Frontend Changes

### 7. New data file: `app/api/image-stats-data.ts`
**File:** `MediaSet.Remix/app/api/image-stats-data.ts`

```ts
type BrokenImageLink = {
  entityId: string;
  entityType: string;
  title: string;
  missingFilePath: string;
};

type OrphanedImageFile = {
  relativePath: string;
  sizeBytes: number;
};

type ImageStats = {
  totalFiles: number;
  totalSizeBytes: number;
  filesByEntityType: Record<string, number>;
  sizeByEntityType: Record<string, number>;
  brokenLinks: BrokenImageLink[];
  orphanedFiles: OrphanedImageFile[];
  lastUpdated: string;
};

export async function getImageStats(): Promise<ImageStats | null> { ... }
```
Calls `${baseUrl}/stats/images`. Returns `null` on 204/error.

### 8. New route: `app/routes/image-stats.tsx`
**File:** `MediaSet.Remix/app/routes/image-stats.tsx`

- `loader` calls `getImageStats()`
- Summary `StatCard` components: Total Files, Total Size (formatted as MB/GB), per-entity-type file counts
- **Broken Links section**: table/list of `BrokenImageLink` entries — shows Entity Type, Title, Entity Id (linkable to detail page), missing file path
- **Orphaned Files section**: table/list of `OrphanedImageFile` entries — shows relative path, file size
- Use `HardDrive` icon from lucide-react for page header
- Handle null/empty state gracefully

### 9. Update navigation in `root.tsx`
**File:** `MediaSet.Remix/app/root.tsx`

Add nav link in both desktop and mobile menus, following existing `NavLink` pattern:
```tsx
<NavLink to="/image-stats" className="p-3 flex gap-2 items-center rounded-lg">
  <HardDrive /> Images
</NavLink>
```
Import `HardDrive` from `lucide-react`.

---

## Testing

- **Backend unit test**: Add `ImageStatsServiceTests.cs` in `MediaSet.Api.Tests/` following existing NUnit patterns
- **Frontend unit test**: Add `image-stats-data.test.ts` following `stats-data.test.ts` patterns (mock `apiFetch`)
- **Build verification**: `dotnet build`
- **Manual test**: Run dev stack, navigate to `/image-stats`, verify stats load including broken links and orphaned files lists
- **API test**: `GET /stats/images` returns 200 with populated JSON including arrays
