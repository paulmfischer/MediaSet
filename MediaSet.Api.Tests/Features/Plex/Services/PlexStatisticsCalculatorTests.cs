using System;
using System.Collections.Generic;
using System.Linq;
using MediaSet.Api.Features.Plex.Models;
using MediaSet.Api.Features.Plex.Services;
using MediaSet.Api.Shared.Models;
using NUnit.Framework;

namespace MediaSet.Api.Tests.Features.Plex.Services;

[TestFixture]
public class PlexStatisticsCalculatorTests
{
    private static Movie Entry(string id, string title, bool tv = false) => new() { Id = id, Title = title, IsTvSeries = tv };

    private static PlexItemData Doc(string id, bool onPlex = true) => new() { Id = id, PlexItemId = "k" + id, OnPlex = onPlex };

    private static int Count(IReadOnlyList<PlexCountDto> list, string name) => list.Single(c => c.Name == name).Count;

    [Test]
    public void Compute_NoDocuments_ReturnsZeroedSectionsWithTotals()
    {
        var stats = PlexStatisticsCalculator.Compute([Entry("a", "A"), Entry("b", "B", tv: true)], []);

        Assert.That(stats.Movies.Coverage, Is.EqualTo(new PlexCoverageDto(1, 0, 1, 0)));
        Assert.That(stats.Tv.Coverage, Is.EqualTo(new PlexCoverageDto(1, 0, 1, 0)));
        Assert.That(stats.Movies.Units, Is.EqualTo(0));
        Assert.That(stats.Movies.Resolution, Is.Empty);
        Assert.That(stats.Movies.Storage.AverageBytes, Is.EqualTo(0));
        Assert.That(stats.Movies.Runtime.AverageSeconds, Is.EqualTo(0));
        Assert.That(stats.Movies.MostWatched, Is.Empty);
        Assert.That(stats.Movies.Completeness, Is.Null);
        Assert.That(stats.Tv.Completeness, Is.EqualTo(new PlexCompletenessDto(0, 0, 0, 0)));
    }

    [Test]
    public void Compute_Movies_CountsEachBreakdownOverOnPlexMoviesOnly()
    {
        var entries = new[] { Entry("a", "A"), Entry("b", "B"), Entry("c", "C"), Entry("d", "D"), Entry("e", "E") };
        var a = Doc("a");
        a.Resolution = "1080";
        a.VideoCodec = "H264";
        a.Hdr = false;
        a.Watched = true;
        a.SubtitleLanguages = ["English"];
        a.BestAudioTrack = new PlexAudioTrackData("ac3", 6, "English");
        a.SizeBytes = 4000;
        a.DurationSeconds = 7200;
        var b = Doc("b");
        b.Resolution = "4k";
        b.VideoCodec = "hevc";
        b.Hdr = true;
        b.BestAudioTrack = new PlexAudioTrackData("eac3", 8, null);
        b.SizeBytes = 8000;
        b.DurationSeconds = 3600;
        var c = Doc("c"); // no stream data at all
        var gone = Doc("d", onPlex: false);
        gone.Resolution = "720";
        gone.SizeBytes = 999_999;
        // e is unlinked

        var stats = PlexStatisticsCalculator.Compute(entries, [a, b, c, gone]).Movies;

        Assert.That(stats.Coverage, Is.EqualTo(new PlexCoverageDto(5, 3, 2, 0)));
        Assert.That(stats.Units, Is.EqualTo(3));
        Assert.That(stats.Resolution.Select(r => r.Name), Is.EqualTo(["4k", "1080", "unknown"]));
        Assert.That(Count(stats.VideoCodec, "h264"), Is.EqualTo(1));
        Assert.That(Count(stats.VideoCodec, "unknown"), Is.EqualTo(1));
        Assert.That(Count(stats.Hdr, "HDR"), Is.EqualTo(1));
        Assert.That(Count(stats.Hdr, "SDR"), Is.EqualTo(2));
        Assert.That(Count(stats.Subtitles, "With subtitles"), Is.EqualTo(1));
        Assert.That(Count(stats.Subtitles, "Without subtitles"), Is.EqualTo(2));
        Assert.That(Count(stats.Watched, "Watched"), Is.EqualTo(1));
        Assert.That(Count(stats.Watched, "Not watched"), Is.EqualTo(2));
        Assert.That(Count(stats.AudioCodec, "ac3"), Is.EqualTo(1));
        Assert.That(Count(stats.AudioChannels, "5.1"), Is.EqualTo(1));
        Assert.That(Count(stats.AudioChannels, "7.1"), Is.EqualTo(1));
        Assert.That(Count(stats.AudioChannels, "unknown"), Is.EqualTo(1));
        // The gone item's size is not counted; the average is over the movies that report one
        Assert.That(stats.Storage, Is.EqualTo(new PlexStorageDto(12000, 6000, 2)));
        Assert.That(stats.Runtime, Is.EqualTo(new PlexRuntimeDto(10800, 5400, 2)));
    }

