using Cronos;
using MediaSet.Api.Features.Plex.Models;
using MediaSet.Api.Infrastructure.Plex;

namespace MediaSet.Api.Features.Plex.Services;

public sealed record PlexValidationResult(
    bool IntegrationEnabled,
    IReadOnlyList<string> EnabledEntityTypes,
    IReadOnlyList<string> Messages);

/// <summary>
/// Validates the Plex configuration at startup. Messages never contain the token.
/// A missing base URL or token disables the whole integration; a bad schedule or an
/// unsupported entity key disables only that entity type.
/// </summary>
public static class PlexConfigurationValidator
{
    public static PlexValidationResult Validate(PlexConfiguration config)
    {
        var messages = new List<string>();

        if (string.IsNullOrWhiteSpace(config.BaseUrl))
        {
            messages.Add("Plex BaseUrl is required. The Plex integration is disabled.");
        }
        else if (!Uri.TryCreate(config.BaseUrl, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            messages.Add("Plex BaseUrl must be an absolute http or https URL. The Plex integration is disabled.");
        }

        if (string.IsNullOrWhiteSpace(config.Token))
        {
            messages.Add("Plex Token is required. The Plex integration is disabled.");
        }

        if (messages.Count > 0)
        {
            return new PlexValidationResult(false, [], messages);
        }

        if (config.Timeout <= 0)
        {
            messages.Add("Plex Timeout must be positive. The Plex integration is disabled.");
        }

        if (config.PageSize <= 0)
        {
            messages.Add("Plex PageSize must be positive. The Plex integration is disabled.");
        }

        if (messages.Count > 0)
        {
            return new PlexValidationResult(false, [], messages);
        }

        var enabled = new List<string>();
        foreach (var (key, entity) in config.Entities)
        {
            var entityType = PlexEntityTypes.Normalize(key);
            if (entityType == null)
            {
                messages.Add($"Plex entity type '{key}' is not supported (supported: {string.Join(", ", PlexEntityTypes.Supported)}). It is disabled.");
                continue;
            }

            if (!entity.Enabled)
            {
                continue;
            }

            if (!TryValidateSchedule(entity.Schedule, out var scheduleError))
            {
                messages.Add($"Plex entity type '{entityType}' has an invalid schedule: {scheduleError}. It is disabled.");
                continue;
            }

            if (!enabled.Contains(entityType))
            {
                enabled.Add(entityType);
            }
        }

        // Report in the stable supported order
        var ordered = PlexEntityTypes.Supported.Where(enabled.Contains).ToList();
        return new PlexValidationResult(true, ordered, messages);
    }

    private static bool TryValidateSchedule(string? schedule, out string error)
    {
        if (string.IsNullOrWhiteSpace(schedule))
        {
            error = "Schedule is required";
            return false;
        }

        try
        {
            var cron = CronExpression.Parse(schedule);
            if (cron.GetNextOccurrence(DateTime.UtcNow, TimeZoneInfo.Utc) == null)
            {
                error = $"cron expression '{schedule}' has no next occurrence";
                return false;
            }

            error = string.Empty;
            return true;
        }
        catch (CronFormatException)
        {
            error = $"'{schedule}' is not a valid cron expression";
            return false;
        }
    }
}
