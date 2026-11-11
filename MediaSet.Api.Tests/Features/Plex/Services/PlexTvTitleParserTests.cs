using MediaSet.Api.Features.Plex.Models;
using MediaSet.Api.Features.Plex.Services;
using NUnit.Framework;

namespace MediaSet.Api.Tests.Features.Plex.Services;

[TestFixture]
public class PlexTvTitleParserTests
{
    private static readonly PlexScope _unreadable = PlexScope.Unreadable;

    [TestCase("Breaking Bad: Season 2", 2)]
    [TestCase("Breaking Bad Season 2", 2)]
    [TestCase("Breaking Bad - Season 2", 2)]
    [TestCase("Breaking Bad, Season 12", 12)]
    [TestCase("breaking bad: SEASON 2", 2)]
    [TestCase("  Breaking   Bad:   Season   2  ", 2)]
    [TestCase("Season 2", 2)]
    [TestCase("Doctor Who: Series 2", 2)]
    [TestCase("Doctor Who Series 11", 11)]
    [TestCase("The Wire S4", 4)]
    [TestCase("The Wire s4", 4)]
    [TestCase("The Wire: Season Two", 2)]
    [TestCase("The Wire: Season Eighteen", 18)]
    [TestCase("The Wire: Second Season", 2)]
    [TestCase("The Wire: The Complete Second Season", 2)]
    [TestCase("The Wire: 3rd Season", 3)]
    [TestCase("The Wire: The Complete Season 5", 5)]
    [TestCase("The Wire: Season 5 Complete", 5)]
    [TestCase("24: Season 3", 3)]
    [TestCase("24 Season 8", 8)]
    [TestCase("Show Season 2 (2008)", 2)]
    [TestCase("Show Season 2 [Blu-ray]", 2)]
    [TestCase("Show Season 02", 2)]
    [TestCase("Law & Order: Season 3", 3)]
    [TestCase("Starsky and Hutch: Season 1", 1)]
    [TestCase("Show – Season 4", 4)]
    [TestCase("Show: Season 2: The Dark Years", 2)]
    public void ParseScope_SingleSeasonForms_ReturnsThatSeason(string title, int season)
    {
        var scope = PlexTvTitleParser.ParseScope(title);

        Assert.That(scope, Is.EqualTo(PlexScope.Single(season)));
        Assert.That(scope.Kind, Is.EqualTo(PlexScopeKinds.Season));
    }

    [TestCase("Show: Seasons 1-3", 1, 3)]
    [TestCase("Show: Seasons 1 - 3", 1, 3)]
    [TestCase("Show: Seasons 1–3", 1, 3)]
    [TestCase("Show: Seasons 1—3", 1, 3)]
    [TestCase("Show: Season 1-3", 1, 3)]
    [TestCase("Show: Series 1-3", 1, 3)]
    [TestCase("Show: Seasons 1 to 3", 1, 3)]
    [TestCase("Show: Seasons 1 through 3", 1, 3)]
    [TestCase("Show: Seasons 1 THRU 3", 1, 3)]
    [TestCase("Show: Seasons One-Three", 1, 3)]
    [TestCase("Show: S1-S3", 1, 3)]
    [TestCase("Show: S1-3", 1, 3)]
    [TestCase("Show: Seasons 1 & 2", 1, 2)]
    [TestCase("Show: Seasons 1 and 2", 1, 2)]
    [TestCase("Show: Seasons 1, 2", 1, 2)]
    [TestCase("Show: Seasons 1, 2 and 3", 1, 3)]
    [TestCase("Show: Seasons 1, 2, 3", 1, 3)]
    [TestCase("Show: The Complete Seasons 1-5", 1, 5)]
    [TestCase("Show: Seasons 1-10 (2008)", 1, 10)]
    [TestCase("Show: Seasons 12-18", 12, 18)]
    public void ParseScope_RangeForms_ReturnsTheRange(string title, int from, int to)
    {
        var scope = PlexTvTitleParser.ParseScope(title);

        Assert.That(scope, Is.EqualTo(new PlexScope(PlexScopeKinds.Range, from, to)));
    }

    [TestCase("Show: Complete Series")]
    [TestCase("Show - The Complete Series")]
    [TestCase("Show The COMPLETE SERIES")]
    [TestCase("Show: Complete Collection")]
    [TestCase("Show: The Complete Seasons")]
    [TestCase("Show: All Seasons")]
    [TestCase("Show: Complete Series (Blu-ray)")]
    [TestCase("Complete Series")]
    public void ParseScope_CompleteSeriesForms_ReturnsAllSeasons(string title)
    {
        var scope = PlexTvTitleParser.ParseScope(title);

        Assert.That(scope, Is.EqualTo(PlexScope.AllSeasons));
    }

