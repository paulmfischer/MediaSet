using System.Globalization;
using MediaSet.Api.Features.Plex.Models;
using MediaSet.Api.Infrastructure.Plex;

namespace MediaSet.Api.Features.Plex.Services;

/// <summary>Pure matching and best-version rules for the Plex sync.</summary>
public static class PlexMatching
{
    /// <summary>
    /// Whether a movie ExternalId (<c>source:value</c>, e.g. tmdbid:603) appears among the item's GUIDs.
    /// Sources are tmdb, imdb and tvdb, with or without an "id" suffix.
    /// </summary>
    public static bool MatchesExternalId(string? externalId, PlexGuids guids)
    {
        if (string.IsNullOrEmpty(externalId))
        {
            return false;
        }

        var parts = externalId.Split(':');
        if (parts.Length != 2 || parts[1].Length == 0)
        {
            return false;
        }

        var plexValue = parts[0].ToLowerInvariant() switch
        {
            "tmdb" or "tmdbid" => guids.Tmdb,
            "imdb" or "imdbid" => guids.Imdb,
            "tvdb" or "tvdbid" => guids.Tvdb,
            _ => null
        };
        return plexValue != null && string.Equals(plexValue, parts[1], StringComparison.OrdinalIgnoreCase);
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
    public static PlexItemData Build(string movieId, PlexItem item, DateTime syncedAt)
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
}
