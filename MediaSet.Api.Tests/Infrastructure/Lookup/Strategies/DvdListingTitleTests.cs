using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediaSet.Api.Features.Images.Services;
using MediaSet.Api.Infrastructure.Lookup.Clients.Tmdb;
using MediaSet.Api.Infrastructure.Lookup.Clients.UpcItemDb;
using MediaSet.Api.Infrastructure.Lookup.Models;
using MediaSet.Api.Infrastructure.Lookup.Strategies;
using MediaSet.Api.Shared.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NUnit.Framework;

namespace MediaSet.Api.Tests.Infrastructure.Lookup.Strategies;

[TestFixture]
public class DvdListingTitleTests
{
    [TestCase("Pre-Owned Futurama, Vol. 5", "Futurama, Vol. 5")]
    [TestCase("Pre-Owned - Gilmore Girls S4 Comp", "Gilmore Girls S4 Comp")]
    [TestCase("Futurama, Vol. 5 - Pre-Owned", "Futurama, Vol. 5")]
    [TestCase("Futurama, Vol. 5 (Pre-Owned)", "Futurama, Vol. 5")]
    [TestCase("Amazon.com: Futurama, Vol. 5", "Futurama, Vol. 5")]
    [TestCase("Walmart - Pre-Owned Gilmore Girls S4 Comp", "Gilmore Girls S4 Comp")]
    public void Clean_ListingConditionOrRetailerWords_AreRemoved(string raw, string expected)
    {
        Assert.That(RetailVideoTitle.Clean(raw), Is.EqualTo(expected));
    }

    [TestCase("Used Cars")]
    [TestCase("New Girl")]
    [TestCase("Target")]
    [TestCase("Pre-Owned")]
    [TestCase("Brand New Day")]
    public void Clean_TitlesThatLookLikeListingWords_AreKept(string title)
    {
        Assert.That(RetailVideoTitle.Clean(title), Is.EqualTo(title));
    }

    [TestCase("Futurama, Vol. 5", "Futurama", null)]
    [TestCase("Futurama, Volume 2", "Futurama", null)]
    [TestCase("Gilmore Girls S4 Comp", "Gilmore Girls", 4)]
    [TestCase("Gilmore Girls S4", "Gilmore Girls", 4)]
    [TestCase("Gilmore Girls - S04 Complete Series", "Gilmore Girls", 4)]
    [TestCase("Gilmore Girls: Season 4 Comp", "Gilmore Girls", 4)]
    [TestCase("Friends: Seasons 1-3", "Friends", null)]
    [TestCase("Friends, Seasons 1 & 2", "Friends", null)]
    public void Parse_DvdListingForms_ResolveSeriesAndSeason(string title, string series, int? season)
    {
        var parsed = VideoTitle.Parse(title);

        Assert.That(parsed.IsSeries, Is.True);
        Assert.That(parsed.SeriesTitle, Is.EqualTo(series));
        Assert.That(parsed.Season, Is.EqualTo(season));
    }

    [TestCase("Kill Bill: Vol. 1")]
    [TestCase("Kill Bill: Volume 2")]
    [TestCase("Super Mario S")]
    public void Parse_FilmWithVolumeOrLetterSuffix_IsNotASeries(string title)
    {
        Assert.That(VideoTitle.Parse(title).IsSeries, Is.False);
    }

    private static (MovieLookupStrategy Strategy, Mock<ITmdbClient> Client) Strategy(string name, int season)
    {
        var client = new Mock<ITmdbClient>();
        client.Setup(c => c.SearchTvAsync(name, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TmdbTvSearchResponse([new(7, name)]));
        client.Setup(c => c.GetTvDetailsAsync(7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TmdbTvResponse(7, name, "2002-01-01", "Plot", "/series.jpg"));
        client.Setup(c => c.GetSeasonDetailsAsync(7, season, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TmdbSeasonResponse(season, "2003-01-01", "/season.jpg", null));
        return (new(new Mock<IUpcItemDbClient>().Object, client.Object, NullLogger<MovieLookupStrategy>.Instance), client);
    }

    private static async Task<IReadOnlyList<ImageCandidate>> Candidates(MovieLookupStrategy strategy, Movie movie)
    {
        var found = await strategy.LookupAsync(IdentifierType.Entity,
            new Dictionary<string, string> { ["title"] = movie.Title }, CancellationToken.None);
        return ImageCandidateMatcher.Select(movie, found.Select(m =>
            new ImageCandidate(m.Title, m.ImageUrl, m.ReleaseDate, Provenance: m.Provenance)));
    }

    [Test]
    public async Task Lookup_FuturamaVolume_UsesSeriesPosterWithoutMovieSearch()
    {
        var (strategy, client) = Strategy("Futurama", 5);
        var movie = new Movie { Id = "1", Title = "Pre-Owned Futurama, Vol. 5" };

        var selected = await Candidates(strategy, movie);

        Assert.That(selected, Has.Count.EqualTo(1));
        Assert.That(selected[0].ImageUrl, Does.EndWith("/series.jpg"));
        Assert.That(selected[0].Provenance?.ProviderId, Is.EqualTo("tv:7"));
        client.Verify(c => c.SearchMovieAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Lookup_GilmoreGirlsS4Comp_UsesSeasonFourPoster()
    {
        var (strategy, _) = Strategy("Gilmore Girls", 4);
        var movie = new Movie { Id = "1", Title = "Gilmore Girls S4 Comp" };

        var selected = await Candidates(strategy, movie);

        Assert.That(selected, Has.Count.EqualTo(1));
        Assert.That(selected[0].ImageUrl, Does.EndWith("/season.jpg"));
        Assert.That(selected[0].Provenance?.ProviderId, Is.EqualTo("tv:7:4"));
    }

    [Test]
    public async Task Lookup_VolumeWhenSeriesNotInTmdb_ReturnsNoCandidates()
    {
        var (strategy, _) = Strategy("Some Other Show", 1);
        var movie = new Movie { Id = "1", Title = "Futurama, Vol. 5" };

        Assert.That(await Candidates(strategy, movie), Is.Empty);
    }
}
