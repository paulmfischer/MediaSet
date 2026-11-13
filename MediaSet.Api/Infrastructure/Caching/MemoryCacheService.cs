using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace MediaSet.Api.Infrastructure.Caching;

public class MemoryCacheService : ICacheService
{
    private readonly IMemoryCache _memoryCache;
    private readonly CacheSettings _cacheSettings;
    private readonly ILogger<MemoryCacheService> _logger;
    private readonly ConcurrentDictionary<string, byte> _cacheKeys;

    public MemoryCacheService(
        IMemoryCache _memoryCache,
        IOptions<CacheSettings> _cacheSettings,
        ILogger<MemoryCacheService> _logger)
    {
        this._memoryCache = _memoryCache;
        this._cacheSettings = _cacheSettings.Value;
        this._logger = _logger;
        _cacheKeys = new ConcurrentDictionary<string, byte>();
    }

    public Task<T?> GetAsync<T>(string key) where T : class
    {
        if (!_cacheSettings.EnableCaching)
        {
            return Task.FromResult<T?>(null);
        }

        if (_memoryCache.TryGetValue(key, out T? value))
        {
            _logger.LogDebug("Cache hit for key: {key}", key);
            return Task.FromResult(value);
        }

        _logger.LogDebug("Cache miss for key: {key}", key);
        return Task.FromResult<T?>(null);
    }

    public Task SetAsync<T>(string key, T value, TimeSpan? expiration = null) where T : class
    {
        if (!_cacheSettings.EnableCaching)
        {
            return Task.CompletedTask;
        }

        var cacheExpiration = expiration ?? TimeSpan.FromMinutes(_cacheSettings.DefaultCacheDurationMinutes);

        var cacheEntryOptions = new MemoryCacheEntryOptions()
            .SetAbsoluteExpiration(cacheExpiration)
            .RegisterPostEvictionCallback((evictedKey, evictedValue, reason, state) =>
            {
                // Remove from tracking dictionary when evicted
                if (evictedKey is string keyString)
                {
                    _cacheKeys.TryRemove(keyString, out _);
                    _logger.LogTrace("Cache entry evicted: {key}, reason: {reason}", keyString, reason);
                }
            });

        _memoryCache.Set(key, value, cacheEntryOptions);
        _cacheKeys.TryAdd(key, 0);

        _logger.LogDebug("Cached value for key: {key} with expiration: {expiration}", key, cacheExpiration);

        return Task.CompletedTask;
    }

    public Task RemoveAsync(string key)
    {
        _memoryCache.Remove(key);
        _cacheKeys.TryRemove(key, out _);
        _logger.LogDebug("Removed cache entry for key: {key}", key);
        return Task.CompletedTask;
    }

    public Task RemoveByPatternAsync(string pattern)
    {
        // Convert wildcard pattern to regex
        var regexPattern = "^" + Regex.Escape(pattern).Replace("\\*", ".*") + "$";
        var regex = new Regex(regexPattern, RegexOptions.IgnoreCase);

        var keysToRemove = _cacheKeys.Keys.Where(key => regex.IsMatch(key)).ToList();

        foreach (var key in keysToRemove)
        {
            _memoryCache.Remove(key);
            _cacheKeys.TryRemove(key, out _);
        }

        _logger.LogDebug("Removed {count} cache entries matching pattern: {pattern}", keysToRemove.Count, pattern);

        return Task.CompletedTask;
    }
}
