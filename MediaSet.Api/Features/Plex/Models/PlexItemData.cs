using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace MediaSet.Api.Features.Plex.Models;

/// <summary>How a Plex link was made. A document with no stored value reads as auto.</summary>
public static class PlexLinkSources
{
    public const string Auto = "auto";
    public const string Manual = "manual";
}

public record PlexAudioTrackData(string? Codec, int? Channels, string? Language);

/// <summary>
/// Rollup of the episodes of a TV entry or one of its seasons. Computed by <see cref="Services.PlexRollups"/> from the
/// episodes that are on Plex only. Watched is true only when every present episode is watched and there is at least one.
/// </summary>
public class PlexRollup
{
    public int EpisodesPresent { get; set; }

    /// <summary>The episode count Plex reports (summed over the seasons for the entry rollup).</summary>
    public int EpisodesExpected { get; set; }

    public int WatchedCount { get; set; }
    public bool Watched { get; set; }

    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime? LastWatchedAt { get; set; }

    /// <summary>The most common resolution (a tie goes to the higher one).</summary>
    public string? Resolution { get; set; }

    /// <summary>True when the present episodes do not all share one resolution.</summary>
    public bool ResolutionMixed { get; set; }

    /// <summary>True when any present episode has HDR.</summary>
    public bool Hdr { get; set; }

    public List<string> AudioLanguages { get; set; } = [];

    /// <summary>True when the present episodes do not all have the same audio languages.</summary>
    public bool AudioLanguagesDiffer { get; set; }

    public List<string> SubtitleLanguages { get; set; } = [];

    /// <summary>True when the present episodes do not all have the same subtitle languages.</summary>
    public bool SubtitleLanguagesDiffer { get; set; }
}

/// <summary>One stored episode of a linked TV entry, in a season the entry's scope covers.</summary>
public class PlexEpisodeData
{
    public int SeasonNumber { get; set; }
    public int EpisodeNumber { get; set; }
    public string Title { get; set; } = string.Empty;

    /// <summary>The Plex rating key of the episode.</summary>
    public string PlexEpisodeId { get; set; } = string.Empty;

    public bool Watched { get; set; }

    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime? LastWatchedAt { get; set; }

    public string? Resolution { get; set; }
    public bool Hdr { get; set; }
    public string? VideoCodec { get; set; }
    public PlexAudioTrackData? BestAudioTrack { get; set; }
    public List<string> AudioLanguages { get; set; } = [];
    public List<string> SubtitleLanguages { get; set; } = [];

    /// <summary>Duration in seconds.</summary>
    public long? DurationSeconds { get; set; }

    public string? Container { get; set; }

    /// <summary>Server file path. Exposed only by the single-item Plex responses.</summary>
    public string? FilePath { get; set; }

    public long? SizeBytes { get; set; }

    /// <summary>
    /// Plex's updatedAt for the episode when its streams were last read. Null for episodes stored before this was kept
    /// and when the stream lookup failed, so the next sync reads them again.
    /// </summary>
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime? PlexUpdatedAt { get; set; }

    /// <summary>False when Plex no longer returned the episode on the last run (the data is then stale).</summary>
    public bool OnPlex { get; set; } = true;

    /// <summary>Consecutive full syncs that did not find the episode. Reset when it is found.</summary>
    public int MissCount { get; set; }
}

/// <summary>
/// One Plex season of a linked TV entry, as reported by Plex. Only the seasons the entry's scope covers are stored.
/// Its episodes are in <see cref="PlexItemData.Episodes"/>.
/// </summary>
public class PlexSeasonData
{
    /// <summary>The season number (0 is Plex's specials season).</summary>
    public int Index { get; set; }

    /// <summary>The Plex rating key of the season.</summary>
    public string PlexSeasonId { get; set; } = string.Empty;

    /// <summary>Episodes Plex reports for the season (what Plex has, not what MediaSet expects).</summary>
    public int EpisodeCount { get; set; }

    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime? DateAdded { get; set; }

    public string? DeepLink { get; set; }

