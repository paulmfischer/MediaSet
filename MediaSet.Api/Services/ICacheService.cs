namespace MediaSet.Api.Services;

public interface ICacheService
{
    /// <summary>
    /// Retrieves a value from the cache.
    /// </summary>
    /// <typeparam name="T">The type of the cached value.</typeparam>
    /// <param name="key">The cache key.</param>
    /// <returns>The cached value if found; otherwise, null.</returns>
    Task<T?> GetAsync<T>(string key) where T : class;

    /// <summary>
    /// Stores a value in the cache.
    /// </summary>
    /// <typeparam name="T">The type of the value to cache.</typeparam>
    /// <param name="key">The cache key.</param>
    /// <param name="value">The value to cache.</param>
    /// <param name="expiration">Optional expiration time. If not provided, a default expiration will be used.</param>
    Task SetAsync<T>(string key, T value, TimeSpan? expiration = null) where T : class;

    /// <summary>
    /// Removes a specific key from the cache.
    /// </summary>
    /// <param name="key">The cache key to remove.</param>
    Task RemoveAsync(string key);

    /// <summary>
    /// Removes all cache entries matching the specified pattern.
    /// Pattern supports wildcards (*) for flexible matching.
    /// </summary>
    /// <param name="pattern">The pattern to match cache keys (e.g., "metadata:Books:*").</param>
    Task RemoveByPatternAsync(string pattern);
}
