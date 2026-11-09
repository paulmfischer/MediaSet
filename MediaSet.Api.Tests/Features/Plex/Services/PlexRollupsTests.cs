using System;
using System.Linq;
using MediaSet.Api.Features.Plex.Models;
using MediaSet.Api.Features.Plex.Services;
using NUnit.Framework;

namespace MediaSet.Api.Tests.Features.Plex.Services;

[TestFixture]
public class PlexRollupsTests
{
    private static PlexEpisodeData Ep(int season, int number, string? resolution = "1080", bool watched = false, DateTime? lastWatched = null,
        bool hdr = false, bool onPlex = true, string[]? audio = null, string[]? subtitles = null) => new()
        {
            SeasonNumber = season,
            EpisodeNumber = number,
            Resolution = resolution,
            Watched = watched,
            LastWatchedAt = lastWatched,
            Hdr = hdr,
            OnPlex = onPlex,
            AudioLanguages = [.. audio ?? []],
            SubtitleLanguages = [.. subtitles ?? []]
        };

    [Test]
    public void Compute_NoEpisodes_ReturnsEmptyRollupWithExpectedCount()
    {
        var rollup = PlexRollups.Compute([], 5);

        Assert.That(rollup.EpisodesPresent, Is.EqualTo(0));
        Assert.That(rollup.EpisodesExpected, Is.EqualTo(5));
        Assert.That(rollup.WatchedCount, Is.EqualTo(0));
        Assert.That(rollup.Watched, Is.False);
        Assert.That(rollup.LastWatchedAt, Is.Null);
        Assert.That(rollup.Resolution, Is.Null);
        Assert.That(rollup.ResolutionMixed, Is.False);
        Assert.That(rollup.Hdr, Is.False);
        Assert.That(rollup.AudioLanguages, Is.Empty);
        Assert.That(rollup.AudioLanguagesDiffer, Is.False);
        Assert.That(rollup.SubtitleLanguages, Is.Empty);
        Assert.That(rollup.SubtitleLanguagesDiffer, Is.False);
    }

    [Test]
    public void Compute_AllEpisodesWatched_IsWatchedWithMostRecentLastWatched()
    {
        var older = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var newer = new DateTime(2024, 3, 1, 0, 0, 0, DateTimeKind.Utc);

        var rollup = PlexRollups.Compute([Ep(1, 1, watched: true, lastWatched: older), Ep(1, 2, watched: true, lastWatched: newer)], 2);

        Assert.That(rollup.Watched, Is.True);
        Assert.That(rollup.WatchedCount, Is.EqualTo(2));
        Assert.That(rollup.LastWatchedAt, Is.EqualTo(newer));
    }

    [Test]
    public void Compute_SomeEpisodesUnwatched_IsNotWatchedButKeepsCountAndLastWatched()
    {
        var seen = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        var rollup = PlexRollups.Compute([Ep(1, 1, watched: true, lastWatched: seen), Ep(1, 2)], 2);

        Assert.That(rollup.Watched, Is.False);
        Assert.That(rollup.WatchedCount, Is.EqualTo(1));
        Assert.That(rollup.LastWatchedAt, Is.EqualTo(seen));
    }

    [Test]
    public void Compute_MissingEpisodesAreIgnored_WatchedRequiresOnlyPresentOnes()
    {
        var rollup = PlexRollups.Compute([Ep(1, 1, watched: true), Ep(1, 2, onPlex: false, hdr: true, resolution: "4k", audio: ["fr"])], 2);

        Assert.That(rollup.EpisodesPresent, Is.EqualTo(1));
        Assert.That(rollup.Watched, Is.True);
        Assert.That(rollup.Hdr, Is.False);
        Assert.That(rollup.Resolution, Is.EqualTo("1080"));
        Assert.That(rollup.AudioLanguages, Is.Empty);
    }

    [Test]
    public void Compute_OnlyMissingEpisodes_IsNotWatched()
    {
        var rollup = PlexRollups.Compute([Ep(1, 1, watched: true, onPlex: false)], 1);

        Assert.That(rollup.EpisodesPresent, Is.EqualTo(0));
        Assert.That(rollup.Watched, Is.False);
    }

    [Test]
    public void Compute_ResolutionsDiffer_PicksMostCommonAndFlagsMixed()
    {
        var rollup = PlexRollups.Compute([Ep(1, 1, "720"), Ep(1, 2, "1080"), Ep(1, 3, "1080")], 3);

        Assert.That(rollup.Resolution, Is.EqualTo("1080"));
        Assert.That(rollup.ResolutionMixed, Is.True);
    }

    [Test]
    public void Compute_ResolutionTie_PicksHigherResolution()
    {
        var rollup = PlexRollups.Compute([Ep(1, 1, "720"), Ep(1, 2, "4k"), Ep(1, 3, "4k"), Ep(1, 4, "720")], 4);

        Assert.That(rollup.Resolution, Is.EqualTo("4k"));
        Assert.That(rollup.ResolutionMixed, Is.True);
    }

