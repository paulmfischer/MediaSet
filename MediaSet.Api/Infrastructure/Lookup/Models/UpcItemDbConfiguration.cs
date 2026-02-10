namespace MediaSet.Api.Infrastructure.Lookup.Models;

public class UpcItemDbConfiguration
{
    public string BaseUrl { get; set; } = string.Empty;
    public int Timeout { get; set; } = 10;

    /// <summary>
    /// Maximum number of requests per minute (free plan: 6, paid plans: higher).
    /// Default is 5 to provide buffer below free tier limit.
    /// </summary>
    public int MaxRequestsPerMinute { get; set; } = 5;

    /// <summary>
    /// Maximum number of requests per day (free plan: 100, paid plans: higher).
    /// Default is 90 to provide buffer below free tier limit.
    /// </summary>
    public int MaxRequestsPerDay { get; set; } = 90;

    /// <summary>
    /// Minimum delay between requests in milliseconds.
    /// Default is 1000ms (1 second) to stay well below rate limits.
    /// </summary>
    public int MinDelayBetweenRequestsMs { get; set; } = 1000;

    /// <summary>
    /// Maximum time to pause in seconds when waiting for burst limit to reset.
    /// Requests requiring longer pauses will fail immediately.
    /// </summary>
    public int MaxRetryPauseSeconds { get; set; } = 65;

    /// <summary>
    /// Validates the configuration.
    /// </summary>
    public bool IsValid(out string? errorMessage)
    {
        errorMessage = null;

        if (MaxRequestsPerMinute <= 0)
        {
            errorMessage = "MaxRequestsPerMinute must be greater than 0";
            return false;
        }

        if (MaxRequestsPerDay <= 0)
        {
            errorMessage = "MaxRequestsPerDay must be greater than 0";
            return false;
        }

        if (MinDelayBetweenRequestsMs < 0)
        {
            errorMessage = "MinDelayBetweenRequestsMs must be non-negative";
            return false;
        }

        if (MaxRetryPauseSeconds <= 0)
        {
            errorMessage = "MaxRetryPauseSeconds must be greater than 0";
            return false;
        }

        return true;
    }
}
