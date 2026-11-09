using MediaSet.Api.Features.Plex.Models;

namespace MediaSet.Api.Features.Plex.Services;

/// <summary>Pure rollup rules for the episodes of a TV entry. No storage, no HTTP.</summary>
public static class PlexRollups
{
    /// <summary>
    /// Rolls up the episodes that are on Plex (OnPlex true); the others are ignored. Expected is the episode count Plex
    /// reports for the season or seasons the episodes belong to.
    /// </summary>
    public static PlexRollup Compute(IEnumerable<PlexEpisodeData> episodes, int expected)
    {
        var present = episodes.Where(e => e.OnPlex).ToList();

        var resolutions = present
            .Where(e => !string.IsNullOrWhiteSpace(e.Resolution))
            .GroupBy(e => e.Resolution!.Trim().ToLowerInvariant())
            .Select(g => (Resolution: g.First().Resolution!.Trim(), Count: g.Count()))
            .OrderByDescending(x => x.Count)
            .ThenByDescending(x => PlexMatching.ResolutionRank(x.Resolution))
            .ToList();

        var audio = present.Select(e => (IReadOnlyList<string>)e.AudioLanguages).ToList();
        var subtitles = present.Select(e => (IReadOnlyList<string>)e.SubtitleLanguages).ToList();
        var watched = present.Where(e => e.Watched).ToList();

        return new PlexRollup
        {
            EpisodesPresent = present.Count,
            EpisodesExpected = expected,
            WatchedCount = watched.Count,
            Watched = present.Count > 0 && watched.Count == present.Count,
            LastWatchedAt = watched.Where(e => e.LastWatchedAt.HasValue).Select(e => e.LastWatchedAt).Max(),
            Resolution = resolutions.FirstOrDefault().Resolution,
            ResolutionMixed = resolutions.Count > 1,
            Hdr = present.Any(e => e.Hdr),
            AudioLanguages = Union(audio),
            AudioLanguagesDiffer = Differ(audio),
            SubtitleLanguages = Union(subtitles),
            SubtitleLanguagesDiffer = Differ(subtitles)
        };
    }

    /// <summary>
    /// Fills the rollup of each season (against that season's Plex episode count) and of the entry (against the sum),
    /// then the entry's top-level watched, last watched, resolution, HDR and subtitle fields from the entry rollup.
    /// </summary>
    public static void Apply(PlexItemData data)
    {
        foreach (var season in data.Seasons)
        {
            season.Rollup = Compute(data.Episodes.Where(e => e.SeasonNumber == season.Index), season.EpisodeCount);
        }

        var rollup = Compute(data.Episodes, data.Seasons.Sum(s => s.EpisodeCount));
        data.Rollup = rollup;
        data.Watched = rollup.Watched;
        data.LastWatchedAt = rollup.LastWatchedAt;
        data.Resolution = rollup.Resolution;
        data.Hdr = rollup.Hdr;
        data.SubtitleLanguages = [.. rollup.SubtitleLanguages];
    }

    private static List<string> Union(IEnumerable<IReadOnlyList<string>> sets) =>
        [.. sets.SelectMany(s => s)
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(l => l, StringComparer.OrdinalIgnoreCase)];

    private static bool Differ(List<IReadOnlyList<string>> sets)
    {
        if (sets.Count < 2)
        {
            return false;
        }

        HashSet<string> Normalize(IReadOnlyList<string> s) =>
            s.Where(l => !string.IsNullOrWhiteSpace(l)).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var first = Normalize(sets[0]);
        return sets.Skip(1).Any(s => !first.SetEquals(Normalize(s)));
    }
}