    /// <summary>Rollup of this season's episodes that are on Plex, against the episode count Plex reports for it.</summary>
    public PlexRollup? Rollup { get; set; }
}

/// <summary>
/// Plex data for one linked movie or TV entry, stored in the PlexItems collection. The document id is the movie id, so
/// there is at most one per movie and the built-in unique _id index is the unique index on the entity id.
/// Kept apart from the movie so editing a movie never touches it.
/// </summary>
public class PlexItemData
{
    /// <summary>The movie id.</summary>
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = string.Empty;

    /// <summary>The Plex rating key of the linked item.</summary>
    public string PlexItemId { get; set; } = string.Empty;

    /// <summary>"auto" (matched by the sync) or "manual" (linked by the user; never re-matched or replaced by the sync).</summary>
    public string LinkSource { get; set; } = PlexLinkSources.Auto;

    /// <summary>False when the linked item was not found in Plex on the last run (the data is then stale).</summary>
    public bool OnPlex { get; set; }

    /// <summary>Consecutive full syncs that found the linked item gone from Plex. Reset by a match.</summary>
    public int MissCount { get; set; }

    /// <summary>A manual link whose item is gone from Plex. Never dropped automatically; reset by a match.</summary>
    public bool Broken { get; set; }

    /// <summary>
    /// Set only for a TV entry: the seasons of the Plex show it covers. <see cref="PlexItemId"/> is then the show's
    /// rating key, and several entries may share it, each with its own scope. Null for a movie.
    /// </summary>
    public PlexScope? Scope { get; set; }

    /// <summary>TV entries only: the Plex seasons the scope covers, with Plex's episode count per season.</summary>
    public List<PlexSeasonData> Seasons { get; set; } = [];

    /// <summary>TV entries only: every stored episode of the covered seasons, including ones Plex no longer returns (OnPlex false).</summary>
    public List<PlexEpisodeData> Episodes { get; set; } = [];

    /// <summary>TV entries only: rollup over all episodes on Plex, against the sum of the seasons' Plex episode counts.</summary>
    public PlexRollup? Rollup { get; set; }

    public string? Library { get; set; }
    public string? Resolution { get; set; }
    public bool Hdr { get; set; }
    public string? VideoCodec { get; set; }
    public PlexAudioTrackData? BestAudioTrack { get; set; }
    public List<string> SubtitleLanguages { get; set; } = [];

    /// <summary>Watched by the owner of the configured Plex token.</summary>
    public bool Watched { get; set; }

    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime? LastWatchedAt { get; set; }

    public int ViewCount { get; set; }

    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime? DateAdded { get; set; }

    public string? DeepLink { get; set; }

    /// <summary>Duration in seconds.</summary>
    public long? DurationSeconds { get; set; }

    public string? Container { get; set; }

    /// <summary>Server file path. Exposed only by the single-movie Plex endpoint.</summary>
    public string? FilePath { get; set; }

    public long? SizeBytes { get; set; }

    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime LastSyncedAt { get; set; }
}

