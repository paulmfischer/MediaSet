namespace MediaSet.Api.Models;

/// <summary>
/// Configuration options for HTTP request/response logging.
/// Allows excluding specific paths from being logged to prevent noise
/// and avoid logging-of-logs scenarios.
/// </summary>
public class HttpLoggingOptions
{
    /// <summary>
    /// Paths to exclude from HTTP logging (exact match).
    /// Example: ["/api/logs", "/health", "/health/ready"]
    /// </summary>
    public List<string> ExcludedPaths { get; set; } = [];

    /// <summary>
    /// Path prefixes to exclude from HTTP logging (starts with match).
    /// Example: ["/api/health", "/swagger", "/.well-known"]
    /// </summary>
    public List<string> ExcludePathStartsWith { get; set; } = [];

    /// <summary>
    /// Determines if a given path should be excluded from HTTP logging.
    /// </summary>
    /// <param name="path">The request path to check.</param>
    /// <returns>True if the path should be excluded; otherwise, false.</returns>
    public bool IsPathExcluded(string path)
    {
        // Check for exact match
        if (ExcludedPaths.Any(excluded => path.Equals(excluded, StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        // Check for prefix match
        if (ExcludePathStartsWith.Any(prefix => path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        return false;
    }
}