    [Test]
    public void Compute_BrokenLink_IsCountedAndNotOnPlex()
    {
        var broken = Doc("a", onPlex: false);
        broken.Broken = true;

        var stats = PlexStatisticsCalculator.Compute([Entry("a", "A")], [broken]).Movies;

        Assert.That(stats.Coverage, Is.EqualTo(new PlexCoverageDto(1, 0, 1, 1)));
    }

    [Test]
    public void Compute_DocumentOfNoListedEntry_IsIgnored()
    {
        var orphan = Doc("zzz");
        orphan.Resolution = "1080";

        var stats = PlexStatisticsCalculator.Compute([Entry("a", "A")], [orphan]).Movies;

        Assert.That(stats.Units, Is.EqualTo(0));
        Assert.That(stats.Coverage.OnPlex, Is.EqualTo(0));
    }

    [Test]
    public void Compute_MostWatched_OrdersByViewCountLimitsToTenAndSkipsUnwatched()
    {
        var entries = Enumerable.Range(1, 12).Select(i => Entry("m" + i, "Movie " + i)).Append(Entry("never", "Never")).ToList();
        var docs = Enumerable.Range(1, 12).Select(i => { var d = Doc("m" + i); d.ViewCount = i; return d; }).Append(Doc("never")).ToList();

        var top = PlexStatisticsCalculator.Compute(entries, docs).Movies.MostWatched;

        Assert.That(top, Has.Count.EqualTo(PlexStatisticsCalculator.MostWatchedLimit));
        Assert.That(top[0], Is.EqualTo(new PlexMostWatchedDto("m12", "Movie 12", 12)));
        Assert.That(top.Select(t => t.ViewCount), Is.Ordered.Descending);
        Assert.That(top.Select(t => t.Id), Does.Not.Contain("never"));
    }

    [Test]
    public void Compute_Tv_MostWatchedRanksByWatchedEpisodesNotViewCount()
    {
        List<Movie> entries = [Entry("a", "Alpha", tv: true), Entry("b", "Beta", tv: true), Entry("c", "Gamma", tv: true), Entry("d", "Delta", tv: true)];
        var a = Doc("a");
        a.ViewCount = 99;
        a.Rollup = new PlexRollup { WatchedCount = 2 };
        var b = Doc("b");
        b.Rollup = new PlexRollup { WatchedCount = 5 };
        var c = Doc("c");
        c.Rollup = new PlexRollup { WatchedCount = 2 };
        var d = Doc("d");
        d.ViewCount = 50;
        d.Rollup = new PlexRollup { WatchedCount = 0 };

        var top = PlexStatisticsCalculator.Compute(entries, [a, b, c, d]).Tv.MostWatched;

        Assert.That(top, Is.EqualTo(new List<PlexMostWatchedDto>
        {
            new("b", "Beta", 5),
            new("a", "Alpha", 2),
            new("c", "Gamma", 2)
        }));
    }

    [Test]
    public void Compute_Tv_MostWatchedIsEmptyWhenNoEpisodeIsWatched()
    {
        var show = Doc("a");
        show.Rollup = new PlexRollup { WatchedCount = 0 };
        var other = Doc("b"); // no rollup and no episodes

        var top = PlexStatisticsCalculator.Compute([Entry("a", "Alpha", tv: true), Entry("b", "Beta", tv: true)], [show, other]).Tv.MostWatched;

        Assert.That(top, Is.Empty);
    }

