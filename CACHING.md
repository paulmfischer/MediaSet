# Caching Implementation

## Overview

MediaSet API implements in-memory caching to improve performance for frequently accessed, computationally expensive operations. The caching layer uses ASP.NET Core's `IMemoryCache` to cache metadata queries and statistics calculations.

## What is Cached

### 1. Metadata Queries (`/metadata/{media}/{property}`)
Metadata queries fetch all entities of a specific type and extract distinct values for a property (e.g., all unique authors, genres, or formats). These queries can be expensive as the collection grows.

**Cache Key Pattern**: `metadata:{MediaType}:{PropertyName}`

**Examples**:
- `metadata:Books:Authors` - All unique book authors
- `metadata:Movies:Genres` - All unique movie genres
- `metadata:Games:Platform` - All unique game platforms

### 2. Statistics (`/stats`)
Statistics aggregate data across all books, movies, and games to calculate totals, counts, and other metrics. This requires fetching and processing all entities.

**Cache Key**: `stats`

## Cache Configuration

Cache behavior is configured in `appsettings.json` and `appsettings.Development.json`:

```json
{
  "CacheSettings": {
    "EnableCaching": true,
    "DefaultCacheDurationMinutes": 10,
    "MetadataCacheDurationMinutes": 10,
    "StatsCacheDurationMinutes": 10
  }
}
```

### Configuration Options

- **EnableCaching**: Global switch to enable/disable caching (default: `true`)
- **DefaultCacheDurationMinutes**: Default TTL for cache entries without specific duration (default: `10`)
- **MetadataCacheDurationMinutes**: TTL for metadata query results (default: `10`)
- **StatsCacheDurationMinutes**: TTL for statistics calculations (default: `5`)

**Development Environment**: Shorter TTLs (5 minutes) for faster cache refresh during development.

**Production Environment**: Longer TTLs (10 minutes) for better performance.

## Cache Invalidation

Caches are automatically invalidated when data changes to ensure consistency. The system uses a proactive invalidation strategy.

### Invalidation Triggers

Cache invalidation occurs in `EntityService<TEntity>` for the following operations:

1. **CreateAsync** - Creating a new entity
2. **UpdateAsync** - Updating an existing entity
3. **RemoveAsync** - Deleting an entity
4. **BulkCreateAsync** - Bulk creating entities (e.g., CSV upload)

### Invalidation Scope

When any entity changes:

- **Metadata caches**: All metadata caches for that entity type are cleared
  - Pattern: `metadata:{EntityType}:*`
  - Example: Creating a book invalidates `metadata:Books:*` (Authors, Genres, Formats, etc.)

- **Stats cache**: The global stats cache is cleared
  - Key: `stats`

### Why Pattern-Based Invalidation?

Rather than tracking which specific metadata properties are affected by a change, we invalidate all metadata for the entity type. This ensures:
- Simplicity: No need to track property dependencies
- Correctness: No risk of stale data
- Performance: Pattern matching is fast with the concurrent dictionary tracking

## Architecture

### Components

1. **ICacheService**: Interface defining caching operations
   - `GetAsync<T>`: Retrieve cached value
   - `SetAsync<T>`: Store value in cache with optional TTL
   - `RemoveAsync`: Remove specific cache key
   - `RemoveByPatternAsync`: Remove all keys matching pattern (supports wildcards)

2. **MemoryCacheService**: Implementation using `IMemoryCache`
   - Tracks cache keys in a `ConcurrentDictionary` for pattern matching
   - Handles cache eviction callbacks
   - Logs cache operations for monitoring

3. **CacheSettings**: Configuration model for cache behavior

### Service Integration

- **MetadataService**: Checks cache before querying database, stores results after computation
- **StatsService**: Checks cache before calculating stats, stores results after computation
- **EntityService**: Invalidates relevant caches after data mutations

### Dependency Injection

```csharp
// In Program.cs
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<ICacheService, MemoryCacheService>();
```

**Note**: `ICacheService` is registered as a singleton to maintain cache state across requests.

## Logging

The caching implementation includes comprehensive logging:

### Log Levels

- **Debug**: Cache hits and misses (high volume)
- **Information**: Cache set operations and invalidations
- **Trace**: Cache evictions (when TTL expires)

### Example Log Messages

