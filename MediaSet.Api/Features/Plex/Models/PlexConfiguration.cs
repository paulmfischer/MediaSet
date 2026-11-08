namespace MediaSet.Api.Features.Plex.Models;

public class PlexConfiguration
{
    public const string DefaultSchedule = "0 3 * * 0";

    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>Plex token. A secret: never log it, return it or put it in a message.</summary>
    public string Token { get; set; } = string.Empty;

    public int Timeout { get; set; } = 30;

    public int PageSize { get; set; } = 100;

    public bool AcceptInvalidCertificate { get; set; }

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
