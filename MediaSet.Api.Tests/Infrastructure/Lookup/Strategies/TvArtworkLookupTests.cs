using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediaSet.Api.Features.Images.Services;
using MediaSet.Api.Infrastructure.Lookup;
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
public class TvArtworkLookupTests
{
    private Mock<ITmdbClient> client = null!;
    private MovieLookupStrategy strategy = null!;

    [SetUp]
    public void SetUp()
    {
        client = new Mock<ITmdbClient>();
        strategy = new(new Mock<IUpcItemDbClient>().Object, client.Object,
            NullLogger<MovieLookupStrategy>.Instance);
    }

    private void Series(string name, int season, string? poster = "/season.jpg", int id = 1,
        string firstAirDate = "2009-09-17", string seasonAirDate = "2014-01-02",
        List<TmdbEpisode>? episodes = null)
    {
        client.Setup(c => c.SearchTvAsync(It.Is<string>(q => VideoTitle.Normalize(q) == VideoTitle.Normalize(name)),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TmdbTvSearchResponse([new(id, name)]));
        client.Setup(c => c.GetTvDetailsAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TmdbTvResponse(id, name, firstAirDate, "Plot", "/series.jpg"));
        client.Setup(c => c.GetSeasonDetailsAsync(id, season, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TmdbSeasonResponse(season, seasonAirDate, poster, episodes));
    }

    private Task<IReadOnlyList<MovieResponse>> Lookup(string title, bool tv = false) =>
        strategy.LookupAsync(IdentifierType.Entity,
            new Dictionary<string, string> { ["title"] = title, ["isTvSeries"] = tv.ToString().ToLowerInvariant() },
            CancellationToken.None);

    [TestCase("Community: Season 6", "Community", 6)]
    [TestCase("Community: The Complete Sixth Season", "Community", 6)]
    [TestCase("Bob's Burgers: Season 1", "Bob's Burgers", 1)]
    [TestCase("Bob’s Burgers: The Complete Ninth Season", "Bob's Burgers", 9)]
    public async Task LookupAsync_ExplicitSeason_UsesTvResourceAndSeasonPoster(string title, string name, int season)
    {
        Series(name, season);
        var result = await Lookup(title);
        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(result[0].ImageUrl, Does.EndWith("/season.jpg"));
        Assert.That(result[0].Provenance?.ProviderId, Is.EqualTo($"tv:1:{season}"));
        client.Verify(c => c.SearchMovieAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task LookupAsync_MissingSeasonPoster_FallsBackToSeriesPoster()
    {
        Series("Community", 6, null);
        var result = await Lookup("Community: Season 6");
        Assert.That(result.Single().ImageUrl, Does.EndWith("/series.jpg"));
    }

    [Test]
    public async Task LookupAsync_MissingSeason_DoesNotFallBackToSeries()
    {
        Series("Community", 6);
        client.Setup(c => c.GetSeasonDetailsAsync(1, 6, It.IsAny<CancellationToken>()))
            .ReturnsAsync((TmdbSeasonResponse?)null);
        Assert.That(await Lookup("Community: Season 6"), Is.Empty);
    }

    [Test]
    public async Task LookupAsync_CompleteSeries_UsesSeriesPoster()
    {
        Series("Constantine", 1);
        var result = await Lookup("Constantine: The Complete Series");
        Assert.That(result.Single().ImageUrl, Does.EndWith("/series.jpg"));
        client.Verify(c => c.GetSeasonDetailsAsync(It.IsAny<int>(), It.IsAny<int>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestCase("Doctor Who: The Time of the Doctor")]
    [TestCase("Doctor Who - Matt Smith Specials [HD] - S02E002 - The Time of the Doctor [HD]")]
    public async Task LookupAsync_KnownSpecial_VerifiesEpisodeInSeasonZero(string title)
    {
        Series("Doctor Who", 0, "/specials.jpg", episodes: [new(1, "The Time of the Doctor", "2013-12-25")]);
        var result = await Lookup(title);
        Assert.That(result.Single().Provenance?.ProviderId, Is.EqualTo("tv:1:0:1"));
        Assert.That(result.Single().ImageUrl, Does.EndWith("/specials.jpg"));
    }

    [Test]
    public async Task LookupAsync_ColonInSeriesName_SearchesFullSeriesName()
    {
        Series("Star Trek: Discovery", 1);
        var result = await Lookup("Star Trek: Discovery", tv: true);
        Assert.That(result.Single().Provenance?.ProviderId, Is.EqualTo("tv:1"));
        Assert.That(result.Single().ImageUrl, Does.EndWith("/series.jpg"));
    }

    [Test]
    public async Task LookupAsync_DuplicateSpecialNames_RetainsAmbiguousEpisodeIdentities()
    {
        Series("Show", 0, episodes: [new(1, "Special", "2020"), new(2, "Special", "2021")]);
        var results = await Lookup("Show: Special");
        Assert.That(results, Has.Count.EqualTo(2));
        Assert.Throws<ProviderLookupException>(() => ImageCandidateMatcher.Select(new Movie { Title = "Show: Special" },
            results.Select(m => new ImageCandidate(m.Title, m.ImageUrl, m.ReleaseDate, Provenance: m.Provenance))));
    }

    [Test]
    public async Task LookupAsync_UnknownSpecial_RejectsUnverifiedSeriesPoster()
    {
        Series("Doctor Who", 0, episodes: [new(1, "The Snowmen", "2012-12-25")]);
        Assert.That(await Lookup("Doctor Who: Made Up Special"), Is.Empty);
    }

    [Test]
    public async Task LookupAsync_IncorrectMovieTitle_IsRejectedBeforeDetails()
    {
        client.Setup(c => c.SearchMovieAsync("Community", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TmdbSearchResponse(1, [new(2, "Community Service", "2010", "")], 1, 1));
        Assert.That(await Lookup("Community"), Is.Empty);
        client.Verify(c => c.GetMovieDetailsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task LookupAsync_TvFlag_RejectsSameNamedMovie()
    {
        Series("Clerks", 1);
        var result = await Lookup("Clerks", tv: true);
        Assert.That(result.Single().Provenance?.ProviderId, Is.EqualTo("tv:1"));
        client.Verify(c => c.SearchMovieAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task LookupAsync_BareFilmAndSeriesTitle_RemainsAmbiguous()
    {
        Series("Constantine", 1);
        client.Setup(c => c.SearchMovieAsync("Constantine", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TmdbSearchResponse(1, [new(2, "Constantine", "2005", "")], 1, 1));
        client.Setup(c => c.GetMovieDetailsAsync(2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TmdbMovieResponse(2, "Constantine", "", "2005", 120, [], [], 7, "/movie.jpg"));
        var result = await Lookup("Constantine");
        Assert.Throws<ProviderLookupException>(() => ImageCandidateMatcher.Select(new Movie { Title = "Constantine" },
            result.Select(m => new ImageCandidate(m.Title, m.ImageUrl, m.ReleaseDate, Provenance: m.Provenance))));
    }

    [TestCase("2009", true)]
    [TestCase("2014", true)]
    [TestCase("1980", false)]
    public async Task Select_SeasonReleaseDate_AcceptsSeriesOrSeasonYearOnly(string year, bool accepted)
    {
        Series("Community", 5);
        var result = await Lookup("Community: Season 5");
        var matches = ImageCandidateMatcher.Select(new Movie
            { Title = "Community: Season 5", IsTvSeries = true, ReleaseDate = year },
            result.Select(m => new ImageCandidate(m.Title, m.ImageUrl, m.ReleaseDate, Provenance: m.Provenance)));
        Assert.That(matches.Count, Is.EqualTo(accepted ? 1 : 0));
    }

    [Test]
    public async Task LookupAsync_ConfirmedTvIdentity_RefreshesSeasonWithoutMovieLookup()
    {
        Series("Community", 6);
        var result = await strategy.LookupAsync(IdentifierType.Entity, new Dictionary<string, string>
            { ["confirmedProviderId"] = "tv:1:6", ["title"] = "Community: The Complete Sixth Season" },
            CancellationToken.None);
        Assert.That(result.Single().Provenance?.Title, Is.EqualTo("Community: The Complete Sixth Season"));
        Assert.That(result.Single().ImageUrl, Does.EndWith("/season.jpg"));
        client.Verify(c => c.SearchMovieAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
