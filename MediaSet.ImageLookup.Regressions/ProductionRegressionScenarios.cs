using MediaSet.Api.Features.Images.Models;
using MediaSet.Api.Shared.Models;
using NUnit.Framework;

namespace MediaSet.ImageLookup.Regressions;

public sealed partial class RegressionSuite
{
    // Stable public names are historical report IDs; see PRODUCTION_EVIDENCE.md.
    [Test]
    public Task Production912_BookQuota_DefersFreshAndIneligibleBooksAsync() => VerifyBookQuotaAsync(true);

    [Test]
    public async Task Production912_SeasonZero404_RejectsUnverifiedSeriesArtworkAsync()
    {
        using var scenario = new MovieScenario(this);
        scenario.Search("special-title-miss", "tv", $"{SeriesTitle}: Fixture Special", "{\"results\":[]}");
        scenario.TvSearchAndDetails();
        scenario.Add("specials-404", "tv/40/season/0", "{}", status: 404);
        await scenario.InsertAsync(new Movie { Title = $"{SeriesTitle}: Fixture Special", IsTvSeries = true });

        await scenario.ProcessAsync();

        await scenario.AssertTerminalAsync(ImageLookupOutcome.NoMatch);
        scenario.Verify("special-title-miss", "tv-search", "series-detail", "specials-404");
    }

    [Test]
    public async Task Production912_CollectionTitleMiss_NeverSubstitutesIndividualFilmAsync()
    {
        using var scenario = new MovieScenario(this);
        scenario.Search("collection-miss", "collection", "Fixture", "{\"results\":[]}");
        await scenario.InsertAsync(new Movie { Title = "Fixture Trilogy" });

        await scenario.ProcessAsync();

        await scenario.AssertTerminalAsync(ImageLookupOutcome.NoMatch);
        scenario.Verify("collection-miss");
    }

    [Test]
    public Task Production912_GameMatchMiss_PersistsTerminalNoMatchAsync() => VerifyGameMissAsync(false);

    [Test]
    public Task Production912_GameArtworkMiss_PreservesConfirmedIdentityAsync() => VerifyGameMissAsync(true);

    [Test]
    public Task Production912_OversizedMusicArtwork_PersistsInvalidArtworkAsync() => VerifyInvalidMusicArtworkAsync("oversized");

    [Test]
    public Task Production912_MusicBrainz503_RetriesAndRecoversWhenDueAsync() => VerifyMusicRecoveryAsync(false, "503");
}
