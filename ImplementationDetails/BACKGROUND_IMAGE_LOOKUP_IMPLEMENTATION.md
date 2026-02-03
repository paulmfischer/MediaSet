# Background Image Lookup Implementation Plan

## Related Issue/Task

**GitHub Issue**: [#434](https://github.com/paulmfischer/MediaSet/issues/434)

## Overview

This feature adds a background service that automatically finds and downloads cover images for entities that are missing them. The service will periodically scan the database for entities without images and attempt to retrieve images from external APIs (Open Library, TMDb, GiantBomb, MusicBrainz).

The background service will be configurable to:
- Configure how frequently the service runs
- Rate-limit API calls to avoid overwhelming external services or getting blocked
- Track which entities have been attempted previously to avoid repeated failures
- Auto-detect which entity types to process based on configured lookup strategies

## Goals

1. **Automated image enrichment**: Automatically populate missing cover images without manual user intervention
2. **Respectful API usage**: Rate-limit requests to external services to avoid being blocked
3. **Efficient operation**: Don't repeatedly query for entities that have no available images
4. **Background execution**: Run without impacting API performance or user experience
5. **Configurable control**: Allow users to customize behavior per entity type and scheduling

## Requirements

### Functional Requirements

1. **Background Service**
   - Implement as .NET `BackgroundService` that runs on a configurable schedule
   - Check for entities missing `CoverImage` (where `CoverImage` is `null`)
   - Process entities in small batches to avoid overwhelming the system
   - Log progress and results for observability

2. **Image Lookup Integration**
   - Use existing lookup strategies (`BookLookupStrategy`, `MovieLookupStrategy`, etc.) to find images
   - Extract `ImageUrl` from lookup response
   - Download and save image using existing `ImageService`
   - Update entity with `CoverImage` data

3. **Rate Limiting**
   - Configure maximum requests per minute per entity type
   - Add delays between requests to spread load over time
   - Support chunking large operations across multiple runs if needed

4. **Runtime Management**
   - Configure maximum runtime duration to prevent infinite operations
   - Service stops processing when max runtime is reached, resuming in next scheduled run
   - Ensures predictable resource usage and prevents runaway processes

6. **Tracking Attempted Lookups**
   - Add `ImageLookup` object to entity models containing lookup metadata:
     - `LookupAttemptedAt` (DateTime?) - when the lookup was attempted
     - `FailureReason` (string?) - error message if lookup failed, null if succeeded
     - `PermanentFailure` (bool) - indicates lookup should never be retried (e.g., no identifier exists)
   - After first lookup attempt (success or failure), populate the `ImageLookup` object on the entity
   - Skip entities where `ImageLookup` is not null during normal runs
   - Skip entities where `ImageLookup.PermanentFailure` is true even if forced retry is requested
   - Encapsulates all lookup metadata in a single cohesive object

7. **Existing ImageUrl Handling**
   - If an entity has `ImageUrl` populated but no `CoverImage`, attempt to download from `ImageUrl` first
   - Only perform external API lookup if `ImageUrl` download fails or `ImageUrl` is empty
   - Gives priority to user-provided URLs over external lookups

9. **Configuration**
   - Schedule interval using cron expressions (e.g., `0 2 * * *` for daily at 2 AM)
   - **Minimum interval: 1 hour** (no sub-hourly scheduling allowed)
   - Maximum runtime duration to prevent infinite operations
   - Batch size (how many entities to process per run) - distributed proportionally across enabled entity types
   - Rate limits (requests per minute) - shared across all enabled entity types
   - Auto-detects which entity types to process based on registered lookup strategies
   - Configuration validated at startup to reject invalid cron expressions

10. **Proportional Batch Allocation**
    - Batch size is distributed evenly across all entity types with available lookup strategies
    - Example: If batch size is 40 and 4 entity types are enabled, process 10 from each type
    - If batch size doesn't divide evenly, remaining slots are distributed to types with more pending entities
    - Ensures fair processing across all media types rather than exhausting one type before moving to the next

### Non-Functional Requirements

1. **Performance**
   - Background service should not impact API response times
   - Use async/await for all I/O operations
   - Process entities in small batches (e.g., 10-50 at a time)

2. **Reliability**
   - Handle errors gracefully (network failures, API rate limits, invalid data)
   - Continue processing remaining entities if one fails
   - Log all errors for debugging

3. **Observability**
   - Log when service starts/stops
   - Log number of entities processed, succeeded, failed
   - Log rate limit delays
   - Expose metrics via existing logging infrastructure

4. **Resource Management**
   - Use cancellation tokens for graceful shutdown
   - Limit concurrent requests to external APIs
   - Clean up resources properly (HTTP clients, streams)

## Technical Architecture

### Components

#### 1. BackgroundImageLookupService

A hosted background service that runs periodically to find and download images.

```csharp
public class BackgroundImageLookupService : BackgroundService
{
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly IOptions<BackgroundImageLookupConfiguration> _config;
    private readonly ILogger<BackgroundImageLookupService> _logger;
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Main loop: run on schedule
        // For each enabled entity type:
        //   - Query for entities where CoverImage is null and ImageLookup is null
        //   - Process in batches
        //   - For each entity:
        //     - Lookup metadata using existing strategy
        //     - Extract ImageUrl
        //     - Download and save image
        //     - Update entity with CoverImage and ImageLookup
        //     - Apply rate limiting delays
    }
}
```

**Key Responsibilities:**
- Schedule-based execution
- Batch processing
- Error handling and logging
- Rate limiting enforcement
- Graceful shutdown on cancellation

#### 2. IImageLookupService

A service that orchestrates the lookup process for a single entity using the LookupStrategyFactory.

```csharp
public interface IImageLookupService
{
    Task<ImageLookupResult> LookupAndSaveImageAsync(
        IEntity entity,
        MediaTypes mediaType,
        CancellationToken cancellationToken);
}

public record ImageLookupResult(
    bool Success,
    string? ImageUrl,
    string? ErrorMessage,
    bool PermanentFailure = false
);
```

**Key Responsibilities:**
- Use `LookupStrategyFactory` to get the appropriate lookup strategy for the entity type
- Extract identifier from entity using reflection to find property marked with `[LookupIdentifier]` attribute
- Call lookup strategy with entity identifier
- Extract ImageUrl from response
- Download image using `ImageService.DownloadAndSaveImageAsync`
- Update entity with CoverImage
- Return result status

#### 3. BackgroundImageLookupConfiguration

Configuration class for the background service.

```csharp
public class BackgroundImageLookupConfiguration
{
    public bool Enabled { get; set; } = false;
    public string Schedule { get; set; } = "0 2 * * *"; // Daily at 2 AM
    public int MaxRuntimeMinutes { get; set; } = 60; // Maximum runtime per scheduled run

    public int BatchSize { get; set; } = 25;
    public int RequestsPerMinute { get; set; } = 30;
}
```

The batch size is distributed proportionally across all entity types with available lookup strategies. For example, if `BatchSize` is 40 and 4 entity types have strategies, 10 entities from each type will be processed.

The service automatically detects which entity types to process by attempting to retrieve a lookup strategy via `LookupStrategyFactory` for each media type. If a strategy exists for a media type, that entity type will be processed.

#### 4. Entity Model Updates

Add `ImageLookup` object to all entity models.

```csharp
public class ImageLookup
{
    /// <summary>
    /// The timestamp when the background lookup service attempted to retrieve an image.
    /// </summary>
    public DateTime LookupAttemptedAt { get; set; }

    /// <summary>
    /// If the lookup failed, contains the error message explaining why the image could not be retrieved.
    /// Null if lookup succeeded.
    /// </summary>
    public string? FailureReason { get; set; }

    /// <summary>
    /// Indicates that lookup should never be retried for this entity.
    /// Set to true when the entity lacks required identifiers (e.g., no ISBN, no Barcode).
    /// </summary>
    public bool PermanentFailure { get; set; } = false;
}

public class Book : IEntity
{
    // ... existing properties ...

    public ImageLookup? ImageLookup { get; set; }
}
```

Update `IEntity` interface to include this property.

#### 5. Identifier Extraction Strategy

To enable `ImageLookupService` to work generically with any entity type, we need a way to identify which property contains the lookup identifier. Currently:
- **Books**: Use `ISBN` property
- **Movies, Games, Music**: Use `Barcode` property

Implement an attribute-based approach to mark the lookup identifier field:

```csharp
[AttributeUsage(AttributeTargets.Property)]
public class LookupIdentifierAttribute : Attribute
{
}

public class Book : IEntity
{
    [LookupIdentifier]
    public string? ISBN { get; set; }
    // ... other properties
}

public class Movie : IEntity
{
    [LookupIdentifier]
    public string? Barcode { get; set; }
    // ... other properties
}
```

The `ImageLookupService` will use reflection to find the property marked with `[LookupIdentifier]` and extract its value for lookup operations.

### Database Queries

**Find entities without images:**

```csharp
var filter = Builders<TEntity>.Filter.And(
    Builders<TEntity>.Filter.Eq(e => e.CoverImage, null),
    Builders<TEntity>.Filter.Eq(e => e.ImageLookup, null)
);

var entities = await _entityCollection
    .Find(filter)
    .Limit(batchSize)
    .ToListAsync(cancellationToken);
```

**Update entity after successful lookup:**

```csharp
var imageLookup = new ImageLookup
{
    LookupAttemptedAt = DateTime.UtcNow,
    FailureReason = null
};

var update = Builders<TEntity>.Update
    .Set(e => e.CoverImage, image)
    .Set(e => e.ImageLookup, imageLookup);

await _entityCollection.UpdateOneAsync(
    e => e.Id == entity.Id,
    update,
    cancellationToken: cancellationToken);
```

**Update entity after failed lookup:**

```csharp
var imageLookup = new ImageLookup
{
    LookupAttemptedAt = DateTime.UtcNow,
    FailureReason = errorMessage,
    PermanentFailure = false
};

var update = Builders<TEntity>.Update
    .Set(e => e.ImageLookup, imageLookup);

await _entityCollection.UpdateOneAsync(
    e => e.Id == entity.Id,
    update,
    cancellationToken: cancellationToken);
```

**Update entity with permanent failure (no identifier):**

```csharp
var imageLookup = new ImageLookup
{
    LookupAttemptedAt = DateTime.UtcNow,
    FailureReason = "Entity lacks required lookup identifier",
    PermanentFailure = true
};

var update = Builders<TEntity>.Update
    .Set(e => e.ImageLookup, imageLookup);

await _entityCollection.UpdateOneAsync(
    e => e.Id == entity.Id,
    update,
    cancellationToken: cancellationToken);
```

### Rate Limiting Strategy

Implement simple time-based rate limiting:

```csharp
private async Task ApplyRateLimitAsync(int requestsPerMinute, CancellationToken cancellationToken)
{
    var delayMs = (int)(60_000.0 / requestsPerMinute);
    await Task.Delay(delayMs, cancellationToken);
}
```

For more sophisticated rate limiting, consider using a sliding window or token bucket algorithm.

### Scheduling Strategy

Use cron expressions parsed via the `Cronos` library for maximum flexibility:

```csharp
protected override async Task ExecuteAsync(CancellationToken stoppingToken)
{
    while (!stoppingToken.IsCancellationRequested)
    {
        var now = DateTime.UtcNow;
        var nextRun = CronExpression.Parse(_config.Value.Schedule)
            .GetNextOccurrence(now, TimeZoneInfo.Utc);
        var delay = nextRun - now;

        await Task.Delay(delay, stoppingToken);

        if (!stoppingToken.IsCancellationRequested)
        {
            var runStartTime = DateTime.UtcNow;
            var maxRuntime = TimeSpan.FromMinutes(_config.Value.MaxRuntimeMinutes);

            await ProcessAllEntityTypesAsync(runStartTime, maxRuntime, stoppingToken);
        }
    }
}
```

**Cron Expression Validation:**
- Only allow expressions with a minimum interval of 1 hour or more between runs
- Reject minute-level intervals (e.g., `*/5 * * * *` is not allowed)
- Valid examples:
  - `0 2 * * *` = Every day at 2 AM ✓
  - `0 */6 * * *` = Every 6 hours ✓
  - `0 0 * * 1` = Every Monday at midnight ✓
  - `*/30 * * * *` = Every 30 minutes ✗ (too frequent)
  - `*/15 * * * *` = Every 15 minutes ✗ (too frequent)

Validation logic:
```csharp
private bool IsValidCronExpression(string cronExpression)
{
    try
    {
        var expression = CronExpression.Parse(cronExpression);
        var now = DateTime.UtcNow;
        var run1 = expression.GetNextOccurrence(now, TimeZoneInfo.Utc);
        var run2 = expression.GetNextOccurrence(run1.Value.AddSeconds(1), TimeZoneInfo.Utc);
        
        var interval = run2 - run1;
        return interval.TotalHours >= 1;
    }
    catch
    {
        return false;
    }
}
```

Validate during configuration binding or startup to fail fast if an invalid schedule is provided.

## Proposed Changes

### Backend Changes (MediaSet.Api)

#### New Files

**Attributes/LookupIdentifierAttribute.cs**
```csharp
using System;

namespace MediaSet.Api.Attributes;

[AttributeUsage(AttributeTargets.Property)]
public class LookupIdentifierAttribute : Attribute
{
}
```

**Models/ImageLookup.cs**
```csharp
namespace MediaSet.Api.Models;

public class ImageLookup
{
    /// <summary>
    /// The timestamp when the background lookup service attempted to retrieve an image.
    /// </summary>
    public DateTime LookupAttemptedAt { get; set; }

    /// <summary>
    /// If the lookup failed, contains the error message explaining why the image could not be retrieved.
    /// Null if lookup succeeded.
    /// </summary>
    public string? FailureReason { get; set; }

    /// <summary>
    /// Indicates that lookup should never be retried for this entity.
    /// Set to true when the entity lacks required identifiers (e.g., no ISBN, no Barcode).
    /// </summary>
    public bool PermanentFailure { get; set; } = false;
}
```

**Models/BackgroundImageLookupConfiguration.cs**
```csharp
namespace MediaSet.Api.Models;

public class BackgroundImageLookupConfiguration
{
    public bool Enabled { get; set; } = false;
    public string Schedule { get; set; } = "0 2 * * *";
    public int MaxRuntimeMinutes { get; set; } = 60;

    public int BatchSize { get; set; } = 25;
    public int RequestsPerMinute { get; set; } = 30;
}
```

**Services/IImageLookupService.cs**
```csharp
namespace MediaSet.Api.Services;

public interface IImageLookupService
{
    Task<ImageLookupResult> LookupAndSaveImageAsync(
        IEntity entity,
        MediaTypes mediaType,
        CancellationToken cancellationToken);
}

public record ImageLookupResult(
    bool Success,
    string? ImageUrl,
    string? ErrorMessage,
    bool PermanentFailure = false
);
```

**Services/ImageLookupService.cs**
```csharp
namespace MediaSet.Api.Services;

public class ImageLookupService : IImageLookupService
{
    private readonly LookupStrategyFactory _strategyFactory;
    private readonly IImageService _imageService;
    private readonly ILogger<ImageLookupService> _logger;

    public async Task<ImageLookupResult> LookupAndSaveImageAsync(
        IEntity entity,
        MediaTypes mediaType,
        CancellationToken cancellationToken)
    {
        // 1. Check if entity has existing ImageUrl - if so, try downloading from it first
        // 2. If ImageUrl download succeeds, return success
        // 3. Use reflection to find property marked with [LookupIdentifier] attribute
        // 4. If no identifier found or value is empty, return failure with PermanentFailure = true
        // 5. Extract identifier value from entity
        // 6. Get appropriate lookup strategy from factory based on media type
        // 7. Call lookup strategy with identifier
        // 8. Extract ImageUrl from response
        // 9. If ImageUrl exists, download and save image
        // 10. Update entity with CoverImage
        // 11. Return result
    }
}
```

**Services/BackgroundImageLookupService.cs**
```csharp
namespace MediaSet.Api.Services;

public class BackgroundImageLookupService : BackgroundService
{
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly IOptions<BackgroundImageLookupConfiguration> _config;
    private readonly ILogger<BackgroundImageLookupService> _logger;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Main background service loop
        // For each MediaTypes enum value (Books, Movies, Games, Music):
        //   - Try to get lookup strategy using LookupStrategyFactory
        //   - If strategy exists, process that entity type
        //   - If strategy doesn't exist (NotSupportedException), skip that type
        //   - Track elapsed time and stop if MaxRuntimeMinutes is exceeded
    }

    private async Task ProcessAllEntityTypesAsync(CancellationToken cancellationToken)
    {
        // Record start time for max runtime enforcement
        // Determine which entity types have available strategies
        // Calculate per-type batch limit (BatchSize / number of enabled types)
        // Iterate through enabled MediaTypes
        // For each type, process up to per-type limit
        // Check elapsed time after each entity, stop if MaxRuntimeMinutes exceeded
    }

    private async Task<int> ProcessEntityTypeAsync<TEntity>(
        MediaTypes mediaType,
        int limit,
        DateTime startTime,
        CancellationToken cancellationToken)
        where TEntity : IEntity, new()
    {
        // Query for entities of this type missing images (limit to per-type allocation)
        // For each entity:
        //   - Check if MaxRuntimeMinutes exceeded, return early if so
        //   - Call IImageLookupService.LookupAndSaveImageAsync
        //   - Apply rate limiting
        // Return count of entities processed
    }
}
```

#### Modified Files

**Models/IEntity.cs**

Add new property:
```csharp
public interface IEntity
{
    // ... existing properties ...
    public ImageLookup? ImageLookup { get; set; }
}
```

**Models/Book.cs, Movie.cs, Game.cs, Music.cs**

Add property:
```csharp
public ImageLookup? ImageLookup { get; set; }
```

Add `[LookupIdentifier]` attribute to lookup field:
- Book: Add to `ISBN` property
- Movie, Game, Music: Add to `Barcode` property

Update `IsEmpty()` if needed.

**Program.cs**

Register new services:
```csharp
// Configure background image lookup
builder.Services.Configure<BackgroundImageLookupConfiguration>(
    builder.Configuration.GetSection(nameof(BackgroundImageLookupConfiguration)));

// Register service
builder.Services.AddScoped<IImageLookupService, ImageLookupService>();

// Register background service only if enabled
var backgroundConfig = builder.Configuration
    .GetSection(nameof(BackgroundImageLookupConfiguration))
    .Get<BackgroundImageLookupConfiguration>();

if (backgroundConfig?.Enabled == true)
{
    builder.Services.AddHostedService<BackgroundImageLookupService>();
    bootstrapLogger.Information("Background image lookup service enabled");
}
```

The service will automatically detect which lookup strategies are registered (BookLookupStrategy, MovieLookupStrategy, GameLookupStrategy, MusicLookupStrategy) and process only those entity types.

**appsettings.json**

Add configuration section:
```json
{
  "BackgroundImageLookupConfiguration": {
    "Enabled": false,
    "Schedule": "0 2 * * *",
    "MaxRuntimeMinutes": 60,
    "BatchSize": 25,
    "RequestsPerMinute": 30
  }
}
```

**appsettings.Development.json**

Override for development:
```json
{
  "BackgroundImageLookupConfiguration": {
    "Enabled": false
  }
}
```

### Testing Changes

#### Backend Tests (MediaSet.Api.Tests)

**Services/ImageLookupServiceTests.cs**
- Test successful image lookup and save
- Test entity without required identifier (no ISBN, UPC, etc.) sets `PermanentFailure = true`
- Test lookup returns no ImageUrl
- Test image download fails
- Test entity update after successful lookup
- Test downloading from existing `ImageUrl` before external lookup
- Test error handling

**Services/BackgroundImageLookupServiceTests.cs**
- Test service initialization
- Test processing batch of entities
- Test proportional batch allocation across entity types
- Test rate limiting delays
- Test max runtime enforcement stops processing
- Test skipping entities with ImageLookup != null
- Test skipping entities with `PermanentFailure = true`
- Test error handling for individual failures
- Test cancellation token handling
- Test configuration (enabled/disabled per entity type)
- Test ImageLookup object population on success and failure

## Implementation Steps

1. **Add entity model fields**
   - Create `LookupIdentifierAttribute` for marking lookup identifier properties
   - Create `ImageLookup` class with `LookupAttemptedAt`, `FailureReason`, and `PermanentFailure`
   - Update `IEntity` interface with `ImageLookup` property
   - Update all entity models (Book, Movie, Game, Music) to add `ImageLookup` property
   - Add `[LookupIdentifier]` attribute to ISBN (Book) and Barcode (Movie, Game, Music) properties
   - Test database serialization

2. **Create configuration classes**
   - Implement `BackgroundImageLookupConfiguration`
   - Implement `EntityTypeConfiguration`
   - Add to `appsettings.json`

3. **Implement ImageLookupService**
   - Create `IImageLookupService` interface
   - Implement `ImageLookupService` with lookup strategy orchestration
   - Add unit tests

4. **Implement BackgroundImageLookupService**
   - Create hosted background service
   - Implement scheduling logic
   - Implement batch processing
   - Implement rate limiting
   - Add logging and error handling
   - Add unit tests

5. **Integration testing**
   - Test end-to-end with real MongoDB
   - Test with rate limiting
   - Test manual trigger
   - Test graceful shutdown

8. **Documentation**
   - Update README with background service configuration
   - Add troubleshooting guide

## Configuration Examples

### Production Configuration (Enabled, Conservative Rate Limits)

```json
{
  "BackgroundImageLookupConfiguration": {
    "Enabled": true,
    "Schedule": "0 3 * * *",
    "MaxRuntimeMinutes": 60,
    "BatchSize": 40,
    "RequestsPerMinute": 20
  }
}
```

### Development Configuration (Disabled)

```json
{
  "BackgroundImageLookupConfiguration": {
    "Enabled": false
  }
}
```

### Testing Configuration (Hourly, Small Batches)

```json
{
  "BackgroundImageLookupConfiguration": {
    "Enabled": true,
    "Schedule": "0 * * * *",
    "MaxRuntimeMinutes": 30,
    "BatchSize": 20,
    "RequestsPerMinute": 60
  }
}
```

## Acceptance Criteria

- [ ] `LookupIdentifierAttribute` created for marking lookup identifier properties
- [ ] `ImageLookup` class created with `LookupAttemptedAt`, `FailureReason`, and `PermanentFailure` properties
- [ ] `BackgroundImageLookupConfiguration` class created with schedule, rate limit, and `MaxRuntimeMinutes` settings
- [ ] `IEntity` interface updated with `ImageLookup` property
- [ ] All entity models (Book, Movie, Game, Music) updated with `ImageLookup` property
- [ ] `[LookupIdentifier]` attribute added to ISBN (Book) and Barcode (Movie, Game, Music) properties
- [ ] `ImageLookupService` uses reflection to extract identifier from property marked with `[LookupIdentifier]`
- [ ] `IImageLookupService` interface and implementation created to use `LookupStrategyFactory`
- [ ] `BackgroundImageLookupService` hosted service created with scheduled execution
- [ ] Background service uses `LookupStrategyFactory` to detect which entity types have strategies
- [ ] Background service queries for entities where `CoverImage` is null and `ImageLookup` is null
- [ ] Background service distributes batch size proportionally across enabled entity types
- [ ] Background service applies rate limiting (configurable requests per minute)
- [ ] Background service stops processing when `MaxRuntimeMinutes` is reached
- [ ] Background service attempts to download from existing `ImageUrl` before external lookup
- [ ] Background service populates `ImageLookup` object after first attempt (success or failure)
- [ ] Background service sets `ImageLookup.PermanentFailure = true` when entity lacks required identifier
- [ ] Background service updates entity with `CoverImage` on successful lookup
- [ ] Background service populates `ImageLookup.FailureReason` on failed lookup attempts
- [ ] Background service logs progress and errors
- [ ] Background service handles cancellation gracefully
- [ ] Configuration added to `appsettings.json` with default values
- [ ] Background service only registered when `Enabled = true` in configuration
- [ ] Background service only processes entity types for which `LookupStrategyFactory` has a strategy
- [ ] Unit tests for `ImageLookupService` cover success and error scenarios
- [ ] Unit tests for `BackgroundImageLookupService` cover batch processing, rate limiting, and max runtime
- [ ] Integration tests verify end-to-end image lookup and entity updates
- [ ] Documentation updated with configuration examples
- [ ] README includes background service setup instructions

## Open Questions

1. **Should the background service auto-detect lookup strategies?**
   - **Recommendation**: Yes, use `LookupStrategyFactory` to check if a lookup strategy is available for each media type
   - This mirrors the existing pattern in Program.cs where lookup endpoints are conditionally registered
   - Eliminates need for per-entity-type configuration flags
   - Strategy unavailability (NotSupportedException) indicates that entity type should be skipped

2. **Should we support parallel processing of multiple entity types?**
   - **Recommendation**: No, process sequentially to simplify implementation and avoid resource contention
   - Can add parallel processing later if needed

3. **How should we handle API rate limit errors from external services?**
   - **Recommendation**: Catch rate limit exceptions, log warning, and continue with next entity
   - Don't mark as attempted if rate limited (let next run retry)

4. **Should the background service validate images before saving?**
    - **Recommendation**: Yes, use existing `ImageService` validation (MIME type, file size)
    - Existing validation covers security and storage concerns

5. **Should we support custom cron expressions or simple interval-based scheduling?**
   - **Recommendation**: Use cron expressions for maximum flexibility
   - Cron format (`0 2 * * *`) allows complex scheduling patterns (specific days, hours, multiple intervals)
   - Simple interval-based would only allow fixed intervals (e.g., "every 24 hours")
   - Current configuration already uses cron expressions, so use a library like `Cronos` for parsing
   - Cron approach supports future use cases like "every Monday at 2 AM" or "every 4 hours"

## Risks and Mitigations

| Risk | Impact | Mitigation |
|------|--------|------------|
| External API rate limiting | Service gets blocked, no images retrieved | Implement conservative rate limits, add exponential backoff, log rate limit errors |
| Background service impacts API performance | Slow API responses | Use small batch sizes, add delays between requests, run during off-peak hours |
| Repeated failures for same entities | Wasted API calls | Track attempted entities with `ImageLookup`, mark permanent failures, don't retry unless forced |
| Service runs indefinitely | Resource exhaustion, overlapping runs | Enforce `MaxRuntimeMinutes` limit, stop processing and resume in next scheduled run |
| Service crashes or stops unexpectedly | No automated image lookup | Add health checks, comprehensive error handling, monitoring |
| Invalid cron expression configuration | Service fails to start | Validate cron expressions at startup, require minimum 1-hour interval |
| External API responses change format | Image lookup fails | Add validation for response structure, log parsing errors |
| Lookup strategy not available for media type | Entities of that type skipped without error | `LookupStrategyFactory` throws NotSupportedException, catch and log which strategies are available at startup |
| Entity lacks required identifier | Repeated failed lookups for same entity | Set `PermanentFailure = true` to prevent future attempts |

## Future Enhancements

1. **Parallel processing**: Process multiple entity types concurrently
2. **Priority queue**: Allow marking specific entities as high-priority for lookup
3. **Retry strategy**: Implement exponential backoff for failed lookups
4. **Image quality selection**: Configure preferred image size/quality
5. **Detailed history**: Create separate collection to track all lookup attempts
6. **Smart scheduling**: Adjust schedule based on success rate (more frequent if finding many images)
7. **External service health**: Check service availability before attempting lookups
8. **Batch download optimization**: Download multiple images in parallel
9. **Cron expression support**: Allow flexible scheduling using cron expressions

## Dependencies

- **Existing Services**:
  - `IImageService` (download and save images)
  - `IEntityService<TEntity>` (query and update entities)
  - Lookup strategies (`BookLookupStrategy`, `MovieLookupStrategy`, etc.)
  
- **External Libraries**:
  - `Microsoft.Extensions.Hosting` (for BackgroundService)
  - `Cronos` (for cron expression parsing and validation)
  
- **External APIs**:
  - Open Library (books)
  - TMDb (movies)
  - GiantBomb (games)
  - MusicBrainz (music)

## Success Metrics

- **Coverage**: Percentage of entities with cover images increases over time
- **Success Rate**: Percentage of lookup attempts that successfully find and save images
- **Performance**: Background service completes batches within configured intervals
- **Reliability**: Service runs continuously without crashes or hangs
- **API Health**: No rate limit blocks from external services
