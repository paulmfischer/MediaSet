namespace MediaSet.Api.Models;

public class CacheSettings
{
    /// <summary>
    /// Enables or disables caching globally.
    /// </summary>
    public bool EnableCaching { get; set; } = true;

    /// <summary>
    /// Default cache duration in minutes for items without a specific TTL.
    /// </summary>
    public int DefaultCacheDurationMinutes { get; set; } = 10;

    /// <summary>
    /// Cache duration in minutes for metadata queries.
    /// </summary>
    public int MetadataCacheDurationMinutes { get; set; } = 10;

    /// <summary>
    /// Cache duration in minutes for statistics calculations.
    /// </summary>
    public int StatsCacheDurationMinutes { get; set; } = 10;
}
