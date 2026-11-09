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
