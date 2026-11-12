using MediaSet.Api.Features.Plex.Models;
using MediaSet.Api.Shared.Models;

namespace MediaSet.Api.Features.Plex.Services;

/// <summary>Pure statistics over the stored Plex documents and the movie list. No storage, no HTTP.</summary>
public static class PlexStatisticsCalculator
{
    public const int MostWatchedLimit = 10;
    private const string Unknown = "unknown";

    /// <summary>A document from the PlexItems collection of an entry in the movie list; documents of no listed entry are ignored.</summary>
    public static PlexStatisticsDto Compute(IReadOnlyCollection<Movie> entries, IReadOnlyCollection<PlexItemData> docs)
    {
        var byId = docs.GroupBy(d => d.Id).ToDictionary(g => g.Key, g => g.First());
        var movies = entries.Where(m => !m.IsTvSeries).ToList();
        var tv = entries.Where(m => m.IsTvSeries).ToList();
        return new PlexStatisticsDto(
            ComputeKind("movies", movies, byId, tvKind: false),
            ComputeKind("tv", tv, byId, tvKind: true));
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
