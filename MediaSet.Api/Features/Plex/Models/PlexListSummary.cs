namespace MediaSet.Api.Features.Plex.Models;

/// <summary>The values of the movie list's <c>plex</c> filter.</summary>
public static class PlexListFilters
{
    public const string OnPlex = "onPlex";
    public const string NotOnPlex = "notOnPlex";
    public const string Unwatched = "unwatched";
    public const string Broken = "broken";

    /// <summary>The canonical spelling of a filter value (case-insensitive), or null when it is not one.</summary>
    public static string? Normalize(string? value) =>
        new[] { OnPlex, NotOnPlex, Unwatched, Broken }
            .FirstOrDefault(f => string.Equals(f, value, StringComparison.OrdinalIgnoreCase));

    /// <summary>The canonical spelling of a music list filter value (only onPlex and notOnPlex apply to albums), or null when it is not one.</summary>
    public static string? NormalizeMusic(string? value)
    {
        var normalized = Normalize(value);
        return normalized is OnPlex or NotOnPlex ? normalized : null;
    }
}

/// <summary>
/// The small Plex summary attached to each movie of the paged search. It never carries a file path. A movie that is
/// not linked has no summary. For a TV entry Watched and Resolution come from the entry's rollup.
/// </summary>
public record PlexListSummary(bool OnPlex, string? Resolution, bool Watched, bool Broken)
{
    public static PlexListSummary From(PlexItemData d) => new(d.OnPlex, d.Resolution, d.Watched, d.Broken);
}

/// <summary>
/// The small Plex summary attached to each album of the paged music search. It never carries a file path. An album that
/// is not linked has no summary. There is no watched flag for music.
/// </summary>
public record PlexMusicListSummary(
    bool OnPlex,
    bool Broken,
    int PlayCount,
    DateTimeOffset? LastPlayedAt,
    int? TracksPresent,
    int? TracksExpected,
    string? AudioCodec,
    int? Bitrate)
{
    public static PlexMusicListSummary From(PlexItemData d) => new(
        d.OnPlex,
        d.Broken,
        d.PlayCount ?? 0,
        d.LastPlayedAt.HasValue ? new DateTimeOffset(DateTime.SpecifyKind(d.LastPlayedAt.Value, DateTimeKind.Utc)) : null,
        d.TracksPresent,
        d.TracksExpected,
        d.AudioCodec,
        d.Bitrate);
}

/// <summary>The stored Plex data of a linked album as the single music lookup returns it. It never carries a file path.</summary>
public record PlexMusicDataDto(
    string MusicId,
    string PlexItemId,
    bool OnPlex,
    string LinkSource,
    bool Broken,
    string? Library,
    int PlayCount,
    DateTimeOffset? LastPlayedAt,
    int? TracksPresent,
    int? TracksExpected,
    string? AudioCodec,
    int? Bitrate,
    long? SizeBytes,
    DateTimeOffset? DateAdded,
    string? DeepLink,
    DateTimeOffset LastSyncedAt)
{
    private static DateTimeOffset Utc(DateTime d) => new(DateTime.SpecifyKind(d, DateTimeKind.Utc));

    public static PlexMusicDataDto From(PlexItemData d) => new(
        d.Id,
        d.PlexItemId,
        d.OnPlex,
        d.LinkSource,
        d.Broken,
        d.Library,
        d.PlayCount ?? 0,
        d.LastPlayedAt.HasValue ? Utc(d.LastPlayedAt.Value) : null,
        d.TracksPresent,
        d.TracksExpected,
        d.AudioCodec,
        d.Bitrate,
        d.SizeBytes,
        d.DateAdded.HasValue ? Utc(d.DateAdded.Value) : null,
        d.DeepLink,
        Utc(d.LastSyncedAt));
}