    [TestCase("Breaking Bad")]
    [TestCase("24")]
    [TestCase("Show: The Movie")]
    [TestCase("Seasons")]
    [TestCase("Season")]
    [TestCase("Show: Season")]
    [TestCase("Show: Season Finale")]
    [TestCase("Show: Series")]
    [TestCase("Season of the Witch")]
    [TestCase("Reasons 2")]
    [TestCase("Show: Seasonal 2")]
    [TestCase("Show S2E3")]
    [TestCase("Se7en")]
    [TestCase("")]
    [TestCase("   ")]
    public void ParseScope_NoSeasonText_IsUnreadable(string title)
    {
        Assert.That(PlexTvTitleParser.ParseScope(title).IsReadable, Is.False);
    }

    [Test]
    public void ParseScope_Null_IsUnreadable()
    {
        Assert.That(PlexTvTitleParser.ParseScope(null), Is.EqualTo(_unreadable));
    }

    [TestCase("Show: Seasons 1-3 + Movie")]
    [TestCase("Show: Seasons 1-3 & Movie")]
    [TestCase("Show: Seasons 1-3 and the Movie")]
    [TestCase("Show: Season 2 and Specials")]
    [TestCase("Show: Season 2 with Extras")]
    [TestCase("Show: Season 2 plus Bonus Disc")]
    [TestCase("Movie + Season 2")]
    [TestCase("Show: Season 1 Season 2")]
    [TestCase("Show: Season 1, Season 2")]
    [TestCase("Show: Seasons 1-3, Season 5")]
    [TestCase("Show: Complete Series Season 1")]
    [TestCase("Show: Complete Series (Seasons 1-5)")]
    [TestCase("Show: Seasons 3-1")]
    [TestCase("Show: Seasons 1 & 3")]
    [TestCase("Show: Seasons 1, 3")]
    [TestCase("Show: Seasons 1, 2, 4")]
    [TestCase("Show: Seasons 1-3-5")]
    [TestCase("Show: Seasons 1-3 & 4")]
    [TestCase("Show: Season 0")]
    [TestCase("Show: Season 100")]
    [TestCase("Show: Season 2024")]
    [TestCase("Show: Seasons 0-2")]
    [TestCase("Show: Season Twenty One")]
    public void ParseScope_AmbiguousOrConflicting_IsUnreadable(string title)
    {
        Assert.That(PlexTvTitleParser.ParseScope(title).IsReadable, Is.False);
    }

    [Test]
    public void ParseScope_RealTitleContainingSeriesNumber_IsReadAsThatSeason()
    {
        // Known limit of the heuristic, pinned so a change is deliberate
        Assert.That(PlexTvTitleParser.ParseScope("Series 7: The Contenders"), Is.EqualTo(PlexScope.Single(7)));
    }

    [Test]
    public void ParseScope_RangeOfOneSeason_IsASingleSeason()
    {
        Assert.That(PlexTvTitleParser.ParseScope("Show: Seasons 2-2"), Is.EqualTo(PlexScope.Single(2)));
    }

    [TestCase("Breaking Bad: Season 2", "Breaking Bad")]
    [TestCase("Breaking Bad - Season 2", "Breaking Bad")]
    [TestCase("Breaking Bad Season 2", "Breaking Bad")]
    [TestCase("Breaking Bad, Season 2", "Breaking Bad")]
    [TestCase("  Breaking   Bad: SEASON 2  ", "Breaking Bad")]
    [TestCase("Breaking Bad: Seasons 1-3", "Breaking Bad")]
    [TestCase("Breaking Bad: Seasons 1 & 2", "Breaking Bad")]
    [TestCase("Breaking Bad: The Complete Series", "Breaking Bad")]
    [TestCase("Breaking Bad: The Complete Second Season", "Breaking Bad")]
    [TestCase("Doctor Who: Series 2", "Doctor Who")]
    [TestCase("Doctor Who (2005): Series 2", "Doctor Who")]
    [TestCase("Doctor Who: Series 2 (2006)", "Doctor Who")]
    [TestCase("Show: Season 2 [Blu-ray]", "Show")]
    [TestCase("Show: S2", "Show")]
    [TestCase("24: Season 3", "24")]
    [TestCase("24", "24")]
    [TestCase("Breaking Bad", "Breaking Bad")]
    [TestCase("  Breaking   Bad ", "Breaking Bad")]
    [TestCase("Law & Order: Season 3", "Law & Order")]
    [TestCase("Show: Season 2: The Dark Years", "Show: The Dark Years")]
    [TestCase("Season 2", "")]
    [TestCase("Show: Seasons 1-3 + Movie", "")]
    [TestCase("Show: Season 2 and Specials", "")]
    [TestCase("", "")]
    public void CleanTitle_Title_RemovesSeasonTextAndSeams(string title, string expected)
    {
        Assert.That(PlexTvTitleParser.CleanTitle(title), Is.EqualTo(expected));
    }

    [Test]
    public void CleanTitle_Null_IsEmpty()
    {
        Assert.That(PlexTvTitleParser.CleanTitle(null), Is.EqualTo(string.Empty));
    }
}
