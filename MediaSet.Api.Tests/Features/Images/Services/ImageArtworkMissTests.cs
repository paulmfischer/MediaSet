using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using MediaSet.Api.Features.Images.Services;
using MediaSet.Api.Infrastructure.Lookup.Clients.Igdb;
using MediaSet.Api.Infrastructure.Lookup.Clients.MusicBrainz;
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

/// <summary>Artwork lookup misses from issue #613: game titles and platforms, music release-group fallback.</summary>
[TestFixture]
public class ImageArtworkMissTests
{
    private static IgdbGame Game(int id, string title, int year, string[] platforms, string[]? alternatives = null) => new(
        id, title, null, new DateTimeOffset(year, 1, 1, 0, 0, 0, TimeSpan.Zero).ToUnixTimeSeconds(), null, null,
        [.. platforms.Select((p, index) => new IgdbPlatform(index, p, null))], null,
        new IgdbCover(id, $"//images.igdb.com/igdb/image/upload/t_thumb/{id}.jpg"),
        alternatives?.Select(a => new IgdbAlternativeName(a)).ToList());

    private static async Task<ImageLookupResult> LookupGame(Game entity, params IgdbGame[] results)
    {
        var igdb = new Mock<IIgdbClient>();
        igdb.Setup(c => c.SearchGameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync([.. results]);
        using var services = new ServiceCollection().AddSingleton<ILookupStrategy<GameResponse>>(
            new GameLookupStrategy(new Mock<IUpcItemDbClient>().Object, igdb.Object,
                NullLogger<GameLookupStrategy>.Instance)).BuildServiceProvider();
        var images = new Mock<IImageService>();
        images.Setup(c => c.DownloadAndSaveImageAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<CancellationToken>())).ReturnsAsync(new Image { FilePath = "cover.jpg" });
        var service = new ImageLookupService(new LookupStrategyFactory(services), images.Object,
            NullLogger<ImageLookupService>.Instance);
        return await service.LookupAndSaveImageAsync(entity, MediaTypes.Games, CancellationToken.None);
    }

    [TestCase("Wii Fit", "Nintendo Wii", "Wii")]
    [TestCase("Wii Fit", "Wii", "Wii")]
    [TestCase("Halo 3", "Microsoft Xbox 360", "Xbox 360")]
    [TestCase("Halo 3", "Sony PlayStation 3", "PlayStation 3")]
    [TestCase("Halo 3", "Nintendo 64", "Nintendo 64")]
    [TestCase("Halo 3", "N64", "Nintendo 64")]
    [TestCase("Halo 3", "PC", "PC (Microsoft Windows)")]
    [TestCase("Halo 3", "Nintendo GameCube", "GameCube")]
    [TestCase("Forza Horizon 5", "Xbox Series S", "Xbox Series X|S")]
    [TestCase("Forza Horizon 5", "Xbox Series X", "Xbox Series X|S")]
    public async Task LookupAndSaveImageAsync_StoredPlatformHasManufacturer_MatchesIgdbPlatformName(
        string title, string stored, string igdbPlatform)
    {
        var result = await LookupGame(new Game { Id = "1", Title = title, Platform = stored }, Game(5, title, 2008, [igdbPlatform]));

        Assert.That(result.Success, Is.True);
        Assert.That(result.ConfirmedMatch?.ProviderId, Is.EqualTo("5"));
    }

    [Test]
    public async Task LookupAndSaveImageAsync_StoredPlatformIsADifferentConsole_DoesNotMatch()
    {
        var result = await LookupGame(new Game { Id = "1", Title = "Wii Fit", Platform = "Nintendo Wii" },
            Game(5, "Wii Fit", 2008, ["Wii U"]));

        Assert.That(result.Success, Is.False);
        Assert.That(result.Outcome, Is.EqualTo(ImageLookupOutcome.NoMatch));
    }

