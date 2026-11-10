namespace MediaSet.Api.Infrastructure.Plex;

/// <summary>A Plex library section.</summary>
public record PlexLibrary(string Id, string Title, string Type);

/// <summary>External ids Plex lists for an item. Values are the raw ids (for example "603" or "tt0133093").</summary>
public record PlexGuids(string? Tmdb, string? Imdb, string? Tvdb);

public record PlexAudioTrack(string? Codec, int? Channels, string? Language);

public record PlexSubtitle(string? Language, bool External);

/// <summary>One media version of an item (a Plex item can hold several files in different qualities).</summary>
public record PlexVersion(
    string? Resolution,
    bool Hdr,
    string? VideoCodec,
    IReadOnlyList<PlexAudioTrack> AudioTracks,
    IReadOnlyList<PlexSubtitle> Subtitles,
    TimeSpan? Duration,
    string? Container,
    string? FilePath,
    long? SizeBytes);

public record PlexItem(
    string RatingKey,
    string Type,
    string Title,
    int? Year,
    string LibraryId,
    string LibraryTitle,
    DateTimeOffset? AddedAt,
    string? DeepLink,
    PlexGuids Guids,
    IReadOnlyList<PlexVersion> Versions,
    int ViewCount = 0,
    DateTimeOffset? LastViewedAt = null);

/// <summary>A season of a Plex show. Index 0 is the specials season. EpisodeCount is what Plex reports.</summary>
public record PlexSeason(
    string RatingKey,
    int Index,
    string Title,
    int EpisodeCount,
    DateTimeOffset? AddedAt,
    string? DeepLink);

/// <summary>
/// An episode of a Plex season. The versions are the episode's media files (best one chosen by the caller).
/// StreamsAvailable is false when the per-episode stream lookup failed, so audio, subtitle and HDR data are missing
/// (not absent). SeasonIndex is what Plex reports for the parent season (0 for specials).
/// </summary>
public record PlexEpisode(
    string RatingKey,
    int SeasonIndex,
    int EpisodeIndex,
    string Title,
    int ViewCount,
    DateTimeOffset? LastViewedAt,
    IReadOnlyList<PlexVersion> Versions,
    bool StreamsAvailable = true,
    DateTimeOffset? UpdatedAt = null,
    bool StreamsReused = false);

/// <summary>
/// What a caller already stores about an episode: when Plex last updated it and its file size. An episode whose listing
/// still matches both has unchanged streams, so the per-episode stream lookup is skipped for it.
/// </summary>
public record PlexKnownEpisode(DateTimeOffset UpdatedAt, long SizeBytes)
{
    /// <summary>True when a listed episode (its updatedAt and the file sizes of its listed versions) matches what is stored.</summary>
    public bool Matches(DateTimeOffset? listedUpdatedAt, IReadOnlyList<PlexVersion> listedVersions) =>
        listedUpdatedAt == UpdatedAt && listedVersions.Any(v => v.SizeBytes == SizeBytes);
}
