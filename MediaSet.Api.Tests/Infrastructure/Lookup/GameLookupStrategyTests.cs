using MediaSet.Api.Features.Lookup.Models;
using MediaSet.Api.Features.Entities.Models;
using MediaSet.Api.Infrastructure.Lookup;
using MediaSet.Api.Features.Entities.Models;
using MediaSet.Api.Infrastructure.Lookup;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MediaSet.Api.Tests.Infrastructure.Lookup;

[TestFixture]
public class GameLookupStrategyTests
{
    private Mock<IUpcItemDbClient> _upcItemDbClientMock = null!;
    private Mock<IGiantBombClient> _giantBombClientMock = null!;
    private Mock<ILogger<GameLookupStrategy>> _loggerMock = null!;
    private GameLookupStrategy _strategy = null!;

    [SetUp]
    public void SetUp()
    {
        _upcItemDbClientMock = new Mock<IUpcItemDbClient>();
        _giantBombClientMock = new Mock<IGiantBombClient>();
        _loggerMock = new Mock<ILogger<GameLookupStrategy>>();
        _strategy = new GameLookupStrategy(
            _upcItemDbClientMock.Object,
            _giantBombClientMock.Object,
            _loggerMock.Object);
    }

    #region CanHandle

    [Test]
    public void CanHandle_WithGamesAndUpc_ReturnsTrue()
    {
        Assert.That(_strategy.CanHandle(MediaTypes.Games, IdentifierType.Upc), Is.True);
    }

    [Test]
    public void CanHandle_WithGamesAndEan_ReturnsTrue()
    {
        Assert.That(_strategy.CanHandle(MediaTypes.Games, IdentifierType.Ean), Is.True);
    }

    [Test]
    public void CanHandle_WithBooksAndUpc_ReturnsFalse()
    {
        Assert.That(_strategy.CanHandle(MediaTypes.Books, IdentifierType.Upc), Is.False);
    }

    [Test]
    public void CanHandle_WithMoviesAndUpc_ReturnsFalse()
    {
        Assert.That(_strategy.CanHandle(MediaTypes.Movies, IdentifierType.Upc), Is.False);
    }

    #endregion

    #region LookupAsync

