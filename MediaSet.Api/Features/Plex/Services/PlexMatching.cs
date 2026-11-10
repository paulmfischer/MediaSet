using System.Globalization;
using MediaSet.Api.Features.Plex.Models;
using MediaSet.Api.Infrastructure.Plex;

namespace MediaSet.Api.Features.Plex.Services;

/// <summary>Pure matching and best-version rules for the Plex sync.</summary>
public static class PlexMatching
{
    /// <summary>
    /// The lookup key of a movie ExternalId (<c>source:value</c>, e.g. tmdbid:603): the canonical source
    /// (tmdb, imdb or tvdb, with or without an "id" suffix) and the lowercased value. Null when unsupported.
    /// </summary>
    public static (string Source, string Value)? ExternalIdKey(string? externalId)
    {
        if (string.IsNullOrEmpty(externalId))
        {
            return null;
        }

        var parts = externalId.Split(':');
        if (parts.Length != 2 || parts[1].Length == 0)
        {
            return null;
        }

        var source = parts[0].ToLowerInvariant() switch
        {
            "tmdb" or "tmdbid" => "tmdb",
            "imdb" or "imdbid" => "imdb",
            "tvdb" or "tvdbid" => "tvdb",
            _ => null
        };
        return source == null ? null : (source, parts[1].ToLowerInvariant());
    }

    /// <summary>The lookup keys of an item's GUIDs, in the same form as <see cref="ExternalIdKey"/>.</summary>
    public static IEnumerable<(string Source, string Value)> GuidKeys(PlexGuids guids)
    {
        if (guids.Tmdb != null)
        { yield return ("tmdb", guids.Tmdb.ToLowerInvariant()); }
        if (guids.Imdb != null)
        { yield return ("imdb", guids.Imdb.ToLowerInvariant()); }
        if (guids.Tvdb != null)
        { yield return ("tvdb", guids.Tvdb.ToLowerInvariant()); }
    }

    /// <summary>Highest resolution, then HDR, then larger file size. Null when the item has no versions.</summary>
    public static PlexVersion? BestVersion(IReadOnlyList<PlexVersion> versions) =>
        versions
            .OrderByDescending(v => ResolutionRank(v.Resolution))
            .ThenByDescending(v => v.Hdr)
            .ThenByDescending(v => v.SizeBytes ?? 0)
            .FirstOrDefault();

    /// <summary>Most channels, then best codec.</summary>
    public static PlexAudioTrack? BestAudio(IReadOnlyList<PlexAudioTrack> tracks) =>
        tracks
            .OrderByDescending(t => t.Channels ?? 0)
            .ThenByDescending(t => CodecRank(t.Codec))
            .FirstOrDefault();

    public static int ResolutionRank(string? resolution)
    {
        if (string.IsNullOrWhiteSpace(resolution))
        {
            return 0;
        }

        var r = resolution.Trim().ToLowerInvariant();
        return r switch
        {
            "4k" => 2160,
            "sd" => 480,
            _ => int.TryParse(r.TrimEnd('p', 'i'), NumberStyles.None, CultureInfo.InvariantCulture, out var n) ? n : 0
        };
    }

    public static int CodecRank(string? codec) => codec?.ToLowerInvariant() switch
    {
        "truehd" => 7,
        "dca-ma" or "dtshd_ma" or "dts-hd ma" => 6,
        "eac3" => 5,
        "dca" or "dts" => 4,
        "ac3" => 3,
        "flac" or "pcm" => 3,
        "aac" => 2,
        "mp3" => 1,
        _ => 0
    };

    /// <summary>Builds the stored document for a movie from a Plex item.</summary>
    public static PlexItemData Build(string movieId, PlexItem item, DateTime syncedAt, string linkSource = PlexLinkSources.Auto)
    {
        var best = BestVersion(item.Versions);
        var audio = best == null ? null : BestAudio(best.AudioTracks);
        var subtitleLanguages = new List<string>();
        foreach (var language in (best?.Subtitles ?? (IReadOnlyList<PlexSubtitle>)[]).Select(x => x.Language))
        {
            if (!string.IsNullOrWhiteSpace(language) && !subtitleLanguages.Contains(language, StringComparer.OrdinalIgnoreCase))
            {
                subtitleLanguages.Add(language);
            }
        }

        return new PlexItemData
        {
            Id = movieId,
            PlexItemId = item.RatingKey,
            OnPlex = true,
            LinkSource = linkSource,
            Library = item.LibraryTitle,
            Resolution = best?.Resolution,
            Hdr = best?.Hdr ?? false,
            VideoCodec = best?.VideoCodec,
            BestAudioTrack = audio == null ? null : new PlexAudioTrackData(audio.Codec, audio.Channels, audio.Language),
            SubtitleLanguages = subtitleLanguages,
            Watched = item.ViewCount > 0,
            LastWatchedAt = item.LastViewedAt?.UtcDateTime,
            ViewCount = item.ViewCount,
            DateAdded = item.AddedAt?.UtcDateTime,
            DeepLink = item.DeepLink,
            DurationSeconds = best?.Duration is { } d ? (long)d.TotalSeconds : null,
            Container = best?.Container,
            FilePath = best?.FilePath,
            SizeBytes = best?.SizeBytes,
            LastSyncedAt = syncedAt
        };
    }