    [Test]
    public void Compute_SameResolutionEverywhere_IsNotMixed()
    {
        var rollup = PlexRollups.Compute([Ep(1, 1, "1080"), Ep(1, 2, "1080")], 2);

        Assert.That(rollup.Resolution, Is.EqualTo("1080"));
        Assert.That(rollup.ResolutionMixed, Is.False);
    }

    [Test]
    public void Compute_AnyEpisodeHdr_IsHdr()
    {
        Assert.That(PlexRollups.Compute([Ep(1, 1), Ep(1, 2, hdr: true)], 2).Hdr, Is.True);
        Assert.That(PlexRollups.Compute([Ep(1, 1), Ep(1, 2)], 2).Hdr, Is.False);
    }

    [Test]
    public void Compute_LanguagesDiffer_ReturnsUnionAndDifferFlags()
    {
        var rollup = PlexRollups.Compute(
        [
            Ep(1, 1, audio: ["en"], subtitles: ["en", "es"]),
            Ep(1, 2, audio: ["en", "fr"], subtitles: ["en", "es"])
        ], 2);

        Assert.That(rollup.AudioLanguages, Is.EqualTo(["en", "fr"]));
        Assert.That(rollup.AudioLanguagesDiffer, Is.True);
        Assert.That(rollup.SubtitleLanguages, Is.EqualTo(["en", "es"]));
        Assert.That(rollup.SubtitleLanguagesDiffer, Is.False);
    }

    [Test]
    public void Compute_OneEpisodeWithoutSubtitles_SubtitlesDiffer()
    {
        var rollup = PlexRollups.Compute([Ep(1, 1, subtitles: ["en"]), Ep(1, 2)], 2);

        Assert.That(rollup.SubtitleLanguages, Is.EqualTo(["en"]));
        Assert.That(rollup.SubtitleLanguagesDiffer, Is.True);
    }

    [Test]
    public void Compute_EpisodesPresentAgainstExpected_ReportsBoth()
    {
        var rollup = PlexRollups.Compute([Ep(1, 1), Ep(1, 2), Ep(1, 3, onPlex: false)], 10);

        Assert.That(rollup.EpisodesPresent, Is.EqualTo(2));
        Assert.That(rollup.EpisodesExpected, Is.EqualTo(10));
    }

    [Test]
    public void Apply_TvEntry_RollsUpEachSeasonAndTheEntryAndFillsTopLevelFields()
    {
        var seen = new DateTime(2024, 2, 1, 0, 0, 0, DateTimeKind.Utc);
        var data = new PlexItemData
        {
            Seasons =
            [
                new PlexSeasonData { Index = 1, EpisodeCount = 2 },
                new PlexSeasonData { Index = 2, EpisodeCount = 3 }
            ],
            Episodes =
            [
                Ep(1, 1, watched: true, lastWatched: seen, subtitles: ["en"]),
                Ep(1, 2, watched: true, lastWatched: seen, hdr: true),
                Ep(2, 1, "720", watched: true, lastWatched: seen)
            ]
        };

        PlexRollups.Apply(data);

        Assert.That(data.Seasons[0].Rollup!.EpisodesPresent, Is.EqualTo(2));
        Assert.That(data.Seasons[0].Rollup!.EpisodesExpected, Is.EqualTo(2));
        Assert.That(data.Seasons[0].Rollup!.Watched, Is.True);
        Assert.That(data.Seasons[1].Rollup!.EpisodesPresent, Is.EqualTo(1));
        Assert.That(data.Seasons[1].Rollup!.EpisodesExpected, Is.EqualTo(3));
        Assert.That(data.Rollup!.EpisodesPresent, Is.EqualTo(3));
        Assert.That(data.Rollup!.EpisodesExpected, Is.EqualTo(5));
        Assert.That(data.Watched, Is.True);
        Assert.That(data.LastWatchedAt, Is.EqualTo(seen));
        Assert.That(data.Resolution, Is.EqualTo("1080"));
        Assert.That(data.Hdr, Is.True);
        Assert.That(data.SubtitleLanguages, Is.EqualTo(["en"]));
    }

    [Test]
    public void Apply_NoEpisodes_LeavesEntryUnwatchedWithEmptyFields()
    {
        var data = new PlexItemData { Seasons = [new PlexSeasonData { Index = 1, EpisodeCount = 4 }] };

        PlexRollups.Apply(data);

        Assert.That(data.Watched, Is.False);
        Assert.That(data.Resolution, Is.Null);
        Assert.That(data.Rollup!.EpisodesExpected, Is.EqualTo(4));
        Assert.That(data.Seasons.Single().Rollup!.EpisodesPresent, Is.EqualTo(0));
    }
}
