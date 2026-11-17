using MediaSet.Api.Features.Plex.Models;
using MediaSet.Api.Infrastructure.Plex;
using MediaSet.Api.Shared.Models;

namespace MediaSet.Api.Features.Plex.Services;

/// <summary>
/// Pure matching and rollup rules for music. A music entry matches a Plex album only by its MusicBrainz id (never by title or
/// artist, so compilations and various-artists releases cannot be mismatched). The album's tracks, all discs together, are
/// rolled up onto one stored document.
/// </summary>
public static class PlexMusicMatching
{
    private const string MusicBrainzSource = "mbid";

    /// <summary>
    /// The lookup key of a music ExternalId (<c>mbid:&lt;id&gt;</c>): the lowercased MusicBrainz id. Null when the id is empty,
    /// not in that form, or from another source.
    /// </summary>
    public static string? MbidKey(string? externalId)
    {
        if (string.IsNullOrWhiteSpace(externalId))
        {
            return null;
        }

        var parts = externalId.Trim().Split(':');
        return parts.Length == 2
            && string.Equals(parts[0], MusicBrainzSource, StringComparison.OrdinalIgnoreCase)
            && parts[1].Length > 0
            ? parts[1].ToLowerInvariant()
            : null;
    }

    /// <summary>The lookup key of the MusicBrainz id Plex holds for an album, or null when it has none.</summary>
    public static string? MbidKey(PlexGuids guids) =>
        string.IsNullOrWhiteSpace(guids.Mbid) ? null : guids.Mbid.Trim().ToLowerInvariant();

    /// <summary>The totals of an album's tracks.</summary>
    public sealed record AlbumRollup(
        int TracksPresent,
        int PlayCount,
        DateTime? LastPlayedAt,
        string? AudioCodec,
        int? Bitrate,
        long? SizeBytes);

    /// <summary>
    /// Plays are summed, last played is the latest of the tracks, size is the sum of the known file sizes, the codec is the one
    /// most tracks use (a tie goes to the better ranked one, then alphabetical) and the bitrate is the rounded average of the
    /// tracks that report one.
    /// </summary>
    public static AlbumRollup Rollup(IReadOnlyList<PlexTrack> tracks)
    {
        var codec = tracks
            .Where(t => !string.IsNullOrWhiteSpace(t.AudioCodec))
            .GroupBy(t => t.AudioCodec!.Trim().ToLowerInvariant())
            .OrderByDescending(g => g.Count())
            .ThenByDescending(g => PlexMatching.CodecRank(g.Key))
            .ThenBy(g => g.Key, StringComparer.Ordinal)
            .Select(g => g.Key)
            .FirstOrDefault();

        var bitrates = tracks.Where(t => t.Bitrate is > 0).Select(t => t.Bitrate!.Value).ToList();
        var sizes = tracks.Where(t => t.SizeBytes is > 0).Select(t => t.SizeBytes!.Value).ToList();
        // Null when no track was ever played
        DateTimeOffset? lastPlayed = tracks.Where(t => t.LastViewedAt.HasValue).Select(t => t.LastViewedAt).Max();

        return new AlbumRollup(
            tracks.Count,
            tracks.Sum(t => t.ViewCount),
            lastPlayed?.UtcDateTime,
            codec,
            bitrates.Count == 0 ? null : (int)Math.Round(bitrates.Average(), MidpointRounding.AwayFromZero),
            sizes.Count == 0 ? null : sizes.Sum());
    }

    /// <summary>
    /// Builds the stored document of a music entry linked to a Plex album. Expected is the entry's track count, so fewer
    /// tracks on Plex than expected is a partial album that still matches. There is no watched flag, rating or file path.
    /// </summary>
    public static PlexItemData Build(
        string musicId,
        PlexItem album,
        IReadOnlyList<PlexTrack> tracks,
        int? expectedTracks,
        DateTime syncedAt,
        string linkSource = PlexLinkSources.Auto)
    {
        var rollup = Rollup(tracks);
        return new PlexItemData
        {
            Id = musicId,
            PlexItemId = album.RatingKey,
            EntityType = PlexEntityTypes.Music,
            OnPlex = true,
            LinkSource = linkSource,
            Library = album.LibraryTitle,
            DateAdded = album.AddedAt?.UtcDateTime,
            DeepLink = album.DeepLink,
            PlayCount = rollup.PlayCount,
            LastPlayedAt = rollup.LastPlayedAt,
            TracksPresent = rollup.TracksPresent,
            TracksExpected = expectedTracks is > 0 ? expectedTracks : null,
            AudioCodec = rollup.AudioCodec,
            Bitrate = rollup.Bitrate,
            SizeBytes = rollup.SizeBytes,
            PlexUpdatedAt = album.UpdatedAt?.UtcDateTime,
            LastSyncedAt = syncedAt
        };
    }

    /// <summary>How long a recorded unusable cover fetch is trusted before it is tried again (a Plex outage can look like no image).</summary>
    public static readonly TimeSpan CoverRetryAfter = TimeSpan.FromDays(30);

    /// <summary>
    /// The state of a Plex album a cover fetch result belongs to: its id and last update. Null when Plex gave no update time,
    /// since a change could then not be told and the fetch is simply repeated.
    /// </summary>
    public static string? CoverAttemptKey(PlexItem album) =>
        album.UpdatedAt is { } updated ? $"{album.RatingKey}|{updated.ToUnixTimeSeconds()}" : null;

    /// <summary>True when the stored data says this album state was already tried for a cover, recently enough to trust it.</summary>
    public static bool CoverAlreadyTried(PlexItemData? stored, string? key, DateTime now) =>
        key != null && stored is { CoverAttemptKey: { } k, CoverAttemptedAt: { } at } && k == key && now - at < CoverRetryAfter;

    /// <summary>The expected track count of a music entry, or null when it has none.</summary>
    public static int? ExpectedTracks(Music music) => music.Tracks is > 0 ? music.Tracks : null;
}