    /// <summary>
    /// Builds the stored document for a TV entry linked to a Plex show: show-level fields (library, date added,
    /// deep link), the Plex seasons the scope covers (selected by index), the episodes and the rollups. The show has no
    /// file of its own, so the movie file fields stay empty. Episodes are the freshly read ones by season index; null
    /// keeps the previous episodes as they were. Episodes the previous document had that Plex no longer returns are kept
    /// with OnPlex false; with countMisses (a full sync) they gain a miss and an automatic link drops them at
    /// <see cref="AutoLinkDropMisses"/>, while a manual link never drops them.
    /// </summary>
    public static PlexItemData BuildShow(
        string entryId,
        PlexItem show,
        IReadOnlyList<PlexSeason> seasons,
        PlexScope scope,
        DateTime syncedAt,
        string linkSource = PlexLinkSources.Auto,
        IReadOnlyDictionary<int, IReadOnlyList<PlexEpisode>>? episodes = null,
        PlexItemData? previous = null,
        bool countMisses = false)
    {
        var inScope = SeasonsInScope(seasons, scope).ToList();
        var data = new PlexItemData
        {
            Episodes = episodes == null
                ? [.. previous?.Episodes ?? []]
                : MergeEpisodes(previous?.Episodes ?? [], scope, episodes, linkSource == PlexLinkSources.Manual, countMisses),
            Id = entryId,
            PlexItemId = show.RatingKey,
            OnPlex = true,
            LinkSource = linkSource,
            Scope = scope,
            Seasons = [.. inScope.Select(s => new PlexSeasonData
            {
                Index = s.Index,
                PlexSeasonId = s.RatingKey,
                EpisodeCount = s.EpisodeCount,
                DateAdded = s.AddedAt?.UtcDateTime,
                DeepLink = s.DeepLink
            })],
            Library = show.LibraryTitle,
            DateAdded = show.AddedAt?.UtcDateTime,
            DeepLink = show.DeepLink,
            LastSyncedAt = syncedAt
        };

        PlexRollups.Apply(data);
        return data;
    }

    /// <summary>
    /// Merges the freshly read episodes into the stored ones. A found episode is rewritten from Plex (OnPlex true,
    /// MissCount 0). A stored episode of a covered season that was not returned (or whose season Plex no longer has) is
    /// kept with OnPlex false; when counting misses it gains one and an automatic link drops it at
    /// <see cref="AutoLinkDropMisses"/>. Stored episodes of seasons the scope no longer covers are dropped.
    /// </summary>
    public static List<PlexEpisodeData> MergeEpisodes(
        IReadOnlyList<PlexEpisodeData> stored,
        PlexScope scope,
        IReadOnlyDictionary<int, IReadOnlyList<PlexEpisode>> fetched,
        bool manual,
        bool countMisses)
    {
        var storedByKey = stored.GroupBy(e => (e.SeasonNumber, e.EpisodeNumber)).ToDictionary(g => g.Key, g => g.First());
        var merged = new Dictionary<(int Season, int Episode), PlexEpisodeData>();
        foreach (var (seasonIndex, episodes) in fetched)
        {
            foreach (var episode in episodes)
            {
                var built = BuildEpisode(seasonIndex, episode);
                storedByKey.TryGetValue((seasonIndex, episode.EpisodeIndex), out var previous);
                if (episode.StreamsReused && previous != null && previous.PlexEpisodeId == episode.RatingKey)
                {
                    // Unchanged in Plex: the streams were not read, keep the stored ones (the rest comes from the listing)
                    CopyStreams(built, previous);
                    built.PlexUpdatedAt = previous.PlexUpdatedAt;
                }
                else if (episode.StreamsReused)
                {
                    // Nothing stored to reuse: no streams, and no PlexUpdatedAt so the next sync reads them
                    built.PlexUpdatedAt = null;
                }
                else if (!episode.StreamsAvailable && previous != null)
                {
                    // The stream lookup failed this time: keep what was known rather than overwrite it with nothing
                    built.Hdr = previous.Hdr;
                    built.VideoCodec ??= previous.VideoCodec;
                    built.BestAudioTrack = previous.BestAudioTrack;
                    built.AudioLanguages = previous.AudioLanguages;
                    built.SubtitleLanguages = previous.SubtitleLanguages;
                    built.PlexUpdatedAt = previous.PlexUpdatedAt;
                }
                merged.TryAdd((seasonIndex, episode.EpisodeIndex), built);
            }
        }

        foreach (var old in stored.Where(e => scope.Covers(e.SeasonNumber)))
        {
            if (merged.ContainsKey((old.SeasonNumber, old.EpisodeNumber)))
            {
                continue;
            }

            var misses = countMisses ? old.MissCount + 1 : old.MissCount;
            if (countMisses && !manual && misses >= AutoLinkDropMisses)
            {
                continue;
            }

            old.OnPlex = false;
            old.MissCount = misses;
            merged[(old.SeasonNumber, old.EpisodeNumber)] = old;
        }

        return [.. merged.Values.OrderBy(e => e.SeasonNumber).ThenBy(e => e.EpisodeNumber)];
    }

