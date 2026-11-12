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
}