    [Test]
    public async Task LookupAndSaveImageAsync_TitleYearInBrackets_PicksTheRemakeFromThatYear()
    {
        var result = await LookupGame(new Game { Id = "1", Title = "Need for Speed Most Wanted (2012)", Platform = "PS Vita" },
            Game(1, "Need for Speed: Most Wanted", 2005, ["PlayStation 2", "PlayStation Vita"]),
            Game(2, "Need for Speed: Most Wanted", 2012, ["PlayStation Vita", "PC (Microsoft Windows)"]));

        Assert.That(result.Success, Is.True);
        Assert.That(result.ConfirmedMatch?.ProviderId, Is.EqualTo("2"));
    }

    [Test]
    public async Task LookupAndSaveImageAsync_TitleYearAndNoPlatform_DoesNotChooseBetweenSameNamedGames()
    {
        var result = await LookupGame(new Game { Id = "1", Title = "Need for Speed Most Wanted (2012)" },
            Game(1, "Need for Speed: Most Wanted", 2005, ["PlayStation 2"]),
            Game(2, "Need for Speed: Most Wanted", 2012, ["PC (Microsoft Windows)"]));

        Assert.That(result.ConfirmedMatch?.ProviderId, Is.EqualTo("2"));
    }

    [Test]
    public async Task LookupAndSaveImageAsync_StoredTitleIsRegionalName_MatchesIgdbAlternativeName()
    {
        var result = await LookupGame(new Game { Id = "1", Title = "FIFA Soccer 10", Platform = "Xbox 360" },
            Game(7, "FIFA 10", 2009, ["Xbox 360"], ["FIFA Soccer 10"]));

        Assert.That(result.Success, Is.True);
        Assert.That(result.ConfirmedMatch?.ProviderId, Is.EqualTo("7"));
    }

    [TestCase("Need for Speed Most Wanted (2012)", "Need for Speed Most Wanted", "2012")]
    [TestCase("Halo 3 [2007]", "Halo 3", "2007")]
    [TestCase("Wii Fit", "Wii Fit", "")]
    [TestCase("Tetris (Game Boy)", "Tetris (Game Boy)", "")]
    [TestCase("(2012)", "(2012)", "2012")]
    public void RetailGameTitle_BracketedYear_IsStrippedFromTheSearchTitleOnly(string raw, string cleaned, string year)
    {
        Assert.That(RetailGameTitle.Clean(raw), Is.EqualTo(cleaned));
        Assert.That(RetailGameTitle.TrailingYear(raw), Is.EqualTo(year));
    }

    private const string ReleaseFront = "https://coverartarchive.org/release/{0}/front";
    private const string GroupFront = "https://coverartarchive.org/release-group/{0}/front";

    private static MusicBrainzRelease Release(string id, string group, string title = "Gaslighter", string artist = "The Chicks") => new()
    {
        Id = id,
        Title = title,
        Date = "2020-07-17",
        ReleaseGroup = new() { Id = group },
        ArtistCredit = [new() { Name = artist }],
        Media = [new() { Format = "CD" }]
    };

