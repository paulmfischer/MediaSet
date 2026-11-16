using System;
using MediaSet.Api.Features.Plex.Services;
using MediaSet.Api.Infrastructure.Plex;
using NUnit.Framework;

namespace MediaSet.Api.Tests.Features.Plex.Services;

[TestFixture]
public class PlexMusicMatchingTests
{
    private static PlexTrack Track(int views = 0, long? played = null, string? codec = "flac", int? bitrate = 900, long? size = 100) =>
        new("k", 1, 1, views, played == null ? null : DateTimeOffset.FromUnixTimeSeconds(played.Value), codec, bitrate, size);

    [TestCase("mbid:ABC-1", "abc-1")]
    [TestCase(" mbid:abc ", "abc")]
    [TestCase("MBID:abc", "abc")]
    [TestCase("tmdbid:603", null)]
    [TestCase("mbid:", null)]
    [TestCase("mbid:a:b", null)]
    [TestCase("", null)]
    [TestCase(null, null)]
    public void MbidKey_ExternalId_ReturnsLowercasedIdOnlyForMusicBrainz(string? externalId, string? expected) =>
        Assert.That(PlexMusicMatching.MbidKey(externalId), Is.EqualTo(expected));

    [Test]
    public void MbidKey_PlexGuids_ReturnsLowercasedMbid()
    {
        Assert.That(PlexMusicMatching.MbidKey(new PlexGuids(null, null, null, "ABC")), Is.EqualTo("abc"));
        Assert.That(PlexMusicMatching.MbidKey(new PlexGuids("1", null, null)), Is.Null);
    }

    [Test]
    public void Rollup_NoTracks_IsEmpty()
    {
        var r = PlexMusicMatching.Rollup([]);

        Assert.That(r.TracksPresent, Is.EqualTo(0));
        Assert.That(r.PlayCount, Is.EqualTo(0));
        Assert.That(r.LastPlayedAt, Is.Null);
        Assert.That(r.AudioCodec, Is.Null);
        Assert.That(r.Bitrate, Is.Null);
        Assert.That(r.SizeBytes, Is.Null);
    }

    [Test]
    public void Rollup_Tracks_SumsPlaysAndSizeAndTakesLatestPlay()
    {
        var r = PlexMusicMatching.Rollup([Track(2, 100, size: 10), Track(3, 300, size: 20), Track(0, size: null)]);

        Assert.That(r.PlayCount, Is.EqualTo(5));
        Assert.That(r.LastPlayedAt, Is.EqualTo(DateTimeOffset.FromUnixTimeSeconds(300).UtcDateTime));
        Assert.That(r.SizeBytes, Is.EqualTo(30));
        Assert.That(r.TracksPresent, Is.EqualTo(3));
    }

    [Test]
    public void Rollup_MixedCodecs_PicksMostCommonThenBetterRankedOnTie()
    {
        Assert.That(PlexMusicMatching.Rollup([Track(codec: "mp3"), Track(codec: "FLAC"), Track(codec: "flac")]).AudioCodec, Is.EqualTo("flac"));
        Assert.That(PlexMusicMatching.Rollup([Track(codec: "mp3"), Track(codec: "flac")]).AudioCodec, Is.EqualTo("flac"));
    }

    [Test]
    public void Rollup_Bitrates_AveragesOnlyTracksThatReportOne()
    {
        var r = PlexMusicMatching.Rollup([Track(bitrate: 320), Track(bitrate: 321), Track(bitrate: null)]);

        Assert.That(r.Bitrate, Is.EqualTo(321));
    }
}
