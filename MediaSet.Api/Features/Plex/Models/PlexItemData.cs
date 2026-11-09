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
/// Plex data for one linked movie, stored in the PlexItems collection. The document id is the movie id, so
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
    DateTimeOffset LastSyncedAt)
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
        Utc(d.LastSyncedAt));
}
