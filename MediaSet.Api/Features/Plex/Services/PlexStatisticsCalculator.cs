using MediaSet.Api.Features.Plex.Models;
using MediaSet.Api.Shared.Models;

namespace MediaSet.Api.Features.Plex.Services;

/// <summary>The movie and TV sections computed together (they share the movie list).</summary>
public record PlexVideoStatistics(PlexEntityStatisticsDto Movies, PlexEntityStatisticsDto Tv);

/// <summary>Pure statistics over the stored Plex documents and the movie list. No storage, no HTTP.</summary>
public static class PlexStatisticsCalculator
{
    public const int MostWatchedLimit = 10;
    private const string Unknown = "unknown";

    /// <summary>A document from the PlexItems collection of an entry in the movie list; documents of no listed entry are ignored.</summary>
    public static PlexVideoStatistics Compute(IReadOnlyCollection<Movie> entries, IReadOnlyCollection<PlexItemData> docs)
    {
        var byId = docs.GroupBy(d => d.Id).ToDictionary(g => g.Key, g => g.First());
        var movies = entries.Where(m => !m.IsTvSeries).ToList();
        var tv = entries.Where(m => m.IsTvSeries).ToList();
        return new PlexVideoStatistics(
            ComputeKind("movies", movies, byId, tvKind: false),
            ComputeKind("tv", tv, byId, tvKind: true));
    }

    /// <summary>The music section: the music list against the music documents of the PlexItems collection (documents of no listed entry are ignored).</summary>
    public static PlexMusicStatisticsDto ComputeMusic(IReadOnlyCollection<Music> entries, IReadOnlyCollection<PlexItemData> docs)
    {
        var byId = docs.GroupBy(d => d.Id).ToDictionary(g => g.Key, g => g.First());
        var linked = entries.Where(e => e.Id != null && byId.ContainsKey(e.Id)).Select(e => (Entry: e, Doc: byId[e.Id!])).ToList();
        var onPlex = linked.Where(x => x.Doc.OnPlex).ToList();
        var coverage = new PlexCoverageDto(entries.Count, onPlex.Count, entries.Count - onPlex.Count, linked.Count(x => x.Doc.Broken));

        PlexMusicAlbumDto Album((Music Entry, PlexItemData Doc) x) =>
            new(x.Entry.Id!, x.Entry.Title, x.Entry.Artist, x.Doc.PlayCount ?? 0, x.Doc.LastPlayedAt, x.Doc.DateAdded);

        // Each ranking takes the on-Plex albums that have the key, newest/highest first, ties by title
        List<PlexMusicAlbumDto> Top<TKey>(Func<PlexItemData, bool> has, Func<PlexItemData, TKey> key) =>
        [
            .. onPlex
                .Where(x => has(x.Doc))
                .OrderByDescending(x => key(x.Doc))
                .ThenBy(x => x.Entry.Title, StringComparer.OrdinalIgnoreCase)
                .Take(MostWatchedLimit)
                .Select(Album)
        ];

        var mostPlayed = Top(d => d.PlayCount is > 0, d => d.PlayCount);
        var recentlyPlayed = Top(d => d.LastPlayedAt != null, d => d.LastPlayedAt);
        var recentlyAdded = Top(d => d.DateAdded != null, d => d.DateAdded);

        var sized = onPlex.Where(x => x.Doc.SizeBytes is > 0).Select(x => x.Doc.SizeBytes!.Value).ToList();

        // Only albums with a known expected count take part: an entry with no track count cannot be incomplete
        var counted = onPlex.Where(x => x.Doc.TracksExpected is > 0).Select(x => x.Doc).ToList();
        var completeness = new PlexMusicCompletenessDto(
            counted.Sum(d => d.TracksPresent ?? 0),
            counted.Sum(d => d.TracksExpected ?? 0),
            counted.Count(d => (d.TracksPresent ?? 0) >= d.TracksExpected),
            counted.Count(d => (d.TracksPresent ?? 0) < d.TracksExpected));

        return new PlexMusicStatisticsDto(
            coverage,
            onPlex.Count,
            onPlex.Sum(x => x.Doc.PlayCount ?? 0),
            onPlex.Count(x => (x.Doc.PlayCount ?? 0) == 0),
            mostPlayed,
            recentlyPlayed,
            recentlyAdded,
            new PlexStorageDto(sized.Sum(), sized.Count == 0 ? 0 : sized.Sum() / sized.Count, sized.Count),
            Group(onPlex.Select(x => Clean(x.Doc.AudioCodec)?.ToLowerInvariant())),
            BitrateBuckets(onPlex.Select(x => x.Doc.Bitrate)),
            completeness);
    }

    private static readonly (string Name, int Min)[] _bitrateBands =
    [
        ("Under 128 kbps", 0),
        ("128-191 kbps", 128),
        ("192-255 kbps", 192),
        ("256-319 kbps", 256),
        ("320-999 kbps", 320),
        ("1000+ kbps", 1000)
    ];

    /// <summary>Counts by bitrate band, lowest band first; a missing bitrate is "unknown", listed last. Empty bands are left out.</summary>
    private static IReadOnlyList<PlexCountDto> BitrateBuckets(IEnumerable<int?> bitrates)
    {
        var values = bitrates.ToList();
        var counts = new List<PlexCountDto>();
        for (var i = 0; i < _bitrateBands.Length; i++)
        {
            var (name, min) = _bitrateBands[i];
            var max = i + 1 < _bitrateBands.Length ? _bitrateBands[i + 1].Min : int.MaxValue;
            var count = values.Count(b => b is > 0 && b >= min && b < max);
            if (count > 0)
            {
                counts.Add(new PlexCountDto(name, count));
            }
        }
        var unknown = values.Count(b => b is null or <= 0);
        if (unknown > 0)
        {
            counts.Add(new PlexCountDto(Unknown, unknown));
        }
        return counts;
    }