public record PlexItemDataDto(
    string MovieId,
    string PlexItemId,
    bool OnPlex,
    string LinkSource,
    int MissCount,
    bool Broken,
    string? Library,
    string? Resolution,
    bool Hdr,
    string? VideoCodec,
    PlexAudioTrackData? BestAudioTrack,
    IReadOnlyList<string> SubtitleLanguages,
    bool Watched,
    DateTimeOffset? LastWatchedAt,
    int ViewCount,
    DateTimeOffset? DateAdded,
    string? DeepLink,
    long? DurationSeconds,
    string? Container,
    string? FilePath,
    long? SizeBytes,
    DateTimeOffset LastSyncedAt,
    PlexScope? Scope = null,
    IReadOnlyList<PlexSeasonDto>? Seasons = null,
    IReadOnlyList<PlexEpisodeDto>? Episodes = null,
    PlexRollupDto? Rollup = null)
{
    private static DateTimeOffset Utc(DateTime d) => new(DateTime.SpecifyKind(d, DateTimeKind.Utc));

    public static PlexItemDataDto From(PlexItemData d) => new(
        d.Id,
        d.PlexItemId,
        d.OnPlex,
        d.LinkSource,
        d.MissCount,
        d.Broken,
        d.Library,
        d.Resolution,
        d.Hdr,
        d.VideoCodec,
        d.BestAudioTrack,
        d.SubtitleLanguages,
        d.Watched,
        d.LastWatchedAt.HasValue ? Utc(d.LastWatchedAt.Value) : null,
        d.ViewCount,
        d.DateAdded.HasValue ? Utc(d.DateAdded.Value) : null,
        d.DeepLink,
        d.DurationSeconds,
        d.Container,
        d.FilePath,
        d.SizeBytes,
        Utc(d.LastSyncedAt),
        d.Scope,
        d.Scope == null ? null : [.. d.Seasons.OrderBy(x => x.Index).Select(x => new PlexSeasonDto(
            x.Index, x.PlexSeasonId, x.EpisodeCount, x.DateAdded.HasValue ? Utc(x.DateAdded.Value) : null, x.DeepLink, PlexRollupDto.From(x.Rollup)))],
        d.Scope == null ? null : [.. d.Episodes.OrderBy(e => e.SeasonNumber).ThenBy(e => e.EpisodeNumber).Select(PlexEpisodeDto.From)],
        d.Scope == null ? null : PlexRollupDto.From(d.Rollup));
}

public record PlexSeasonDto(int Index, string PlexSeasonId, int EpisodeCount, DateTimeOffset? DateAdded, string? DeepLink, PlexRollupDto? Rollup = null);

/// <summary>The rollup of a TV entry or season as the API returns it.</summary>
public record PlexRollupDto(
    int EpisodesPresent,
    int EpisodesExpected,
    int WatchedCount,
    bool Watched,
    DateTimeOffset? LastWatchedAt,
    string? Resolution,
    bool ResolutionMixed,
    bool Hdr,
    IReadOnlyList<string> AudioLanguages,
    bool AudioLanguagesDiffer,
    IReadOnlyList<string> SubtitleLanguages,
    bool SubtitleLanguagesDiffer)
{
    public static PlexRollupDto? From(PlexRollup? r) => r == null ? null : new(
        r.EpisodesPresent,
        r.EpisodesExpected,
        r.WatchedCount,
        r.Watched,
        r.LastWatchedAt.HasValue ? new DateTimeOffset(DateTime.SpecifyKind(r.LastWatchedAt.Value, DateTimeKind.Utc)) : null,
        r.Resolution,
        r.ResolutionMixed,
        r.Hdr,
        r.AudioLanguages,
        r.AudioLanguagesDiffer,
        r.SubtitleLanguages,
        r.SubtitleLanguagesDiffer);
}

/// <summary>An episode with its file path, as the single-item Plex responses return it.</summary>
public record PlexEpisodeDto(
    int SeasonNumber,
    int EpisodeNumber,
    string Title,
    string PlexEpisodeId,
    bool Watched,
    DateTimeOffset? LastWatchedAt,
    string? Resolution,
    bool Hdr,
    string? VideoCodec,
    PlexAudioTrackData? BestAudioTrack,
    IReadOnlyList<string> AudioLanguages,
    IReadOnlyList<string> SubtitleLanguages,
    long? DurationSeconds,
    string? Container,
    string? FilePath,
    long? SizeBytes,
    bool OnPlex,
    int MissCount)
{
    public static PlexEpisodeDto From(PlexEpisodeData e) => new(
        e.SeasonNumber,
        e.EpisodeNumber,
        e.Title,
        e.PlexEpisodeId,
        e.Watched,
        e.LastWatchedAt.HasValue ? new DateTimeOffset(DateTime.SpecifyKind(e.LastWatchedAt.Value, DateTimeKind.Utc)) : null,
        e.Resolution,
        e.Hdr,
        e.VideoCodec,
        e.BestAudioTrack,
        e.AudioLanguages,
        e.SubtitleLanguages,
        e.DurationSeconds,
        e.Container,
        e.FilePath,
        e.SizeBytes,
        e.OnPlex,
        e.MissCount);
}
