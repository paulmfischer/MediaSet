namespace MediaSet.Api.Infrastructure.Plex;

public class PlexConfiguration
{
    public const string DefaultSchedule = "0 3 * * 0";

    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>Plex token. A secret: never log it, return it or put it in a message.</summary>
    public string Token { get; set; } = string.Empty;

    public int Timeout { get; set; } = 30;

    public int PageSize { get; set; } = 100;

    public bool AcceptInvalidCertificate { get; set; }

    /// <summary>Throttling of the TMDB ExternalId backfill that starts each sync. Independent of the image lookup configuration.</summary>
    public PlexBackfillConfiguration Backfill { get; set; } = new();

    public Dictionary<string, PlexEntityConfiguration> Entities { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);
}

public class PlexEntityConfiguration
{
    public bool Enabled { get; set; }

    /// <summary>Cron schedule, default weekly on Sunday at 03:00.</summary>
    public string Schedule { get; set; } = PlexConfiguration.DefaultSchedule;

    /// <summary>Plex library section ids to read. Empty means all video libraries.</summary>
    public List<string> LibrarySectionIds { get; set; } = [];
}

public class PlexBackfillConfiguration
{
    /// <summary>Most TMDB searches per minute; requests are spaced at least 60/RequestsPerMinute seconds apart.</summary>
    public int RequestsPerMinute { get; set; } = 30;

    /// <summary>Most movies searched per sync run. Movies never searched or searched longest ago go first.</summary>
    public int BatchSize { get; set; } = 50;
}
