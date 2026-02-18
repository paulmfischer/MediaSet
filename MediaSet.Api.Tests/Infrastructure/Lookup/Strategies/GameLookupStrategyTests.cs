using MediaSet.Api.Infrastructure.Lookup.Models;
using MediaSet.Api.Shared.Models;
using MediaSet.Api.Infrastructure.Lookup.Strategies;
using MediaSet.Api.Infrastructure.Lookup.Clients.Igdb;
using MediaSet.Api.Infrastructure.Lookup.Clients.UpcItemDb;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MediaSet.Api.Tests.Infrastructure.Lookup.Strategies;

[TestFixture]
public class GameLookupStrategyTests
{
    private Mock<IUpcItemDbClient> _upcItemDbClientMock = null!;
    private Mock<IIgdbClient> _igdbClientMock = null!;
    private Mock<ILogger<GameLookupStrategy>> _loggerMock = null!;
    private GameLookupStrategy _strategy = null!;

    [SetUp]
    public void SetUp()
    {
        _upcItemDbClientMock = new Mock<IUpcItemDbClient>();
        _igdbClientMock = new Mock<IIgdbClient>();
        _loggerMock = new Mock<ILogger<GameLookupStrategy>>();
        _strategy = new GameLookupStrategy(
            _upcItemDbClientMock.Object,
            _igdbClientMock.Object,
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

        var searchResults = new List<IgdbGame>
        {
            new(
                Id: 1,
                Name: "Halo Infinite",
                Summary: "The next chapter of Master Chief",
                FirstReleaseDate: 1638921600L,
                Genres: null,
                InvolvedCompanies: null,
                Platforms: null,
                AgeRatings: null,
                Cover: null)
        };

        _igdbClientMock
            .Setup(x => x.SearchGameAsync("Halo Infinite", It.IsAny<CancellationToken>()))
            .ReturnsAsync(searchResults);

        var details = new IgdbGame(
            Id: 1,
            Name: "Halo Infinite",
            Summary: "Master Chief returns.",
            FirstReleaseDate: 1638921600L,
            Genres: new List<IgdbNamedObject> { new(5, "Shooter"), new(14, "Action") },
            InvolvedCompanies: new List<IgdbInvolvedCompany>
            {
                new(new IgdbNamedObject(1, "343 Industries"), Developer: true, Publisher: false),
                new(new IgdbNamedObject(2, "Xbox Game Studios"), Developer: false, Publisher: true)
            },
            Platforms: new List<IgdbPlatform>
            {
                new(169, "Xbox Series X|S", "XBSX"),
                new(49, "Xbox One", "XONE")
            },
            AgeRatings: new List<IgdbAgeRating> { new(Category: 1, Rating: 10) },
            Cover: new IgdbCover(999, "//images.igdb.com/igdb/image/upload/t_thumb/co1234.jpg")
        );

        _igdbClientMock
            .Setup(x => x.GetGameDetailsAsync(1, It.IsAny<CancellationToken>()))
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
        _igdbClientMock.Verify(x => x.SearchGameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
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
        var searchResults = new List<IgdbGame>
        {
            new(999, "Super Mario Bros.", null, 495590400L, null, null, null, null, null),
            new(12345, "Super Mario Odyssey", null, 1509062400L, null, null, null, null, null),
            new(888, "Super Mario 64", null, 872352000L, null, null, null, null, null)
        };

        _igdbClientMock
            .Setup(x => x.SearchGameAsync("Super Mario Odyssey", It.IsAny<CancellationToken>()))
            .ReturnsAsync(searchResults);

        var details = new IgdbGame(
            Id: 12345,
            Name: "Super Mario Odyssey",
            Summary: "Mario goes on a globe-trotting adventure",
            FirstReleaseDate: 1509062400L,
            Genres: new List<IgdbNamedObject> { new(8, "Platform"), new(31, "Adventure") },
            InvolvedCompanies: new List<IgdbInvolvedCompany>
            {
                new(new IgdbNamedObject(70, "Nintendo EPD"), Developer: true, Publisher: false),
                new(new IgdbNamedObject(70, "Nintendo"), Developer: false, Publisher: true)
            },
            Platforms: new List<IgdbPlatform> { new(130, "Nintendo Switch", "NSW") },
            AgeRatings: new List<IgdbAgeRating> { new(Category: 1, Rating: 9) },
            Cover: null
        );

        _igdbClientMock
            .Setup(x => x.GetGameDetailsAsync(12345, It.IsAny<CancellationToken>()))
            .ReturnsAsync(details);

        var result = await _strategy.LookupAsync(IdentifierType.Upc, upc, CancellationToken.None);

        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Title, Does.Contain("Super Mario Odyssey"));
        Assert.That(result.Platform, Is.EqualTo("Nintendo Switch"));

        // Verify it called GetGameDetailsAsync with the best match (id 12345), not the first result (id 999)
        _igdbClientMock.Verify(
            x => x.GetGameDetailsAsync(12345, It.IsAny<CancellationToken>()),
            Times.Once);
        _igdbClientMock.Verify(
            x => x.GetGameDetailsAsync(999, It.IsAny<CancellationToken>()),
            Times.Never);
    }

    #endregion

    #region FindBestMatch

    [Test]
    public void FindBestMatch_WithExactMatch_ReturnsExactMatch()
    {
        var results = new List<IgdbGame>
        {
            new(1, "Halo Infinite", null, null, null, null, null, null, null),
            new(2, "Halo 3", null, null, null, null, null, null, null),
            new(3, "Halo 2", null, null, null, null, null, null, null)
        };

        var bestMatch = GameLookupStrategy.FindBestMatch(results, "Halo Infinite");

        Assert.That(bestMatch, Is.Not.Null);
        Assert.That(bestMatch!.Id, Is.EqualTo(1));
        Assert.That(bestMatch.Name, Is.EqualTo("Halo Infinite"));
    }

    [Test]
    public void FindBestMatch_WithPartialMatch_ReturnsClosestMatch()
    {
        var results = new List<IgdbGame>
        {
            new(1, "Super Mario Bros.", null, null, null, null, null, null, null),
            new(2, "Super Mario Odyssey", null, null, null, null, null, null, null),
            new(3, "Super Mario 64", null, null, null, null, null, null, null)
        };

        var bestMatch = GameLookupStrategy.FindBestMatch(results, "Super Mario Odyssey");

        Assert.That(bestMatch, Is.Not.Null);
        Assert.That(bestMatch!.Id, Is.EqualTo(2));
        Assert.That(bestMatch.Name, Is.EqualTo("Super Mario Odyssey"));
    }

    [Test]
    public void FindBestMatch_WithSingleResult_ReturnsThatResult()
    {
        var results = new List<IgdbGame>
        {
            new(1, "The Legend of Zelda: Breath of the Wild", null, null, null, null, null, null, null)
        };

        var bestMatch = GameLookupStrategy.FindBestMatch(results, "Breath of the Wild");

        Assert.That(bestMatch, Is.Not.Null);
        Assert.That(bestMatch!.Id, Is.EqualTo(1));
    }

    [Test]
    public void FindBestMatch_WithEmptyList_ReturnsNull()
    {
        var results = new List<IgdbGame>();

        var bestMatch = GameLookupStrategy.FindBestMatch(results, "Some Game");

        Assert.That(bestMatch, Is.Null);
    }

    [Test]
    public void FindBestMatch_WithPoorMatches_FallsBackToFirst()
    {
        var results = new List<IgdbGame>
        {
            new(1, "Totally Different Game", null, null, null, null, null, null, null),
            new(2, "Another Different Game", null, null, null, null, null, null, null)
        };

        // When no result scores >= 0.5, should fall back to first result
        var bestMatch = GameLookupStrategy.FindBestMatch(results, "Expected Game Title");

        Assert.That(bestMatch, Is.Not.Null);
        Assert.That(bestMatch!.Id, Is.EqualTo(1));
    }

    [Test]
    public void DeriveFormatFromPlatforms_WithPlayStation5Platform_ReturnsBlurayDisc()
    {
        var platforms = new List<IgdbPlatform>
        {
            new(167, "PlayStation 5", "PS5")
        };

        var format = GameLookupStrategy.DeriveFormatFromPlatforms(platforms, "PlayStation 5");

        Assert.That(format, Is.EqualTo("Blu-ray Disc"));
    }

    [Test]
    public void DeriveFormatFromPlatforms_WithPlayStation3Platform_ReturnsDvd()
    {
        var platforms = new List<IgdbPlatform>
        {
            new(9, "PlayStation 3", "PS3")
        };

        var format = GameLookupStrategy.DeriveFormatFromPlatforms(platforms, "PlayStation 3");

        Assert.That(format, Is.EqualTo("DVD"));
    }

    [Test]
    public void DeriveFormatFromPlatforms_WithPlayStation1Platform_ReturnsCdRom()
    {
        var platforms = new List<IgdbPlatform>
        {
            new(7, "PlayStation", "PS")
        };

        var format = GameLookupStrategy.DeriveFormatFromPlatforms(platforms, "PlayStation");

        Assert.That(format, Is.EqualTo("CD-ROM"));
    }

    [Test]
    public void DeriveFormatFromPlatforms_WithPcPlatform_ReturnsCdRom()
    {
        var platforms = new List<IgdbPlatform>
        {
            new(6, "PC (Microsoft Windows)", "PC")
        };

        var format = GameLookupStrategy.DeriveFormatFromPlatforms(platforms, "PC");

        Assert.That(format, Is.EqualTo("CD-ROM"));
    }

    [Test]
    public void DeriveFormatFromPlatforms_WithDreamcastPlatform_ReturnsGdRom()
    {
        var platforms = new List<IgdbPlatform>
        {
            new(23, "Dreamcast", "DC")
        };

        var format = GameLookupStrategy.DeriveFormatFromPlatforms(platforms, "Dreamcast");

        Assert.That(format, Is.EqualTo("GD-ROM"));
    }

    [Test]
    public void DeriveFormatFromPlatforms_WithNintendoSwitchPlatform_ReturnsCartridge()
    {
        var platforms = new List<IgdbPlatform>
        {
            new(130, "Nintendo Switch", "NSW")
        };

        var format = GameLookupStrategy.DeriveFormatFromPlatforms(platforms, "Nintendo Switch");

        Assert.That(format, Is.EqualTo("Cartridge"));
    }

    [Test]
    public void DeriveFormatFromPlatforms_WithXboxSeriesXPlatform_ReturnsBlurayDisc()
    {
        var platforms = new List<IgdbPlatform>
        {
            new(169, "Xbox Series X|S", "XBSX")
        };

        var format = GameLookupStrategy.DeriveFormatFromPlatforms(platforms, "Xbox Series X|S");

        Assert.That(format, Is.EqualTo("Blu-ray Disc"));
    }

    [Test]
    public void DeriveFormatFromPlatforms_WithEmptyPlatforms_ReturnsEmpty()
    {
        var platforms = new List<IgdbPlatform>();

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

    #region DecodeAgeRating

    [Test]
    public void DecodeAgeRating_WithEsrbTeen_ReturnsEsrbT()
    {
        var ratings = new List<IgdbAgeRating> { new(Category: 1, Rating: 10) };
        var result = GameLookupStrategy.DecodeAgeRating(ratings);
        Assert.That(result, Is.EqualTo("ESRB: T"));
    }

    [Test]
    public void DecodeAgeRating_WithEsrbEveryone_ReturnsEsrbE()
    {
        var ratings = new List<IgdbAgeRating> { new(Category: 1, Rating: 8) };
        var result = GameLookupStrategy.DecodeAgeRating(ratings);
        Assert.That(result, Is.EqualTo("ESRB: E"));
    }

    [Test]
    public void DecodeAgeRating_WithEsrbMature_ReturnsEsrbM()
    {
        var ratings = new List<IgdbAgeRating> { new(Category: 1, Rating: 11) };
        var result = GameLookupStrategy.DecodeAgeRating(ratings);
        Assert.That(result, Is.EqualTo("ESRB: M"));
    }

    [Test]
    public void DecodeAgeRating_PrefersEsrbOverPegi()
    {
        var ratings = new List<IgdbAgeRating>
        {
            new(Category: 2, Rating: 4), // PEGI 16
            new(Category: 1, Rating: 10) // ESRB T
        };
        var result = GameLookupStrategy.DecodeAgeRating(ratings);
        Assert.That(result, Is.EqualTo("ESRB: T"));
    }

    [Test]
    public void DecodeAgeRating_WithPegiOnly_ReturnsPegiRating()
    {
        var ratings = new List<IgdbAgeRating> { new(Category: 2, Rating: 5) }; // PEGI 18
        var result = GameLookupStrategy.DecodeAgeRating(ratings);
        Assert.That(result, Is.EqualTo("PEGI: 18"));
    }

    [Test]
    public void DecodeAgeRating_WithNull_ReturnsEmpty()
    {
        var result = GameLookupStrategy.DecodeAgeRating(null);
        Assert.That(result, Is.EqualTo(string.Empty));
    }

    [Test]
    public void DecodeAgeRating_WithEmptyList_ReturnsEmpty()
    {
        var result = GameLookupStrategy.DecodeAgeRating(new List<IgdbAgeRating>());
        Assert.That(result, Is.EqualTo(string.Empty));
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

    [Test]
    public void CleanGameTitleAndExtractEdition_RemovesFormatAndLanguageSegments()
    {
        var (cleanedTitle, edition) = GameLookupStrategy.CleanGameTitleAndExtractEdition("Alan Wake - Xbox 360 - DVD - English");

        Assert.That(cleanedTitle, Is.EqualTo("Alan Wake"));
        Assert.That(edition, Is.Empty);
    }

    [Test]
    public void CleanGameTitleAndExtractEdition_PreservesSubtitleWithMetadataSegments()
    {
        var (cleanedTitle, edition) = GameLookupStrategy.CleanGameTitleAndExtractEdition("Assassin's Creed IV - Black Flag - PS4 - Blu-ray");

        Assert.That(cleanedTitle, Is.EqualTo("Assassin's Creed IV - Black Flag"));
        Assert.That(edition, Is.Empty);
    }

    [Test]
    public void CleanGameTitleAndExtractEdition_RemovesRegionSegments()
    {
        var (cleanedTitle, edition) = GameLookupStrategy.CleanGameTitleAndExtractEdition("Dark Souls - PS3 - NTSC - English");

        Assert.That(cleanedTitle, Is.EqualTo("Dark Souls"));
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
