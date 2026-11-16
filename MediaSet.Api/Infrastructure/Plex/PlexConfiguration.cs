namespace MediaSet.Api.Infrastructure.Plex;

public class PlexConfiguration
{
    public const string DefaultSchedule = "0 3 * * 0";

    /// <summary>Music syncs one hour after the movie sync by default (Sunday 04:00), so the two do not start together.</summary>
    public const string DefaultMusicSchedule = "0 4 * * 0";

    /// <summary>The schedule an entity type runs on: its configured one, or the default for the type when none is set.</summary>
    public static string ScheduleFor(string? entityType, PlexEntityConfiguration entity) =>
        entity.Schedule ?? (PlexEntityTypes.Normalize(entityType) == PlexEntityTypes.Music ? DefaultMusicSchedule : DefaultSchedule);

    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>Plex token. A secret: never log it, return it or put it in a message.</summary>
    public string Token { get; set; } = string.Empty;

    public int Timeout { get; set; } = 30;

    public int PageSize { get; set; } = 100;

    /// <summary>
    /// Most per-item detail (stream) requests in flight at once during a library read. Values below 1 are treated as 1.
    /// </summary>
    public int DetailConcurrency { get; set; } = 4;

    public bool AcceptInvalidCertificate { get; set; }

    /// <summary>Throttling of the ExternalId backfill (TMDB for movies, MusicBrainz for music) that starts each sync. Independent of the image lookup configuration.</summary>
    public PlexBackfillConfiguration Backfill { get; set; } = new();

    public Dictionary<string, PlexEntityConfiguration> Entities { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);
}

public class PlexEntityConfiguration
{
    public bool Enabled { get; set; }

    /// <summary>
    /// Cron schedule. Null (not configured) means the default of the entity type, see <see cref="PlexConfiguration.ScheduleFor"/>:
    /// Sunday 03:00 for movies and Sunday 04:00 for music.
    /// </summary>
    public string? Schedule { get; set; }

    /// <summary>Plex library section ids to read. Empty means all libraries of the entity type's kind (video or artist).</summary>
    public List<string> LibrarySectionIds { get; set; } = [];
}

public class PlexBackfillConfiguration
{
    /// <summary>Most TMDB searches per minute; requests are spaced at least 60/RequestsPerMinute seconds apart.</summary>
    public int RequestsPerMinute { get; set; } = 30;

    /// <summary>Most movies searched per sync run. Movies never searched or searched longest ago go first.</summary>
    public int BatchSize { get; set; } = 50;
}