    [Test]
    public void Compute_Tv_MostWatchedWithoutRollupCountsWatchedOnPlexEpisodes()
    {
        var show = Doc("a");
        show.Rollup = null;
        show.Episodes =
        [
            new PlexEpisodeData { OnPlex = true, Watched = true },
            new PlexEpisodeData { OnPlex = true, Watched = true },
            new PlexEpisodeData { OnPlex = true, Watched = false },
            new PlexEpisodeData { OnPlex = false, Watched = true }
        ];

        var top = PlexStatisticsCalculator.Compute([Entry("a", "Alpha", tv: true)], [show]).Tv.MostWatched;

        Assert.That(top, Is.EqualTo(new List<PlexMostWatchedDto> { new("a", "Alpha", 2) }));
    }

    [Test]
    public void Compute_Tv_CountsEpisodesOnPlexAndCompleteness()
    {
        var show = Doc("t1");
        show.Scope = PlexScope.AllSeasons;
        show.Rollup = new PlexRollup { EpisodesPresent = 2, EpisodesExpected = 3 };
        show.Episodes =
        [
            new PlexEpisodeData { OnPlex = true, Resolution = "1080", VideoCodec = "h264", Watched = true, SizeBytes = 100, DurationSeconds = 60, SubtitleLanguages = ["English"] },
            new PlexEpisodeData { OnPlex = true, Resolution = "1080", VideoCodec = "h264", SizeBytes = 300, DurationSeconds = 120 },
            new PlexEpisodeData { OnPlex = false, Resolution = "720", SizeBytes = 10_000 }
        ];
        var complete = Doc("t2");
        complete.Scope = PlexScope.AllSeasons;
        complete.Rollup = new PlexRollup { EpisodesPresent = 4, EpisodesExpected = 4 };
        var noEpisodes = Doc("t3");
        noEpisodes.Scope = PlexScope.AllSeasons;
        noEpisodes.Rollup = new PlexRollup { EpisodesPresent = 0, EpisodesExpected = 0 };
        var entries = new[] { Entry("t1", "Show 1", tv: true), Entry("t2", "Show 2", tv: true), Entry("t3", "Show 3", tv: true), Entry("m", "Movie") };

        var stats = PlexStatisticsCalculator.Compute(entries, [show, complete, noEpisodes]);

        Assert.That(stats.Tv.Coverage, Is.EqualTo(new PlexCoverageDto(3, 3, 0, 0)));
        Assert.That(stats.Tv.Units, Is.EqualTo(2));
        Assert.That(Count(stats.Tv.Resolution, "1080"), Is.EqualTo(2));
        Assert.That(Count(stats.Tv.Subtitles, "With subtitles"), Is.EqualTo(1));
        Assert.That(Count(stats.Tv.Watched, "Watched"), Is.EqualTo(1));
        Assert.That(stats.Tv.Storage, Is.EqualTo(new PlexStorageDto(400, 200, 2)));
        Assert.That(stats.Tv.Completeness, Is.EqualTo(new PlexCompletenessDto(6, 7, 2, 1)));
        // complete + incomplete add up to the on-Plex entries
        Assert.That(stats.Tv.Completeness!.EntriesComplete + stats.Tv.Completeness.EntriesIncomplete, Is.EqualTo(stats.Tv.Coverage.OnPlex));
        Assert.That(stats.Movies.Coverage.Total, Is.EqualTo(1));
    }

    [Test]
    public void Compute_CodecsDifferingOnlyByCase_AreOneGroup()
    {
        var a = Doc("a");
        a.VideoCodec = "HEVC";
        var b = Doc("b");
        b.VideoCodec = " hevc ";

        var stats = PlexStatisticsCalculator.Compute([Entry("a", "A"), Entry("b", "B")], [a, b]).Movies;

        Assert.That(stats.VideoCodec, Is.EqualTo([new PlexCountDto("hevc", 2)]));
    }

    private static Music Album(string id, string title, string artist = "Artist") => new() { Id = id, Title = title, Artist = artist };

    private static PlexItemData MusicDoc(string id, bool onPlex = true) =>
        new() { Id = id, PlexItemId = "k" + id, EntityType = "music", OnPlex = onPlex };

