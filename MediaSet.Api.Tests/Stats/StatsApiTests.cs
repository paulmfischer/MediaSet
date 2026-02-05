using MediaSet.Api.Features.Statistics.Services;
using MediaSet.Api.Features.Statistics.Models;
using NUnit.Framework;
using Moq;
using MediaSet.Api.Services;
using MediaSet.Api.Features.Entities.Models;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Hosting;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using System.Threading;
using ApiModels = MediaSet.Api.Features.Statistics.Models;

namespace MediaSet.Api.Tests.Stats;

[TestFixture]
public class StatsApiTests : IntegrationTestBase
{
    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;
    private Mock<IStatsService> _statsServiceMock = null!;

    [SetUp]
    public void Setup()
    {
        _statsServiceMock = new Mock<IStatsService>();

        _factory = CreateWebApplicationFactory()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    // Remove existing StatsService registration
                    var statsServiceDescriptor = services.SingleOrDefault(
                        d => d.ServiceType == typeof(IStatsService));
                    if (statsServiceDescriptor != null)
                        services.Remove(statsServiceDescriptor);

                    // Add mock service
                    services.AddScoped<IStatsService>(_ => _statsServiceMock.Object);
                });
            });

        _client = _factory.CreateClient();
    }

    [TearDown]
    public void TearDown()
    {
        _client?.Dispose();
        _factory?.Dispose();
    }

    [Test]
    public async Task GetStats_ShouldReturnMediaStats()
    {
        // Arrange
        var bookFormats = new List<string> { "Hardcover", "Paperback", "eBook" };
        var movieFormats = new List<string> { "Blu-ray", "DVD", "Digital" };

        var expectedStats = new ApiModels.Stats(
            new BookStats(
                Total: 150,
                TotalFormats: 3,
                Formats: bookFormats,
                UniqueAuthors: 75,
                TotalPages: 45000
            ),
            new MovieStats(
                Total: 100,
                TotalFormats: 3,
                Formats: movieFormats,
                TotalTvSeries: 15
            ),
            new GameStats(
                Total: 0,
                TotalFormats: 0,
                Formats: new List<string>(),
                TotalPlatforms: 0,
                Platforms: new List<string>()
            ),
            new MusicStats(
                Total: 0,
                TotalFormats: 0,
                Formats: new List<string>(),
                UniqueArtists: 0,
                TotalTracks: 0
            )
        );

        _statsServiceMock.Setup(s => s.GetMediaStatsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(expectedStats);

        // Act
        var response = await _client.GetAsync("/stats");
        var result = await response.Content.ReadFromJsonAsync<ApiModels.Stats>();

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(result, Is.Not.Null);

        // Verify book stats
        Assert.That(result!.BookStats.Total, Is.EqualTo(150));
        Assert.That(result.BookStats.TotalFormats, Is.EqualTo(3));
        Assert.That(result.BookStats.Formats.Count(), Is.EqualTo(3));
        Assert.That(result.BookStats.UniqueAuthors, Is.EqualTo(75));
        Assert.That(result.BookStats.TotalPages, Is.EqualTo(45000));

        // Verify movie stats
        Assert.That(result.MovieStats.Total, Is.EqualTo(100));
        Assert.That(result.MovieStats.TotalFormats, Is.EqualTo(3));
        Assert.That(result.MovieStats.Formats.Count(), Is.EqualTo(3));
        Assert.That(result.MovieStats.TotalTvSeries, Is.EqualTo(15));

        _statsServiceMock.Verify(s => s.GetMediaStatsAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task GetStats_ShouldReturnZeroStats_WhenNoMediaExists()
    {
        // Arrange
        var expectedStats = new ApiModels.Stats(
            new BookStats(
                Total: 0,
                TotalFormats: 0,
                Formats: new List<string>(),
                UniqueAuthors: 0,
                TotalPages: 0
            ),
            new MovieStats(
                Total: 0,
                TotalFormats: 0,
                Formats: new List<string>(),
                TotalTvSeries: 0
            )
,
            new GameStats(
                Total: 0,
                TotalFormats: 0,
                Formats: new List<string>(),
                TotalPlatforms: 0,
                Platforms: new List<string>()
            )
            ,
            new MusicStats(
                Total: 0,
                TotalFormats: 0,
                Formats: new List<string>(),
                UniqueArtists: 0,
                TotalTracks: 0
            )
        );

        _statsServiceMock.Setup(s => s.GetMediaStatsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(expectedStats);

        // Act
        var response = await _client.GetAsync("/stats");
        var result = await response.Content.ReadFromJsonAsync<ApiModels.Stats>();

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.BookStats.Total, Is.EqualTo(0));
        Assert.That(result.MovieStats.Total, Is.EqualTo(0));
        _statsServiceMock.Verify(s => s.GetMediaStatsAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task GetStats_ShouldReturnStatsWithOnlyBooks()
    {
        // Arrange
        var bookFormats = new List<string> { "Hardcover", "Paperback" };
        var expectedStats = new ApiModels.Stats(
            new BookStats(
                Total: 50,
                TotalFormats: 2,
                Formats: bookFormats,
                UniqueAuthors: 30,
                TotalPages: 15000
            ),
            new MovieStats(
                Total: 0,
                TotalFormats: 0,
                Formats: new List<string>(),
                TotalTvSeries: 0
            )
,
            new GameStats(
                Total: 0,
                TotalFormats: 0,
                Formats: new List<string>(),
                TotalPlatforms: 0,
                Platforms: new List<string>()
            )
            ,
            new MusicStats(
                Total: 0,
                TotalFormats: 0,
                Formats: new List<string>(),
                UniqueArtists: 0,
                TotalTracks: 0
            )
        );

        _statsServiceMock.Setup(s => s.GetMediaStatsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(expectedStats);

        // Act
        var response = await _client.GetAsync("/stats");
        var result = await response.Content.ReadFromJsonAsync<ApiModels.Stats>();

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.BookStats.Total, Is.EqualTo(50));
        Assert.That(result.MovieStats.Total, Is.EqualTo(0));
        _statsServiceMock.Verify(s => s.GetMediaStatsAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task GetStats_ShouldReturnStatsWithOnlyMovies()
    {
        // Arrange
        var movieFormats = new List<string> { "Blu-ray", "Digital" };
        var expectedStats = new ApiModels.Stats(
            new BookStats(
                Total: 0,
                TotalFormats: 0,
                Formats: new List<string>(),
                UniqueAuthors: 0,
                TotalPages: 0
            ),
            new MovieStats(
                Total: 75,
                TotalFormats: 2,
                Formats: movieFormats,
                TotalTvSeries: 10
            )
,
            new GameStats(
                Total: 0,
                TotalFormats: 0,
                Formats: new List<string>(),
                TotalPlatforms: 0,
                Platforms: new List<string>()
            )
            ,
            new MusicStats(
                Total: 0,
                TotalFormats: 0,
                Formats: new List<string>(),
                UniqueArtists: 0,
                TotalTracks: 0
            )
        );

        _statsServiceMock.Setup(s => s.GetMediaStatsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(expectedStats);

        // Act
        var response = await _client.GetAsync("/stats");
        var result = await response.Content.ReadFromJsonAsync<ApiModels.Stats>();

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.BookStats.Total, Is.EqualTo(0));
        Assert.That(result.MovieStats.Total, Is.EqualTo(75));
        Assert.That(result.MovieStats.TotalTvSeries, Is.EqualTo(10));
        _statsServiceMock.Verify(s => s.GetMediaStatsAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task GetStats_ShouldHandleLargeNumbers()
    {
        // Arrange
        var bookFormats = new List<string> { "Hardcover", "Paperback", "eBook", "Audiobook" };
        var movieFormats = new List<string> { "Blu-ray", "DVD", "Digital", "4K UHD" };

        var expectedStats = new ApiModels.Stats(
            new BookStats(
                Total: 10000,
                TotalFormats: 4,
                Formats: bookFormats,
                UniqueAuthors: 5000,
                TotalPages: 3000000
            ),
            new MovieStats(
                Total: 5000,
                TotalFormats: 4,
                Formats: movieFormats,
                TotalTvSeries: 500
            ),
            new GameStats(
                Total: 0,
                TotalFormats: 0,
                Formats: new List<string>(),
                TotalPlatforms: 0,
                Platforms: new List<string>()
            )
            ,
            new MusicStats(
                Total: 0,
                TotalFormats: 0,
                Formats: new List<string>(),
                UniqueArtists: 0,
                TotalTracks: 0
            )
        );

        _statsServiceMock.Setup(s => s.GetMediaStatsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(expectedStats);

        // Act
        var response = await _client.GetAsync("/stats");
        var result = await response.Content.ReadFromJsonAsync<ApiModels.Stats>();

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.BookStats.Total, Is.EqualTo(10000));
        Assert.That(result!.BookStats.UniqueAuthors, Is.EqualTo(5000));
        Assert.That(result!.BookStats.TotalPages, Is.EqualTo(3000000));
        Assert.That(result!.MovieStats.Total, Is.EqualTo(5000));
        Assert.That(result!.MovieStats.TotalTvSeries, Is.EqualTo(500));
        _statsServiceMock.Verify(s => s.GetMediaStatsAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task GetStats_ShouldReturnMultipleFormats()
    {
        // Arrange
        var bookFormats = new List<string> { "Hardcover", "Paperback", "eBook", "Audiobook", "Large Print" };
        var movieFormats = new List<string> { "Blu-ray", "DVD", "Digital", "4K UHD", "VHS" };

        var expectedStats = new ApiModels.Stats(
            new BookStats(
                Total: 200,
                TotalFormats: 5,
                Formats: bookFormats,
                UniqueAuthors: 100,
                TotalPages: 60000
            ),
            new MovieStats(
                Total: 150,
                TotalFormats: 5,
                Formats: movieFormats,
                TotalTvSeries: 20
            ),
            new GameStats(
                Total: 0,
                TotalFormats: 0,
                Formats: new List<string>(),
                TotalPlatforms: 0,
                Platforms: new List<string>()
            )
            ,
            new MusicStats(
                Total: 0,
                TotalFormats: 0,
                Formats: new List<string>(),
                UniqueArtists: 0,
                TotalTracks: 0
            )
        );

        _statsServiceMock.Setup(s => s.GetMediaStatsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(expectedStats);

        // Act
        var response = await _client.GetAsync("/stats");
        var result = await response.Content.ReadFromJsonAsync<ApiModels.Stats>();

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.BookStats.TotalFormats, Is.EqualTo(5));
        Assert.That(result.BookStats.Formats.Count(), Is.EqualTo(5));
        Assert.That(result.BookStats.Formats, Does.Contain("Hardcover"));
        Assert.That(result.BookStats.Formats, Does.Contain("Audiobook"));
        Assert.That(result.MovieStats.TotalFormats, Is.EqualTo(5));
        Assert.That(result.MovieStats.Formats.Count(), Is.EqualTo(5));
        Assert.That(result.MovieStats.Formats, Does.Contain("Blu-ray"));
        Assert.That(result.MovieStats.Formats, Does.Contain("VHS"));
        _statsServiceMock.Verify(s => s.GetMediaStatsAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task GetStats_ShouldHandleSingleFormat()
    {
        // Arrange
        var bookFormats = new List<string> { "eBook" };
        var movieFormats = new List<string> { "Digital" };

        var expectedStats = new ApiModels.Stats(
            new BookStats(
                Total: 25,
                TotalFormats: 1,
                Formats: bookFormats,
                UniqueAuthors: 15,
                TotalPages: 7500
            ),
            new MovieStats(
                Total: 30,
                TotalFormats: 1,
                Formats: movieFormats,
                TotalTvSeries: 5
            ),
            new GameStats(
                Total: 0,
                TotalFormats: 0,
                Formats: new List<string>(),
                TotalPlatforms: 0,
                Platforms: new List<string>()
            )
            ,
            new MusicStats(
                Total: 0,
                TotalFormats: 0,
                Formats: new List<string>(),
                UniqueArtists: 0,
                TotalTracks: 0
            )
        );

        _statsServiceMock.Setup(s => s.GetMediaStatsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(expectedStats);

        // Act
        var response = await _client.GetAsync("/stats");
        var result = await response.Content.ReadFromJsonAsync<ApiModels.Stats>();

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.BookStats.TotalFormats, Is.EqualTo(1));
        Assert.That(result.BookStats.Formats.Count(), Is.EqualTo(1));
        Assert.That(result.MovieStats.TotalFormats, Is.EqualTo(1));
        Assert.That(result.MovieStats.Formats.Count(), Is.EqualTo(1));
        _statsServiceMock.Verify(s => s.GetMediaStatsAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task GetStats_ShouldHandleZeroTvSeries()
    {
        // Arrange
        var movieFormats = new List<string> { "Blu-ray", "DVD" };
        var expectedStats = new ApiModels.Stats(
            new BookStats(
                Total: 100,
                TotalFormats: 2,
                Formats: new List<string> { "Hardcover", "Paperback" },
                UniqueAuthors: 50,
                TotalPages: 30000
            ),
            new MovieStats(
                Total: 80,
                TotalFormats: 2,
                Formats: movieFormats,
                TotalTvSeries: 0
            ),
            new GameStats(
                Total: 0,
                TotalFormats: 0,
                Formats: new List<string>(),
                TotalPlatforms: 0,
                Platforms: new List<string>()
            )
            ,
            new MusicStats(
                Total: 0,
                TotalFormats: 0,
                Formats: new List<string>(),
                UniqueArtists: 0,
                TotalTracks: 0
            )
        );

        _statsServiceMock.Setup(s => s.GetMediaStatsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(expectedStats);

        // Act
        var response = await _client.GetAsync("/stats");
        var result = await response.Content.ReadFromJsonAsync<ApiModels.Stats>();

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.MovieStats.TotalTvSeries, Is.EqualTo(0));
        _statsServiceMock.Verify(s => s.GetMediaStatsAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task GetStats_ShouldHandleAllTvSeries()
    {
        // Arrange
        var movieFormats = new List<string> { "Digital", "Blu-ray" };
        var expectedStats = new ApiModels.Stats(
            new BookStats(
                Total: 50,
                TotalFormats: 1,
                Formats: new List<string> { "Hardcover" },
                UniqueAuthors: 25,
                TotalPages: 15000
            ),
            new MovieStats(
                Total: 40,
                TotalFormats: 2,
                Formats: movieFormats,
                TotalTvSeries: 40
            ),
            new GameStats(
                Total: 0,
                TotalFormats: 0,
                Formats: new List<string>(),
                TotalPlatforms: 0,
                Platforms: new List<string>()
            )
            ,
            new MusicStats(
                Total: 0,
                TotalFormats: 0,
                Formats: new List<string>(),
                UniqueArtists: 0,
                TotalTracks: 0
            )
        );

        _statsServiceMock.Setup(s => s.GetMediaStatsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(expectedStats);

        // Act
        var response = await _client.GetAsync("/stats");
        var result = await response.Content.ReadFromJsonAsync<ApiModels.Stats>();

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.MovieStats.TotalTvSeries, Is.EqualTo(40));
        Assert.That(result.MovieStats.Total, Is.EqualTo(result.MovieStats.TotalTvSeries));
        _statsServiceMock.Verify(s => s.GetMediaStatsAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
