using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using MediaSet.Api.Features.Images.Models;
using MediaSet.Api.Features.Images.Services;
using MediaSet.Api.Infrastructure.Lookup;
using MediaSet.Api.Infrastructure.Lookup.Clients.Igdb;
using MediaSet.Api.Infrastructure.Lookup.Clients.UpcItemDb;
using MediaSet.Api.Infrastructure.Lookup.Models;
using MediaSet.Api.Infrastructure.Lookup.Strategies;
using MediaSet.Api.Infrastructure.Storage;
using MediaSet.Api.Shared.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NUnit.Framework;

namespace MediaSet.Api.Tests.Features.Images.Services;

[TestFixture]
public class ImageFallbackConfidenceTests
{
    private static IgdbGame GameResult(int id, string title, int year, params string[] platforms) => new(
        id, title, null, new DateTimeOffset(year, 1, 1, 0, 0, 0, TimeSpan.Zero).ToUnixTimeSeconds(), null, null,
        platforms.Select((p, index) => new IgdbPlatform(index, p, null)).ToList(), null,
        new IgdbCover(id, $"//images.igdb.com/igdb/image/upload/t_thumb/{id}.jpg"));

    private static (ServiceProvider Services, ImageLookupService Service, Mock<IUpcItemDbClient> Upc,
        Mock<IIgdbClient> Igdb, Mock<IImageService> Images) Setup(List<IgdbGame> results)
    {
        var upc = new Mock<IUpcItemDbClient>();
        var igdb = new Mock<IIgdbClient>();
        igdb.Setup(c => c.SearchGameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(results);
        var services = new ServiceCollection().AddSingleton<ILookupStrategy<GameResponse>>(
            new GameLookupStrategy(upc.Object, igdb.Object, NullLogger<GameLookupStrategy>.Instance)).BuildServiceProvider();
        var images = new Mock<IImageService>();
        images.Setup(c => c.DownloadAndSaveImageAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<CancellationToken>())).ReturnsAsync(new Image { FilePath = "cover.jpg" });
        return (services, new ImageLookupService(new LookupStrategyFactory(services), images.Object,
            NullLogger<ImageLookupService>.Instance), upc, igdb, images);
    }