    [Test]
    public void ComputeMusic_NoDocuments_ReturnsZeroedSectionWithTotals()
    {
        var stats = PlexStatisticsCalculator.ComputeMusic([Album("a", "A"), Album("b", "B")], []);

        Assert.That(stats.Coverage, Is.EqualTo(new PlexCoverageDto(2, 0, 2, 0)));
        Assert.That(stats.Albums, Is.EqualTo(0));
        Assert.That(stats.TotalPlays, Is.EqualTo(0));
        Assert.That(stats.NeverPlayed, Is.EqualTo(0));
        Assert.That(stats.MostPlayed, Is.Empty);
        Assert.That(stats.RecentlyPlayed, Is.Empty);
        Assert.That(stats.RecentlyAdded, Is.Empty);
        Assert.That(stats.Storage, Is.EqualTo(new PlexStorageDto(0, 0, 0)));
        Assert.That(stats.AudioCodec, Is.Empty);
        Assert.That(stats.Bitrate, Is.Empty);
        Assert.That(stats.Completeness, Is.EqualTo(new PlexMusicCompletenessDto(0, 0, 0, 0)));
    }

    [Test]
    public void ComputeMusic_Coverage_CountsOnPlexNotOnPlexAndBroken()
    {
        var gone = MusicDoc("b", onPlex: false);
        gone.Broken = true;

        var stats = PlexStatisticsCalculator.ComputeMusic([Album("a", "A"), Album("b", "B"), Album("c", "C")], [MusicDoc("a"), gone]);

        Assert.That(stats.Coverage, Is.EqualTo(new PlexCoverageDto(3, 1, 2, 1)));
        Assert.That(stats.Albums, Is.EqualTo(1));
    }

    [Test]
    public void ComputeMusic_Plays_SumsPlaysAndCountsNeverPlayedOverOnPlexAlbums()
    {
        var a = MusicDoc("a");
        a.PlayCount = 5;
        var b = MusicDoc("b");
        b.PlayCount = 0;
        var c = MusicDoc("c"); // null play count is never played
        var gone = MusicDoc("d", onPlex: false);
        gone.PlayCount = 100;
        var entries = new[] { Album("a", "A"), Album("b", "B"), Album("c", "C"), Album("d", "D") };

        var stats = PlexStatisticsCalculator.ComputeMusic(entries, [a, b, c, gone]);

        Assert.That(stats.TotalPlays, Is.EqualTo(5));
        Assert.That(stats.NeverPlayed, Is.EqualTo(2));
    }

    [Test]
    public void ComputeMusic_MostPlayed_TopTenByPlaysThenTitleSkippingNeverPlayed()
    {
        var entries = new List<Music>();
        var docs = new List<PlexItemData>();
        for (var i = 0; i < 12; i++)
        {
            entries.Add(Album($"id{i}", $"Album {i:00}"));
            var d = MusicDoc($"id{i}");
            d.PlayCount = i < 11 ? i + 1 : 0;
            docs.Add(d);
        }
        // a tie on plays is broken by title
        entries.Add(Album("tie", "Aardvark"));
        var tie = MusicDoc("tie");
        tie.PlayCount = 11;
        docs.Add(tie);

        var stats = PlexStatisticsCalculator.ComputeMusic(entries, docs);

        Assert.That(stats.MostPlayed, Has.Count.EqualTo(10));
        Assert.That(stats.MostPlayed.Select(a => a.Title).Take(2), Is.EqualTo(["Aardvark", "Album 10"]));
        Assert.That(stats.MostPlayed.Select(a => a.PlayCount), Is.Ordered.Descending);
        Assert.That(stats.MostPlayed.Select(a => a.Id), Does.Not.Contain("id11"));
    }