    [Test]
    public async Task LookupAsync_WithValidUpc_ReturnsGameResponse()
    {
        var upc = "887256301891"; // example
        var upcTitle = "Halo Infinite - Xbox Series X";
        var upcResponse = CreateUpcItemResponse(upc, upcTitle, category: "Video Games", brand: "Microsoft", model: null);

        _upcItemDbClientMock
            .Setup(x => x.GetItemByCodeAsync(upc, It.IsAny<CancellationToken>()))
            .ReturnsAsync(upcResponse);

        var searchResults = new List<GiantBombSearchResult>
        {
            new(
                Id: 1,
                Name: "Halo Infinite",
                OriginalReleaseDate: "2021-12-08",
                Deck: "The next chapter of Master Chief",
                ApiDetailUrl: "https://www.giantbomb.com/api/game/3030-12345/")
        };

        _giantBombClientMock
            .Setup(x => x.SearchGameAsync("Halo Infinite", It.IsAny<CancellationToken>()))
            .ReturnsAsync(searchResults);

        var details = new GiantBombGameDetails(
            Name: "Halo Infinite",
            Genres: new List<GiantBombNameRef> { new("Shooter"), new("Action") },
            Developers: new List<GiantBombCompanyRef> { new("343 Industries") },
            Publishers: new List<GiantBombCompanyRef> { new("Xbox Game Studios") },
            Platforms: new List<GiantBombPlatformRef> { new("Xbox Series X|S", "XBSX"), new("Xbox One", "XONE") },
            OriginalReleaseDate: "2021-12-08",
            Description: "<p>Master Chief returns.</p>",
            Deck: "Master Chief returns.",
            Ratings: new List<GiantBombRatingRef> { new("ESRB: T") }
        );

        _giantBombClientMock
            .Setup(x => x.GetGameDetailsAsync("https://www.giantbomb.com/api/game/3030-12345/", It.IsAny<CancellationToken>()))
            .ReturnsAsync(details);

        var result = await _strategy.LookupAsync(IdentifierType.Upc, upc, CancellationToken.None);

        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Title, Does.Contain("Halo Infinite"));
        Assert.That(result.Platform, Is.EqualTo("Xbox Series X|S"));
        Assert.That(result.Format, Is.EqualTo("Blu-ray Disc"));
        Assert.That(result.ReleaseDate, Is.EqualTo("2021-12-08"));
        Assert.That(result.Rating, Is.EqualTo("ESRB: T"));
        Assert.That(result.Genres, Contains.Item("Shooter"));
        Assert.That(result.Developers, Contains.Item("343 Industries"));
        Assert.That(result.Publishers, Contains.Item("Xbox Game Studios"));
    }

    [Test]
    public async Task LookupAsync_WithInvalidUpc_ReturnsNull()
    {
        var upc = "000000000000";
        _upcItemDbClientMock
            .Setup(x => x.GetItemByCodeAsync(upc, It.IsAny<CancellationToken>()))
            .ReturnsAsync((UpcItemResponse?)null);

        var result = await _strategy.LookupAsync(IdentifierType.Upc, upc, CancellationToken.None);
        Assert.That(result, Is.Null);
        _giantBombClientMock.Verify(x => x.SearchGameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task LookupAsync_WithMultipleSearchResults_SelectsBestMatch()
    {
        var upc = "045496596583";
        var upcTitle = "Super Mario Odyssey - Nintendo Switch";
        var upcResponse = CreateUpcItemResponse(upc, upcTitle, category: "Video Games", brand: "Nintendo", model: null);

        _upcItemDbClientMock
            .Setup(x => x.GetItemByCodeAsync(upc, It.IsAny<CancellationToken>()))
            .ReturnsAsync(upcResponse);

        // Multiple results with varying match quality
        var searchResults = new List<GiantBombSearchResult>
        {
            new(
                Id: 999,
                Name: "Super Mario Bros.",
                OriginalReleaseDate: "1985-09-13",
                Deck: "The original",
                ApiDetailUrl: "https://www.giantbomb.com/api/game/3030-999/"),
            new(
                Id: 12345,
                Name: "Super Mario Odyssey",
                OriginalReleaseDate: "2017-10-27",
                Deck: "Mario goes on a globe-trotting adventure",
                ApiDetailUrl: "https://www.giantbomb.com/api/game/3030-12345/"),
            new(
                Id: 888,
                Name: "Super Mario 64",
                OriginalReleaseDate: "1996-06-23",
                Deck: "Classic 3D Mario",
                ApiDetailUrl: "https://www.giantbomb.com/api/game/3030-888/")
        };

        _giantBombClientMock
            .Setup(x => x.SearchGameAsync("Super Mario Odyssey", It.IsAny<CancellationToken>()))
            .ReturnsAsync(searchResults);

        var details = new GiantBombGameDetails(
            Name: "Super Mario Odyssey",
            Genres: new List<GiantBombNameRef> { new("Action"), new("Platformer") },
            Developers: new List<GiantBombCompanyRef> { new("Nintendo EPD") },
            Publishers: new List<GiantBombCompanyRef> { new("Nintendo") },
            Platforms: new List<GiantBombPlatformRef> { new("Nintendo Switch", "NSW") },
            OriginalReleaseDate: "2017-10-27",
            Description: "<p>Mario's next big adventure</p>",
            Deck: "Mario goes on a globe-trotting adventure",
            Ratings: new List<GiantBombRatingRef> { new("ESRB: E10+") }
        );

        _giantBombClientMock
            .Setup(x => x.GetGameDetailsAsync("https://www.giantbomb.com/api/game/3030-12345/", It.IsAny<CancellationToken>()))
            .ReturnsAsync(details);

        var result = await _strategy.LookupAsync(IdentifierType.Upc, upc, CancellationToken.None);

        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Title, Does.Contain("Super Mario Odyssey"));
        Assert.That(result.Platform, Is.EqualTo("Nintendo Switch"));
        
        // Verify it called GetGameDetailsAsync with the best match (id 12345), not the first result (id 999)
        _giantBombClientMock.Verify(
            x => x.GetGameDetailsAsync("https://www.giantbomb.com/api/game/3030-12345/", It.IsAny<CancellationToken>()), 
            Times.Once);
        _giantBombClientMock.Verify(
            x => x.GetGameDetailsAsync("https://www.giantbomb.com/api/game/3030-999/", It.IsAny<CancellationToken>()), 
            Times.Never);
    }

    #endregion

    #region FindBestMatch

    [Test]
    public void FindBestMatch_WithExactMatch_ReturnsExactMatch()
    {
        var results = new List<GiantBombSearchResult>
        {
            new(1, "Halo Infinite", "2021-12-08", "Description", "url1"),
            new(2, "Halo 3", "2007-09-25", "Description", "url2"),
            new(3, "Halo 2", "2004-11-09", "Description", "url3")
        };

        var bestMatch = GameLookupStrategy.FindBestMatch(results, "Halo Infinite");

        Assert.That(bestMatch, Is.Not.Null);
        Assert.That(bestMatch!.Id, Is.EqualTo(1));
        Assert.That(bestMatch.Name, Is.EqualTo("Halo Infinite"));
    }

    [Test]
    public void FindBestMatch_WithPartialMatch_ReturnsClosestMatch()
    {
        var results = new List<GiantBombSearchResult>
        {
            new(1, "Super Mario Bros.", "1985-09-13", "Description", "url1"),
            new(2, "Super Mario Odyssey", "2017-10-27", "Description", "url2"),
            new(3, "Super Mario 64", "1996-06-23", "Description", "url3")
        };

        var bestMatch = GameLookupStrategy.FindBestMatch(results, "Super Mario Odyssey");

        Assert.That(bestMatch, Is.Not.Null);
        Assert.That(bestMatch!.Id, Is.EqualTo(2));
        Assert.That(bestMatch.Name, Is.EqualTo("Super Mario Odyssey"));
    }

    [Test]
    public void FindBestMatch_WithSingleResult_ReturnsThatResult()
    {
        var results = new List<GiantBombSearchResult>
        {
            new(1, "The Legend of Zelda: Breath of the Wild", "2017-03-03", "Description", "url1")
        };

        var bestMatch = GameLookupStrategy.FindBestMatch(results, "Breath of the Wild");

        Assert.That(bestMatch, Is.Not.Null);
        Assert.That(bestMatch!.Id, Is.EqualTo(1));
    }

    [Test]
    public void FindBestMatch_WithEmptyList_ReturnsNull()
    {
        var results = new List<GiantBombSearchResult>();

        var bestMatch = GameLookupStrategy.FindBestMatch(results, "Some Game");

        Assert.That(bestMatch, Is.Null);
    }

    [Test]
    public void FindBestMatch_WithPoorMatches_FallsBackToFirst()
    {
        var results = new List<GiantBombSearchResult>
        {
            new(1, "Totally Different Game", "2020-01-01", "Description", "url1"),
            new(2, "Another Different Game", "2020-01-01", "Description", "url2")
        };

        // When no result scores >= 0.5, should fall back to first result
        var bestMatch = GameLookupStrategy.FindBestMatch(results, "Expected Game Title");

        Assert.That(bestMatch, Is.Not.Null);
        Assert.That(bestMatch!.Id, Is.EqualTo(1));
    }

    [Test]
    public void DeriveFormatFromPlatforms_WithPlayStation5Platform_ReturnsBlurayDisc()
    {
        var platforms = new List<GiantBombPlatformRef>
        {
            new("PlayStation 5", "PS5")
        };

        var format = GameLookupStrategy.DeriveFormatFromPlatforms(platforms, "PlayStation 5");

        Assert.That(format, Is.EqualTo("Blu-ray Disc"));
    }

    [Test]
    public void DeriveFormatFromPlatforms_WithPlayStation3Platform_ReturnsDvd()
    {
        var platforms = new List<GiantBombPlatformRef>
        {
            new("PlayStation 3", "PS3")
        };

        var format = GameLookupStrategy.DeriveFormatFromPlatforms(platforms, "PlayStation 3");

        Assert.That(format, Is.EqualTo("DVD"));
    }

    [Test]
    public void DeriveFormatFromPlatforms_WithPlayStation1Platform_ReturnsCdRom()
    {
        var platforms = new List<GiantBombPlatformRef>
        {
            new("PlayStation", "PS")
        };

        var format = GameLookupStrategy.DeriveFormatFromPlatforms(platforms, "PlayStation");

        Assert.That(format, Is.EqualTo("CD-ROM"));
    }

    [Test]
    public void DeriveFormatFromPlatforms_WithPcPlatform_ReturnsCdRom()
    {
        var platforms = new List<GiantBombPlatformRef>
        {
            new("PC", "PC")
        };

        var format = GameLookupStrategy.DeriveFormatFromPlatforms(platforms, "PC");

        Assert.That(format, Is.EqualTo("CD-ROM"));
    }

    [Test]
    public void DeriveFormatFromPlatforms_WithDreamcastPlatform_ReturnsGdRom()
    {
        var platforms = new List<GiantBombPlatformRef>
        {
            new("Dreamcast", "DC")
        };

        var format = GameLookupStrategy.DeriveFormatFromPlatforms(platforms, "Dreamcast");

        Assert.That(format, Is.EqualTo("GD-ROM"));
    }

    [Test]
    public void DeriveFormatFromPlatforms_WithNintendoSwitchPlatform_ReturnsCartridge()
    {
        var platforms = new List<GiantBombPlatformRef>
        {
            new("Nintendo Switch", "NSW")
        };

        var format = GameLookupStrategy.DeriveFormatFromPlatforms(platforms, "Nintendo Switch");

        Assert.That(format, Is.EqualTo("Cartridge"));
    }

    [Test]
    public void DeriveFormatFromPlatforms_WithXboxSeriesXPlatform_ReturnsBlurayDisc()
    {
        var platforms = new List<GiantBombPlatformRef>
        {
            new("Xbox Series X|S", "XBSX")
        };

        var format = GameLookupStrategy.DeriveFormatFromPlatforms(platforms, "Xbox Series X|S");

        Assert.That(format, Is.EqualTo("Blu-ray Disc"));
    }

    [Test]
    public void DeriveFormatFromPlatforms_WithEmptyPlatforms_ReturnsEmpty()
    {
        var platforms = new List<GiantBombPlatformRef>();

        var format = GameLookupStrategy.DeriveFormatFromPlatforms(platforms, "");

        Assert.That(format, Is.EqualTo(string.Empty));
    }

    [Test]
    public void DeriveFormatFromPlatforms_WithNullPlatforms_ReturnsEmpty()
    {
        var format = GameLookupStrategy.DeriveFormatFromPlatforms(null, "");

        Assert.That(format, Is.EqualTo(string.Empty));
    }

    #endregion

    #region CleanGameTitleAndExtractEdition

    [Test]
    public void CleanGameTitleAndExtractEdition_RemovesPrePlayed()
    {
        var (cleanedTitle, edition) = GameLookupStrategy.CleanGameTitleAndExtractEdition("Black - Pre-Played");

        Assert.That(cleanedTitle, Is.EqualTo("Black"));
        Assert.That(edition, Is.Empty);
    }

    [Test]
    public void CleanGameTitleAndExtractEdition_RemovesGreatestHits()
    {
        var (cleanedTitle, edition) = GameLookupStrategy.CleanGameTitleAndExtractEdition("Heavy Rain - Greatest Hits");

        Assert.That(cleanedTitle, Is.EqualTo("Heavy Rain"));
        Assert.That(edition, Is.Empty);
    }

    [Test]
    public void CleanGameTitleAndExtractEdition_RemovesPreOwned()
    {
        var (cleanedTitle, edition) = GameLookupStrategy.CleanGameTitleAndExtractEdition("God of War - Pre-Owned");

        Assert.That(cleanedTitle, Is.EqualTo("God of War"));
        Assert.That(edition, Is.Empty);
    }

    [Test]
    public void CleanGameTitleAndExtractEdition_RemovesPlatinumHits()
    {
        var (cleanedTitle, edition) = GameLookupStrategy.CleanGameTitleAndExtractEdition("Halo 3 - Platinum Hits");

        Assert.That(cleanedTitle, Is.EqualTo("Halo 3"));
        Assert.That(edition, Is.Empty);
    }

    [Test]
    public void CleanGameTitleAndExtractEdition_RemovesPlayersChoice()
    {
        var (cleanedTitle, edition) = GameLookupStrategy.CleanGameTitleAndExtractEdition("Super Smash Bros - Player's Choice");

        Assert.That(cleanedTitle, Is.EqualTo("Super Smash Bros"));
        Assert.That(edition, Is.Empty);
    }

    [Test]
    public void CleanGameTitleAndExtractEdition_RemovesNintendoSelects()
    {
        var (cleanedTitle, edition) = GameLookupStrategy.CleanGameTitleAndExtractEdition("Mario Kart 7 - Nintendo Selects");

        Assert.That(cleanedTitle, Is.EqualTo("Mario Kart 7"));
        Assert.That(edition, Is.Empty);
    }

    [Test]
    public void CleanGameTitleAndExtractEdition_RemovesUsed()
    {
        var (cleanedTitle, edition) = GameLookupStrategy.CleanGameTitleAndExtractEdition("The Last of Us - Used");

        Assert.That(cleanedTitle, Is.EqualTo("The Last of Us"));
        Assert.That(edition, Is.Empty);
    }

    [Test]
    public void CleanGameTitleAndExtractEdition_RemovesEssentials()
    {
        var (cleanedTitle, edition) = GameLookupStrategy.CleanGameTitleAndExtractEdition("Uncharted - Essentials");

        Assert.That(cleanedTitle, Is.EqualTo("Uncharted"));
        Assert.That(edition, Is.Empty);
    }

    [Test]
    public void CleanGameTitleAndExtractEdition_ExtractsDeluxeEdition()
    {
        var (cleanedTitle, edition) = GameLookupStrategy.CleanGameTitleAndExtractEdition("Cyberpunk 2077 Deluxe Edition");

        Assert.That(cleanedTitle, Is.EqualTo("Cyberpunk 2077"));
        Assert.That(edition, Is.EqualTo("Deluxe"));
    }

    [Test]
    public void CleanGameTitleAndExtractEdition_RemovesPlatformIndicators()
    {
        var (cleanedTitle, edition) = GameLookupStrategy.CleanGameTitleAndExtractEdition("Spider-Man - PS5");

        Assert.That(cleanedTitle, Is.EqualTo("Spider-Man"));
        Assert.That(edition, Is.Empty);
    }

    [Test]
    public void CleanGameTitleAndExtractEdition_HandlesComplexTitle()
    {
        var (cleanedTitle, edition) = GameLookupStrategy.CleanGameTitleAndExtractEdition("Red Dead Redemption 2 - PlayStation 4 - Pre-Played");

        Assert.That(cleanedTitle, Is.EqualTo("Red Dead Redemption 2"));
        Assert.That(edition, Is.Empty);
    }

    [Test]
    public void CleanGameTitleAndExtractEdition_HandlesEmptyString()
    {
        var (cleanedTitle, edition) = GameLookupStrategy.CleanGameTitleAndExtractEdition("");

        Assert.That(cleanedTitle, Is.Empty);
        Assert.That(edition, Is.Empty);
    }

    [Test]
    public void CleanGameTitleAndExtractEdition_HandlesWhitespace()
    {
        var (cleanedTitle, edition) = GameLookupStrategy.CleanGameTitleAndExtractEdition("   ");

        Assert.That(cleanedTitle, Is.Empty);
        Assert.That(edition, Is.Empty);
    }

    #endregion

    #region Helpers

    private static UpcItemResponse CreateUpcItemResponse(string code, string title, string? category, string? brand, string? model)
    {
        var item = new UpcItem(
            Ean: code,
            Title: title,
            Description: title,
            Category: category,
            Brand: brand,
            Model: model,
            Isbn: null);
        return new UpcItemResponse(code, 1, [item]);
    }

    #endregion
}
