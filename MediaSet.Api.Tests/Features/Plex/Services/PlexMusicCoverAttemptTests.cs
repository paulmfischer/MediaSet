using System;
using MediaSet.Api.Features.Plex.Models;
using MediaSet.Api.Features.Plex.Services;
using MediaSet.Api.Infrastructure.Plex;
using NUnit.Framework;

namespace MediaSet.Api.Tests.Features.Plex.Services;

[TestFixture]
public class PlexMusicCoverAttemptTests
{
    private static readonly DateTime _now = new(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);

    private static PlexItem Album(DateTimeOffset? updatedAt) =>
        new("7001", "album", "Abbey Road", null, "4", "Music", null, null, new PlexGuids(null, null, null), [], UpdatedAt: updatedAt);

    [Test]
    public void CoverAttemptKey_ChangesWithTheAlbumUpdateAndIsNullWithoutOne()
    {
        var a = PlexMusicMatching.CoverAttemptKey(Album(DateTimeOffset.FromUnixTimeSeconds(100)));
        var b = PlexMusicMatching.CoverAttemptKey(Album(DateTimeOffset.FromUnixTimeSeconds(200)));

        Assert.That(a, Is.Not.EqualTo(b));
        Assert.That(PlexMusicMatching.CoverAttemptKey(Album(null)), Is.Null);
    }

    [Test]
    public void CoverAlreadyTried_OnlyForTheSameKeyWithinTheRetryWindow()
    {
        var stored = new PlexItemData { CoverAttemptKey = "k", CoverAttemptedAt = _now.AddDays(-1) };

        Assert.That(PlexMusicMatching.CoverAlreadyTried(stored, "k", _now), Is.True);
        Assert.That(PlexMusicMatching.CoverAlreadyTried(stored, "other", _now), Is.False);
        Assert.That(PlexMusicMatching.CoverAlreadyTried(stored, null, _now), Is.False);
        Assert.That(PlexMusicMatching.CoverAlreadyTried(null, "k", _now), Is.False);
        Assert.That(PlexMusicMatching.CoverAlreadyTried(new PlexItemData(), "k", _now), Is.False);
        Assert.That(PlexMusicMatching.CoverAlreadyTried(
            new PlexItemData { CoverAttemptKey = "k", CoverAttemptedAt = _now - PlexMusicMatching.CoverRetryAfter - TimeSpan.FromSeconds(1) }, "k", _now), Is.False);
    }
}