    private static PlexEntityStatisticsDto ComputeKind(string kind, List<Movie> entries, Dictionary<string, PlexItemData> byId, bool tvKind)
    {
        var linked = entries.Where(e => e.Id != null && byId.ContainsKey(e.Id)).Select(e => (Entry: e, Doc: byId[e.Id!])).ToList();
        var onPlex = linked.Where(x => x.Doc.OnPlex).ToList();
        var coverage = new PlexCoverageDto(entries.Count, onPlex.Count, entries.Count - onPlex.Count, linked.Count(x => x.Doc.Broken));

        // The units the breakdowns count: the on-Plex movies, or the on-Plex episodes of the on-Plex TV entries
        var units = tvKind
            ? onPlex.SelectMany(x => x.Doc.Episodes.Where(e => e.OnPlex).Select(e => new Unit(
                e.Resolution, e.Hdr, e.VideoCodec, e.BestAudioTrack, e.SubtitleLanguages.Count > 0, e.Watched, e.SizeBytes, e.DurationSeconds))).ToList()
            : [.. onPlex.Select(x => new Unit(
                x.Doc.Resolution, x.Doc.Hdr, x.Doc.VideoCodec, x.Doc.BestAudioTrack, x.Doc.SubtitleLanguages.Count > 0, x.Doc.Watched, x.Doc.SizeBytes, x.Doc.DurationSeconds))];

        var sized = units.Where(u => u.SizeBytes is > 0).Select(u => u.SizeBytes!.Value).ToList();
        var timed = units.Where(u => u.DurationSeconds is > 0).Select(u => u.DurationSeconds!.Value).ToList();

        // Movies rank by play count; a show document has no play count of its own, so TV entries rank by watched episodes
        var mostWatched = onPlex
            .Select(x => (x.Entry, Count: tvKind ? WatchedEpisodes(x.Doc) : x.Doc.ViewCount))
            .Where(x => x.Count > 0)
            .OrderByDescending(x => x.Count)
            .ThenBy(x => x.Entry.Title, StringComparer.OrdinalIgnoreCase)
            .Take(MostWatchedLimit)
            .Select(x => new PlexMostWatchedDto(x.Entry.Id!, x.Entry.Title, x.Count))
            .ToList();

        PlexCompletenessDto? completeness = null;
        if (tvKind)
        {
            // Every on-Plex entry is counted once: a missing rollup or an expected count of 0 (Plex reports no episodes) is complete
            var rollups = onPlex.Select(x => x.Doc.Rollup ?? new PlexRollup()).ToList();
            completeness = new PlexCompletenessDto(
                rollups.Sum(r => r.EpisodesPresent),
                rollups.Sum(r => r.EpisodesExpected),
                rollups.Count(r => r.EpisodesPresent >= r.EpisodesExpected),
                rollups.Count(r => r.EpisodesPresent < r.EpisodesExpected));
        }

        return new PlexEntityStatisticsDto(
            kind,
            coverage,
            units.Count,
            Group(units.Select(u => Clean(u.Resolution)), byRank: true),
            Group(units.Select(u => Clean(u.VideoCodec)?.ToLowerInvariant())),
            Split(units.Count(u => u.Hdr), "HDR", units.Count(u => !u.Hdr), "SDR"),
            Split(units.Count(u => u.HasSubtitles), "With subtitles", units.Count(u => !u.HasSubtitles), "Without subtitles"),
            Split(units.Count(u => u.Watched), "Watched", units.Count(u => !u.Watched), "Not watched"),
            Group(units.Select(u => Clean(u.Audio?.Codec)?.ToLowerInvariant())),
            Group(units.Select(u => ChannelLayout(u.Audio?.Channels))),
            new PlexStorageDto(sized.Sum(), sized.Count == 0 ? 0 : sized.Sum() / sized.Count, sized.Count),
            new PlexRuntimeDto(timed.Sum(), timed.Count == 0 ? 0 : timed.Sum() / timed.Count, timed.Count),
            mostWatched,
            completeness);
    }

    private static int WatchedEpisodes(PlexItemData doc) =>
        doc.Rollup?.WatchedCount ?? doc.Episodes.Count(e => e.OnPlex && e.Watched);

    private sealed record Unit(string? Resolution, bool Hdr, string? VideoCodec, PlexAudioTrackData? Audio, bool HasSubtitles, bool Watched, long? SizeBytes, long? DurationSeconds);

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static IReadOnlyList<PlexCountDto> Split(int first, string firstName, int second, string secondName) =>
        [new PlexCountDto(firstName, first), new PlexCountDto(secondName, second)];

    /// <summary>Counts by value (case-insensitive); a missing value is "unknown". Largest first, or best resolution first.</summary>
    private static IReadOnlyList<PlexCountDto> Group(IEnumerable<string?> values, bool byRank = false)
    {
        var groups = values
            .GroupBy(v => v ?? Unknown, StringComparer.OrdinalIgnoreCase)
            .Select(g => new PlexCountDto(g.Key, g.Count()));
        var ordered = byRank
            ? groups.OrderByDescending(c => c.Name == Unknown ? -1 : PlexMatching.ResolutionRank(c.Name)).ThenBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
            : groups.OrderByDescending(c => c.Count).ThenBy(c => c.Name, StringComparer.OrdinalIgnoreCase);
        return [.. ordered];
    }

    private static string? ChannelLayout(int? channels) => channels switch
    {
        null or <= 0 => null,
        1 => "Mono",
        2 => "Stereo",
        6 => "5.1",
        7 => "6.1",
        8 => "7.1",
        var n => $"{n} channels"
    };
}