    [Test]
    public void ComputeMusic_RecentlyPlayedAndAdded_OrderNewestFirstAndSkipMissingDates()
    {
        var a = MusicDoc("a");
        a.LastPlayedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        a.DateAdded = new DateTime(2023, 5, 1, 0, 0, 0, DateTimeKind.Utc);
        var b = MusicDoc("b");
        b.LastPlayedAt = new DateTime(2024, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        var c = MusicDoc("c"); // never played, no date added
        var d = MusicDoc("d");
        d.DateAdded = new DateTime(2024, 2, 1, 0, 0, 0, DateTimeKind.Utc);
        var entries = new[] { Album("a", "A", "Art A"), Album("b", "B"), Album("c", "C"), Album("d", "D") };

        var stats = PlexStatisticsCalculator.ComputeMusic(entries, [a, b, c, d]);

        Assert.That(stats.RecentlyPlayed.Select(x => x.Id), Is.EqualTo(["b", "a"]));
        Assert.That(stats.RecentlyAdded.Select(x => x.Id), Is.EqualTo(["d", "a"]));
        Assert.That(stats.RecentlyPlayed[1].Artist, Is.EqualTo("Art A"));
    }

    [Test]
    public void ComputeMusic_Storage_TotalsAndAveragesOverAlbumsWithASize()
    {
        var a = MusicDoc("a");
        a.SizeBytes = 3000;
        var b = MusicDoc("b");
        b.SizeBytes = 1000;
        var c = MusicDoc("c"); // no size
        var gone = MusicDoc("d", onPlex: false);
        gone.SizeBytes = 9999;
        var entries = new[] { Album("a", "A"), Album("b", "B"), Album("c", "C"), Album("d", "D") };

        var stats = PlexStatisticsCalculator.ComputeMusic(entries, [a, b, c, gone]);

        Assert.That(stats.Storage, Is.EqualTo(new PlexStorageDto(4000, 2000, 2)));
    }

    [Test]
    public void ComputeMusic_CodecAndBitrate_GroupsCodecsAndBucketsBitrates()
    {
        var specs = new (string Id, string? Codec, int? Bitrate)[]
        {
            ("a", "FLAC", 900), ("b", " flac ", 1411), ("c", "mp3", 320), ("d", "mp3", 192),
            ("e", "mp3", 128), ("f", null, null), ("g", "aac", 96), ("h", "mp3", 255)
        };
        var entries = specs.Select(x => Album(x.Id, x.Id.ToUpperInvariant())).ToList();
        var docs = specs.Select(x =>
        {
            var d = MusicDoc(x.Id);
            d.AudioCodec = x.Codec;
            d.Bitrate = x.Bitrate;
            return d;
        }).ToList();

        var stats = PlexStatisticsCalculator.ComputeMusic(entries, docs);

        Assert.That(Count(stats.AudioCodec, "mp3"), Is.EqualTo(4));
        Assert.That(Count(stats.AudioCodec, "flac"), Is.EqualTo(2));
        Assert.That(Count(stats.AudioCodec, "unknown"), Is.EqualTo(1));
        Assert.That(stats.Bitrate, Is.EqualTo(
        [
            new PlexCountDto("Under 128 kbps", 1),
            new PlexCountDto("128-191 kbps", 1),
            new PlexCountDto("192-255 kbps", 2),
            new PlexCountDto("320-999 kbps", 2),
            new PlexCountDto("1000+ kbps", 1),
            new PlexCountDto("unknown", 1)
        ]));
    }

    [Test]
    public void ComputeMusic_Completeness_ComparesTracksPresentToExpectedIgnoringUnknownExpected()
    {
        var full = MusicDoc("a");
        full.TracksPresent = 10;
        full.TracksExpected = 10;
        var partial = MusicDoc("b");
        partial.TracksPresent = 6;
        partial.TracksExpected = 12;
        var noExpected = MusicDoc("c"); // the entry has no track count
        noExpected.TracksPresent = 8;
        var gone = MusicDoc("d", onPlex: false);
        gone.TracksPresent = 1;
        gone.TracksExpected = 9;
        var entries = new[] { Album("a", "A"), Album("b", "B"), Album("c", "C"), Album("d", "D") };

        var stats = PlexStatisticsCalculator.ComputeMusic(entries, [full, partial, noExpected, gone]);

        Assert.That(stats.Completeness, Is.EqualTo(new PlexMusicCompletenessDto(16, 22, 1, 1)));
    }

    [Test]
    public void ComputeMusic_DocumentOfNoListedEntry_IsIgnored()
    {
        var orphan = MusicDoc("zzz");
        orphan.PlayCount = 50;

        var stats = PlexStatisticsCalculator.ComputeMusic([Album("a", "A")], [orphan]);

        Assert.That(stats.Albums, Is.EqualTo(0));
        Assert.That(stats.TotalPlays, Is.EqualTo(0));
    }
}