    private static void CopyStreams(PlexEpisodeData to, PlexEpisodeData from)
    {
        to.Hdr = from.Hdr;
        to.VideoCodec = from.VideoCodec;
        to.BestAudioTrack = from.BestAudioTrack;
        to.AudioLanguages = from.AudioLanguages;
        to.SubtitleLanguages = from.SubtitleLanguages;
    }

    /// <summary>
    /// The stored episodes a client may skip the stream lookup for (by Plex rating key). Only episodes whose streams were
    /// read (they have a PlexUpdatedAt) and that have a file size qualify. Empty for an entry with nothing stored.
    /// </summary>
    public static Dictionary<string, PlexKnownEpisode> KnownEpisodes(PlexItemData? stored)
    {
        var known = new Dictionary<string, PlexKnownEpisode>(StringComparer.Ordinal);
        foreach (var e in stored?.Episodes ?? [])
        {
            if (e.PlexUpdatedAt is { } updated && e.SizeBytes is { } size && !string.IsNullOrEmpty(e.PlexEpisodeId))
            {
                known[e.PlexEpisodeId] = new PlexKnownEpisode(new DateTimeOffset(DateTime.SpecifyKind(updated, DateTimeKind.Utc)), size);
            }
        }
        return known;
    }

    /// <summary>Consecutive runs an auto-linked item or episode may be missing before it is dropped.</summary>
    public const int AutoLinkDropMisses = 2;

    /// <summary>The stored episode for a Plex episode: its best version's file details and its watched state.</summary>
    public static PlexEpisodeData BuildEpisode(int seasonIndex, PlexEpisode episode)
    {
        var best = BestVersion(episode.Versions);
        var audio = best == null ? null : BestAudio(best.AudioTracks);
        return new PlexEpisodeData
        {
            SeasonNumber = seasonIndex,
            EpisodeNumber = episode.EpisodeIndex,
            Title = episode.Title,
            PlexEpisodeId = episode.RatingKey,
            Watched = episode.ViewCount > 0,
            LastWatchedAt = episode.LastViewedAt?.UtcDateTime,
            Resolution = best?.Resolution,
            Hdr = best?.Hdr ?? false,
            VideoCodec = best?.VideoCodec,
            BestAudioTrack = audio == null ? null : new PlexAudioTrackData(audio.Codec, audio.Channels, audio.Language),
            AudioLanguages = Languages((best?.AudioTracks ?? []).Select(t => t.Language)),
            SubtitleLanguages = Languages((best?.Subtitles ?? []).Select(t => t.Language)),
            DurationSeconds = best?.Duration is { } d ? (long)d.TotalSeconds : null,
            Container = best?.Container,
            FilePath = best?.FilePath,
            SizeBytes = best?.SizeBytes,
            // Only when the streams were read, so a failed lookup is retried next sync
            PlexUpdatedAt = episode.StreamsAvailable ? episode.UpdatedAt?.UtcDateTime : null,
            OnPlex = true,
            MissCount = 0
        };
    }

    private static List<string> Languages(IEnumerable<string?> languages) =>
        [.. languages.Where(l => !string.IsNullOrWhiteSpace(l)).Select(l => l!).Distinct(StringComparer.OrdinalIgnoreCase)];

    /// <summary>The seasons the scope covers, picked by season index, in index order. A season Plex lacks is simply absent.</summary>
    public static IEnumerable<PlexSeason> SeasonsInScope(IReadOnlyList<PlexSeason> seasons, PlexScope scope) =>
        seasons.Where(s => scope.Covers(s.Index)).OrderBy(s => s.Index);

    /// <summary>
    /// True when a show can be matched automatically with this scope: "all" always, a season or range when Plex
    /// has at least one season of it. Otherwise the entry stays unmatched rather than linking to nothing.
    /// </summary>
    public static bool ScopeAvailable(IReadOnlyList<PlexSeason> seasons, PlexScope scope) =>
        scope.Kind == PlexScopeKinds.All || SeasonsInScope(seasons, scope).Any();
}