```
[Debug] Cache hit for key: metadata:Books:Authors
[Debug] Cache miss for metadata Movies:Genres, fetching from database
[Information] Cached metadata for Books:Authors with 125 distinct values
[Information] Cached statistics: books=250, movies=180, games=95
[Information] Invalidated caches for entity type: Book
[Information] Removed 3 cache entries matching pattern: metadata:Books:*
```

## Performance Benefits

### Before Caching
- Metadata queries: Full database scan + in-memory processing for each request
- Stats queries: Fetch all books, movies, and games for each request

### After Caching
- First request: Same as before (cache miss)
- Subsequent requests: Instant response from memory (cache hit)
- Cache automatically refreshes after TTL or on data changes

### Expected Improvements
- **Response time**: 90-99% reduction for cache hits (typical: 200ms → 5ms)
- **Database load**: Significantly reduced for metadata and stats endpoints
- **Scalability**: Can handle more concurrent requests with same resources

## Monitoring Cache Effectiveness

To monitor cache performance, filter logs by cache-related messages:

```bash
# Count cache hits vs misses
grep "Cache hit" logs.txt | wc -l
grep "Cache miss" logs.txt | wc -l

# View cache invalidations
grep "Invalidated caches" logs.txt

# Calculate hit rate
hit_rate = cache_hits / (cache_hits + cache_misses)
```

Ideal hit rate depends on:
- Read-to-write ratio (more reads = higher hit rate)
- TTL configuration (longer TTL = higher hit rate)
- Data volatility (more changes = lower hit rate)

## Disabling Caching

To disable caching (e.g., for debugging):

```json
{
  "CacheSettings": {
    "EnableCaching": false
  }
}
```

When disabled:
- No values are stored in cache
- No cache lookups occur
- All requests go directly to database
- No overhead from cache management

## Future Enhancements

### Short Term
- Add cache hit rate metrics to health endpoint
- Implement cache warming on application startup
- Add cache size limits and eviction policies

### Long Term
- Redis integration for distributed caching (multi-instance deployments)
- Per-user cache segmentation for personalized data
- Cache-aside pattern with background refresh
- Intelligent cache warming based on access patterns

## Troubleshooting

### Cache Not Working

1. **Check configuration**: Verify `EnableCaching` is `true`
2. **Check logs**: Look for "Cache miss" messages
3. **Verify TTL**: Ensure cache duration is appropriate
4. **Check DI registration**: Ensure services are registered in `Program.cs`

### Stale Data

If cached data appears stale despite changes:

1. **Check invalidation logic**: Verify `EntityService` invalidates caches
2. **Check TTL**: Cache expires after configured duration
3. **Check cache keys**: Ensure keys match between set and invalidate operations
4. **Review logs**: Look for "Invalidated caches" messages

### High Memory Usage

If cache consumes excessive memory:

1. **Reduce TTL**: Lower cache duration in settings
2. **Disable caching**: Set `EnableCaching` to `false` temporarily
3. **Add size limits**: Implement `MemoryCacheOptions.SizeLimit`
4. **Consider distributed cache**: Move to Redis for better memory management

## Best Practices

1. **TTL Selection**: Balance between performance and data freshness
2. **Monitor Hit Rate**: Track effectiveness and adjust TTL accordingly
3. **Log Analysis**: Regularly review cache logs for patterns
4. **Configuration Management**: Use different settings for dev/staging/prod
5. **Testing**: Test with caching both enabled and disabled
6. **Documentation**: Keep this document updated with changes

## Related Files

- `MediaSet.Api/Services/ICacheService.cs` - Cache service interface
- `MediaSet.Api/Services/MemoryCacheService.cs` - In-memory cache implementation
- `MediaSet.Api/Services/MetadataService.cs` - Metadata caching integration
- `MediaSet.Api/Services/StatsService.cs` - Statistics caching integration
- `MediaSet.Api/Services/EntityService.cs` - Cache invalidation logic
- `MediaSet.Api/Models/CacheSettings.cs` - Configuration model
- `MediaSet.Api/appsettings.json` - Production cache configuration
- `MediaSet.Api/appsettings.Development.json` - Development cache configuration

## References

- [ASP.NET Core Memory Cache](https://learn.microsoft.com/en-us/aspnet/core/performance/caching/memory)
- [Cache-Aside Pattern](https://learn.microsoft.com/en-us/azure/architecture/patterns/cache-aside)
- [Distributed Caching in ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/performance/caching/distributed)