    private static async Task<(ImageLookupResult Result, Mock<IImageService> Images)> LookupMusic(
        Music entity, IEnumerable<string> missingCovers, params MusicBrainzRelease[] releases)
    {
        var musicBrainz = new Mock<IMusicBrainzClient>();
        musicBrainz.Setup(c => c.SearchByEntityPropertiesAsync(It.IsAny<IReadOnlyDictionary<string, string>>(),
            It.IsAny<CancellationToken>())).ReturnsAsync(releases.ToList());
        foreach (var release in releases)
        {
            musicBrainz.Setup(c => c.GetReleaseByIdAsync(release.Id, It.IsAny<CancellationToken>())).ReturnsAsync(release);
        }
        using var services = new ServiceCollection().AddSingleton<ILookupStrategy<MusicResponse>>(
            new MusicLookupStrategy(musicBrainz.Object, NullLogger<MusicLookupStrategy>.Instance)).BuildServiceProvider();
        var images = new Mock<IImageService>();
        images.Setup(c => c.DownloadAndSaveImageAsync(It.IsAny<string>(), "Musics", "1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Image { FilePath = "cover.jpg" });
        foreach (var url in missingCovers)
        {
            images.Setup(c => c.DownloadAndSaveImageAsync(url, "Musics", "1", It.IsAny<CancellationToken>()))
                .ThrowsAsync(new HttpRequestException("Not Found", null, HttpStatusCode.NotFound));
        }
        var service = new ImageLookupService(new LookupStrategyFactory(services), images.Object,
            NullLogger<ImageLookupService>.Instance);
        return (await service.LookupAndSaveImageAsync(entity, MediaTypes.Musics, CancellationToken.None), images);
    }

    [Test]
    public async Task LookupAndSaveImageAsync_ReleaseHasNoFrontCover_UsesReleaseGroupCover()
    {
        var music = new Music { Id = "1", Title = "Gaslighter", Artist = "The Chicks" };

        var (result, images) = await LookupMusic(music, [string.Format(ReleaseFront, "r1")], Release("r1", "g1"));

        var groupUrl = string.Format(GroupFront, "g1");
        Assert.That(result.Success, Is.True);
        Assert.That(result.ImageUrl, Is.EqualTo(groupUrl));
        Assert.That(result.ConfirmedMatch!.ImageUrl, Is.EqualTo(groupUrl));
        Assert.That(result.ConfirmedMatch.ProviderId, Is.EqualTo("r1"));
        Assert.That(result.ConfirmedMatch.ArtworkScope, Does.Contain("Release-group"));
        images.Verify(c => c.DownloadAndSaveImageAsync(groupUrl, "Musics", "1", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task LookupAndSaveImageAsync_ReleaseHasFrontCover_DoesNotRequestReleaseGroup()
    {
        var music = new Music { Id = "1", Title = "Gaslighter", Artist = "The Chicks" };

        var (result, images) = await LookupMusic(music, [], Release("r1", "g1"));

        Assert.That(result.ImageUrl, Is.EqualTo(string.Format(ReleaseFront, "r1")));
        images.Verify(c => c.DownloadAndSaveImageAsync(string.Format(GroupFront, "g1"), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task LookupAndSaveImageAsync_ReleaseGroupHasNoCoverEither_ReportsMissingArtwork()
    {
        var music = new Music { Id = "1", Title = "Gaslighter", Artist = "The Chicks" };

        var (result, _) = await LookupMusic(music, [string.Format(ReleaseFront, "r1"), string.Format(GroupFront, "g1")],
            Release("r1", "g1"));

        Assert.That(result.Success, Is.False);
        Assert.That(result.Outcome, Is.EqualTo(ImageLookupOutcome.MissingArtwork));
    }

    [Test]
    public async Task LookupAndSaveImageAsync_SeveralEditionsOfOneReleaseGroup_AreNotAmbiguous()
    {
        var music = new Music { Id = "1", Title = "Gaslighter", Artist = "The Chicks" };

        var (result, _) = await LookupMusic(music,
            [string.Format(ReleaseFront, "r1"), string.Format(ReleaseFront, "r2")],
            Release("r1", "g1"), Release("r2", "g1"));

        Assert.That(result.Success, Is.True);
        Assert.That(result.ImageUrl, Is.EqualTo(string.Format(GroupFront, "g1")));
    }

    [Test]
    public async Task LookupAndSaveImageAsync_SameTitleInDifferentReleaseGroups_StaysAmbiguous()
    {
        var music = new Music { Id = "1", Title = "Gaslighter", Artist = "The Chicks" };

        var (result, _) = await LookupMusic(music, [], Release("r1", "g1"), Release("r2", "g2"));

        Assert.That(result.Success, Is.False);
        Assert.That(result.Outcome, Is.EqualTo(ImageLookupOutcome.AmbiguousMatch));
    }

    [Test]
    public async Task LookupAndSaveImageAsync_StoredArtistDiffersFromMusicBrainzCredit_IsNoMatch()
    {
        var music = new Music { Id = "1", Title = "My Fair Lady", Artist = "British Cast" };

        var (result, _) = await LookupMusic(music, [], Release("r1", "g1", "My Fair Lady", "Lerner"));

        Assert.That(result.Outcome, Is.EqualTo(ImageLookupOutcome.NoMatch));
    }
}
