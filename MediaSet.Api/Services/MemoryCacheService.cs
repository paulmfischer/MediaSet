using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using MediaSet.Api.Models;

namespace MediaSet.Api.Services;

public class MemoryCacheService : ICacheService
{
    private readonly IMemoryCache memoryCache;
    private readonly CacheSettings cacheSettings;
    private readonly ILogger<MemoryCacheService> logger;
    private readonly ConcurrentDictionary<string, byte> cacheKeys;

    public MemoryCacheService(
        IMemoryCache _memoryCache,
        IOptions<CacheSettings> _cacheSettings,
        ILogger<MemoryCacheService> _logger)
    {
        memoryCache = _memoryCache;
        cacheSettings = _cacheSettings.Value;
        logger = _logger;
        cacheKeys = new ConcurrentDictionary<string, byte>();
    }

    public Task<T?> GetAsync<T>(string key) where T : class
    {
        if (!cacheSettings.EnableCaching)
        {
            return Task.FromResult<T?>(null);
        }

        if (memoryCache.TryGetValue(key, out T? value))
        {
            logger.LogDebug("Cache hit for key: {key}", key);
            return Task.FromResult(value);
        }

        logger.LogDebug("Cache miss for key: {key}", key);
        return Task.FromResult<T?>(null);
    }

    public Task SetAsync<T>(string key, T value, TimeSpan? expiration = null) where T : class
    {
        if (!cacheSettings.EnableCaching)
        {
            return Task.CompletedTask;
        }

        var cacheExpiration = expiration ?? TimeSpan.FromMinutes(cacheSettings.DefaultCacheDurationMinutes);
        
        var cacheEntryOptions = new MemoryCacheEntryOptions()
            .SetAbsoluteExpiration(cacheExpiration)
            .RegisterPostEvictionCallback((evictedKey, evictedValue, reason, state) =>
            {
                // Remove from tracking dictionary when evicted
                if (evictedKey is string keyString)
                {
                    cacheKeys.TryRemove(keyString, out _);
                    logger.LogTrace("Cache entry evicted: {key}, reason: {reason}", keyString, reason);
                }
            });

        memoryCache.Set(key, value, cacheEntryOptions);
        cacheKeys.TryAdd(key, 0);
        
        logger.LogDebug("Cached value for key: {key} with expiration: {expiration}", key, cacheExpiration);
        
        return Task.CompletedTask;
    }

    public Task RemoveAsync(string key)
    {
        memoryCache.Remove(key);
        cacheKeys.TryRemove(key, out _);
        logger.LogInformation("Removed cache entry for key: {key}", key);
        return Task.CompletedTask;
    }

    public Task RemoveByPatternAsync(string pattern)
    {
        // Convert wildcard pattern to regex
        var regexPattern = "^" + Regex.Escape(pattern).Replace("\\*", ".*") + "$";
        var regex = new Regex(regexPattern, RegexOptions.IgnoreCase);

        var keysToRemove = cacheKeys.Keys.Where(key => regex.IsMatch(key)).ToList();

        foreach (var key in keysToRemove)
        {
            memoryCache.Remove(key);
            cacheKeys.TryRemove(key, out _);
        }

        logger.LogInformation("Removed {count} cache entries matching pattern: {pattern}", keysToRemove.Count, pattern);
        
        return Task.CompletedTask;
    }
}
