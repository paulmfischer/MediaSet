using Cronos;

namespace MediaSet.Api.Models;

/// <summary>
/// Configuration for the background image lookup service.
/// </summary>
public class BackgroundImageLookupConfiguration
{
    /// <summary>
    /// Whether the background image lookup service is enabled.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Cron expression for scheduling the background service (e.g., "0 2 * * *" for daily at 2 AM).
    /// Minimum interval is 1 hour.
    /// </summary>
    public string Schedule { get; set; } = "0 2 * * *";

    /// <summary>
    /// Maximum runtime in minutes per scheduled run. Service stops processing when this limit is reached.
    /// </summary>
    public int MaxRuntimeMinutes { get; set; } = 60;

    /// <summary>
    /// Total number of entities to process per run, distributed proportionally across enabled entity types.
    /// </summary>
    public int BatchSize { get; set; } = 25;

    /// <summary>
    /// Maximum number of API requests per minute to external services.
    /// </summary>
    public int RequestsPerMinute { get; set; } = 30;

    /// <summary>
    /// Validates the configuration, ensuring the cron expression is valid and has a minimum 1-hour interval.
    /// </summary>
    /// <param name="errorMessage">Error message if validation fails.</param>
    /// <returns>True if configuration is valid, false otherwise.</returns>
    public bool IsValid(out string? errorMessage)
    {
        errorMessage = null;

        if (string.IsNullOrWhiteSpace(Schedule))
        {
            errorMessage = "Schedule is required";
            return false;
        }

        try
        {
            var cronExpression = CronExpression.Parse(Schedule);
            var now = DateTime.UtcNow;
            var nextRun = cronExpression.GetNextOccurrence(now, TimeZoneInfo.Utc);

            if (nextRun == null)
            {
                errorMessage = $"Invalid cron expression '{Schedule}': no next occurrence found";
                return false;
            }

            var followingRun = cronExpression.GetNextOccurrence(nextRun.Value.AddSeconds(1), TimeZoneInfo.Utc);
            if (followingRun == null)
            {
                errorMessage = $"Invalid cron expression '{Schedule}': no following occurrence found";
                return false;
            }

            var interval = followingRun.Value - nextRun.Value;
            if (interval.TotalHours < 1)
            {
                errorMessage = $"Schedule interval must be at least 1 hour. Current interval: {interval.TotalMinutes:F0} minutes. Schedule: {Schedule}";
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            errorMessage = $"Failed to parse cron expression '{Schedule}': {ex.Message}";
            return false;
        }
    }
}