    // Fixture identity fields are based on the read-only IGDB searches recorded in issue-608-image-evaluation.md.
    [TestCase("Super Mario 64 DS", "DS", "2004", 22301)]
    [TestCase("Red Dead Redemption 2", "PS4", "2018", 25076)]
    [TestCase("The Last of Us", "PS3", "2013", 1009)]
    [TestCase("The Last of Us", "PS4", "2013", 0)]
    public async Task FailedUpc_FallsBackThroughStoredMetadata_WithoutSelectingOtherVersions(
        string title, string platform, string year, int expectedId)
    {
        var setup = Setup([
            GameResult(26192, "The Last of Us Part II", 2020, "PlayStation 4", "PlayStation 5"),
            GameResult(6036, "The Last of Us Remastered", 2014, "PlayStation 4"),
            GameResult(1009, "The Last of Us", 2013, "PlayStation 3"),
            GameResult(22301, "Super Mario 64 DS", 2004, "Wii U", "Nintendo DS"),
            GameResult(103205, "Red Dead Redemption 2: Special Edition", 2018, "PlayStation 4"),
            GameResult(25076, "Red Dead Redemption 2", 2018, "Stadia", "PlayStation 4", "PC", "Xbox One")]);
        using var services = setup.Services;
        var entity = new Game { Id = "1", Barcode = "miss", Title = title, Platform = platform, ReleaseDate = year };
        var result = await setup.Service.LookupAndSaveImageAsync(entity, MediaTypes.Games, CancellationToken.None);
        Assert.That(result.Success, Is.EqualTo(expectedId != 0));
        Assert.That(result.ConfirmedMatch?.ProviderId, Is.EqualTo(expectedId == 0 ? null : expectedId.ToString()));
        if (expectedId != 0)
        {
            Assert.That(result.ConfirmedMatch!.ArtworkScope, Does.Contain("package unverified"));
        }
        setup.Upc.Verify(c => c.GetItemByCodeAsync("miss", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public void CoverlessUnrelatedFirstCandidate_DoesNotHideConfidentArtwork()
    {
        var entity = new Movie { Title = "Dune", ReleaseDate = "2021" };
        var selected = ImageCandidateMatcher.Select(entity, [
            new("Dune", null, "1984"), new("Dune", "https://cover.test/dune.jpg", "2021")]);
        Assert.That(selected.Single().ImageUrl, Does.Contain("dune.jpg"));
    }

    [Test]
    public async Task CoverlessFirstProviderResult_DownloadsConfidentLaterResult()
    {
        var setup = Setup([
            GameResult(434, "Red Dead Redemption", 2010, "PlayStation 3") with { Cover = null },
            GameResult(25076, "Red Dead Redemption 2", 2018, "PlayStation 4")]);
        using var services = setup.Services;
        var result = await setup.Service.LookupAndSaveImageAsync(new Game { Id = "1", Title = "Red Dead Redemption 2",
            Platform = "PS4", ReleaseDate = "2018" }, MediaTypes.Games, CancellationToken.None);
        Assert.That(result.Success, Is.True);
        Assert.That(result.ConfirmedMatch!.ProviderId, Is.EqualTo("25076"));
    }

    [Test]
    public void DuplicateObservationsOfConfirmedIdentity_AllowUsableArtwork()
    {
        var identity = new LookupProvenance("Tmdb", "1", "Dune");
        var selected = ImageCandidateMatcher.Select(new Movie { Title = "Dune", ReleaseDate = "2021" },
            [new("Dune", null, "2021", Provenance: identity),
                new("Dune", "https://cover.test/dune.jpg", "2021", Provenance: identity)]);
        Assert.That(selected.First().ImageUrl, Does.Contain("dune.jpg"));
    }

    [Test]
    public async Task RejectedCandidateDownload_TriesOtherArtworkForSameConfirmedIdentity()
    {
        var first = GameResult(22301, "Super Mario 64 DS", 2004, "Nintendo DS");
        var second = first with { Cover = new IgdbCover(123, "//images.igdb.com/igdb/image/upload/t_thumb/alternate.jpg") };
        var setup = Setup([first, second]);
        using var services = setup.Services;
        setup.Images.Setup(c => c.DownloadAndSaveImageAsync(It.Is<string>(s => s.Contains("22301.jpg")),
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ArgumentException("Rejected artwork"));
        var result = await setup.Service.LookupAndSaveImageAsync(new Game { Id = "1", Title = first.Name,
            Platform = "Nintendo DS" }, MediaTypes.Games, CancellationToken.None);
        Assert.That(result.Success, Is.True);
        Assert.That(result.ImageUrl, Does.Contain("alternate.jpg"));
    }

    [Test]
    public void DifferentIdentitiesWithSameEvidence_RemainAmbiguousEvenIfOneHasArtwork()
    {
        var entity = new Movie { Title = "Dune" };
        var error = Assert.Throws<ProviderLookupException>(() => ImageCandidateMatcher.Select(entity,
            [new("Dune", null, "1984"), new("Dune", "https://cover.test/dune.jpg", "2021")]));
        Assert.That(error!.Outcome, Is.EqualTo(ImageLookupOutcome.AmbiguousMatch));
    }

    [Test]
    public void BookAndAlbumCreatorConflicts_RejectArtwork()
    {
        Assert.That(ImageCandidateMatcher.Select(new Book { Title = "Title", Authors = ["Author A"] },
            [new("Title", "https://cover.test/book.jpg", Creators: ["Author B"])]), Is.Empty);
        Assert.That(ImageCandidateMatcher.Select(new Music { Title = "Album", Artist = "Artist A" },
            [new("Album", "https://cover.test/album.jpg", Creators: ["Artist B"])]), Is.Empty);
    }

    [Test]
    public void BundleAndEditionQualifiers_AreNotRemovedForConfidence()
    {
        Assert.That(ImageCandidateMatcher.Select(new Game { Title = "Red Dead Redemption 2: Special Edition" },
            [new("Red Dead Redemption 2", "https://cover.test/game.jpg")]), Is.Empty);
        Assert.That(ImageCandidateMatcher.Select(new Game { Title = "The Last of Us + Uncharted Bundle" },
            [new("The Last of Us", "https://cover.test/game.jpg")]), Is.Empty);
    }

    [Test]
    public async Task DownloadFailure_PersistsIdentity_AndRetryUsesProviderIdInsteadOfBarcode()
    {
        var candidate = GameResult(25076, "Red Dead Redemption 2", 2018, "PlayStation 4");
        var setup = Setup([candidate]);
        using var services = setup.Services;
        var entity = new Game { Id = "1", Title = candidate.Name, Barcode = "miss", Platform = "PS4", ReleaseDate = "2018" };
        setup.Images.Setup(c => c.DownloadAndSaveImageAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<CancellationToken>())).ThrowsAsync(new HttpRequestException("temporary"));
        var result = await setup.Service.LookupAndSaveImageAsync(entity, MediaTypes.Games, CancellationToken.None);
        entity.ImageLookup = ImageRetryPolicy.Complete(entity, result, new BackgroundImageLookupConfiguration(), DateTime.UtcNow);
        Assert.That(entity.ImageLookup.LastOutcome, Is.EqualTo(ImageLookupOutcome.DownloadFailure));
        Assert.That(entity.ImageLookup.ConfirmedMatch!.ProviderId, Is.EqualTo("25076"));
        setup.Igdb.Setup(c => c.GetGameDetailsAsync(25076, It.IsAny<CancellationToken>())).ReturnsAsync(candidate);
        await setup.Service.LookupAndSaveImageAsync(entity, MediaTypes.Games, CancellationToken.None);
        setup.Upc.Verify(c => c.GetItemByCodeAsync("miss", It.IsAny<CancellationToken>()), Times.Once);
        setup.Igdb.Verify(c => c.GetGameDetailsAsync(25076, It.IsAny<CancellationToken>()), Times.Once);

        entity.Platform = "PS5";
        Assert.That(ImageRetryPolicy.Fingerprint(entity), Is.Not.EqualTo(entity.ImageLookup.ConfirmedMatch.MetadataFingerprint));
        await setup.Service.LookupAndSaveImageAsync(entity, MediaTypes.Games, CancellationToken.None);
        setup.Upc.Verify(c => c.GetItemByCodeAsync("miss", It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Test]
    public async Task InvalidExistingUrl_ContinuesToMetadataArtwork()
    {
        var setup = Setup([GameResult(22301, "Super Mario 64 DS", 2004, "Nintendo DS")]);
        using var services = setup.Services;
        setup.Images.Setup(c => c.DownloadAndSaveImageAsync("https://old.test/cover.jpg", It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<CancellationToken>())).ThrowsAsync(new ArgumentException("Invalid artwork"));
        var result = await setup.Service.LookupAndSaveImageAsync(new Game { Id = "1", Title = "Super Mario 64 DS",
            Platform = "Nintendo DS", ImageUrl = "https://old.test/cover.jpg" }, MediaTypes.Games, CancellationToken.None);
        Assert.That(result.Success, Is.True);
    }

    [Test]
    public void SearchContext_ContainsAvailableIdentityMetadata()
    {
        var context = ImageCandidateMatcher.SearchContext(new Book { Title = "Title", Authors = ["Author"],
            Publisher = "Publisher", PublicationDate = "2001", Subtitle = "Edition" });
        Assert.That(context.Keys, Is.EquivalentTo(new[] { "title", "author", "publisher", "publicationdate", "subtitle" }));
    }
}
