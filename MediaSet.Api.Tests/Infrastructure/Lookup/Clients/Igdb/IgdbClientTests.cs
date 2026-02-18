using System;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MediaSet.Api.Infrastructure.Lookup.Clients.Igdb;
using MediaSet.Api.Infrastructure.Lookup.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;
using NUnit.Framework;

namespace MediaSet.Api.Tests.Infrastructure.Lookup.Clients.Igdb;

[TestFixture]
public class IgdbClientTests
{
    private Mock<HttpMessageHandler> _httpMessageHandlerMock = null!;
    private HttpClient _httpClient = null!;
    private Mock<IIgdbTokenService> _tokenServiceMock = null!;
    private Mock<ILogger<IgdbClient>> _loggerMock = null!;
    private IOptions<IgdbConfiguration> _configuration = null!;
    private IgdbClient _client = null!;

    [SetUp]
    public void Setup()
    {
        _httpMessageHandlerMock = new Mock<HttpMessageHandler>();
        _httpClient = new HttpClient(_httpMessageHandlerMock.Object)
        {
            BaseAddress = new Uri("https://api.igdb.com/v4/")
        };
        _tokenServiceMock = new Mock<IIgdbTokenService>();
        _tokenServiceMock
            .Setup(x => x.GetAccessTokenAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync("test-access-token");
        _loggerMock = new Mock<ILogger<IgdbClient>>();
        _configuration = Options.Create(new IgdbConfiguration
        {
            BaseUrl = "https://api.igdb.com/v4/",
            TokenUrl = "https://id.twitch.tv/oauth2/token",
            ClientId = "test-client-id",
            ClientSecret = "test-client-secret",
            Timeout = 10
        });
        _client = new IgdbClient(_httpClient, _tokenServiceMock.Object, _configuration, _loggerMock.Object);
    }

    [TearDown]
    public void TearDown()
    {
        _httpClient.Dispose();
    }

    #region SearchGameAsync Tests

    [Test]
    public async Task SearchGameAsync_WithValidTitle_ReturnsResults()
    {
        var responseJson = JsonSerializer.Serialize(new[]
        {
            new { id = 12345, name = "Halo Infinite", first_release_date = 1638921600L },
            new { id = 67890, name = "Halo Infinite Campaign", first_release_date = 1638921600L }
        });

        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(responseJson)
            });

        var result = await _client.SearchGameAsync("Halo Infinite", CancellationToken.None);

        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Count, Is.EqualTo(2));
        Assert.That(result[0].Id, Is.EqualTo(12345));
        Assert.That(result[0].Name, Is.EqualTo("Halo Infinite"));
        Assert.That(result[1].Id, Is.EqualTo(67890));
    }

    [Test]
    public async Task SearchGameAsync_WithNoResults_ReturnsEmptyList()
    {
        var responseJson = JsonSerializer.Serialize(Array.Empty<object>());

        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(responseJson)
            });

        var result = await _client.SearchGameAsync("NonexistentGame", CancellationToken.None);

        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Count, Is.EqualTo(0));
    }

    [Test]
    public void SearchGameAsync_WithRateLimit_ThrowsHttpRequestException()
    {
        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.TooManyRequests
            });

        Assert.ThrowsAsync<HttpRequestException>(async () =>
            await _client.SearchGameAsync("Test Game", CancellationToken.None));
    }

    [Test]
    public async Task SearchGameAsync_WithBadRequest_ReturnsNull()
    {
        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.BadRequest
            });

        var result = await _client.SearchGameAsync("Test Game", CancellationToken.None);

        Assert.That(result, Is.Null);
    }

    #endregion

    #region GetGameDetailsAsync Tests

    [Test]
    public async Task GetGameDetailsAsync_WithValidId_ReturnsGame()
    {
        var responseJson = JsonSerializer.Serialize(new[]
        {
            new
            {
                id = 12345,
                name = "Halo Infinite",
                summary = "Master Chief returns in this epic adventure.",
                first_release_date = 1638921600L,
                genres = new[] { new { id = 5, name = "Shooter" } },
                involved_companies = new[]
                {
                    new { company = new { id = 1, name = "343 Industries" }, developer = true, publisher = false },
                    new { company = new { id = 2, name = "Xbox Game Studios" }, developer = false, publisher = true }
                },
                platforms = new[] { new { id = 169, name = "Xbox Series X|S", abbreviation = "XBSX" } },
                age_ratings = new[] { new { category = 1, rating = 10 } },
                cover = new { id = 999, url = "//images.igdb.com/igdb/image/upload/t_thumb/co1234.jpg" }
            }
        });

        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(responseJson)
            });

        var result = await _client.GetGameDetailsAsync(12345, CancellationToken.None);

        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Id, Is.EqualTo(12345));
        Assert.That(result.Name, Is.EqualTo("Halo Infinite"));
        Assert.That(result.Summary, Is.EqualTo("Master Chief returns in this epic adventure."));
        Assert.That(result.Genres, Has.Count.EqualTo(1));
        Assert.That(result.Genres![0].Name, Is.EqualTo("Shooter"));
        Assert.That(result.InvolvedCompanies, Has.Count.EqualTo(2));
        Assert.That(result.Platforms, Has.Count.EqualTo(1));
        Assert.That(result.Platforms![0].Name, Is.EqualTo("Xbox Series X|S"));
        Assert.That(result.AgeRatings, Has.Count.EqualTo(1));
        Assert.That(result.AgeRatings![0].Category, Is.EqualTo(1));
        Assert.That(result.AgeRatings[0].Rating, Is.EqualTo(10));
        Assert.That(result.Cover?.Url, Is.EqualTo("//images.igdb.com/igdb/image/upload/t_thumb/co1234.jpg"));
    }

    [Test]
    public void GetGameDetailsAsync_WithRateLimit_ThrowsHttpRequestException()
    {
        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.TooManyRequests
            });

        Assert.ThrowsAsync<HttpRequestException>(async () =>
            await _client.GetGameDetailsAsync(12345, CancellationToken.None));
    }

    [Test]
    public async Task GetGameDetailsAsync_WithBadRequest_ReturnsNull()
    {
        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.BadRequest
            });

        var result = await _client.GetGameDetailsAsync(99999, CancellationToken.None);

        Assert.That(result, Is.Null);
    }

    #endregion

    #region FixCoverUrl Tests

    [Test]
    public void FixCoverUrl_WithProtocolRelativeUrl_PrependsHttps()
    {
        var result = IgdbClient.FixCoverUrl("//images.igdb.com/igdb/image/upload/t_thumb/co1234.jpg");
        Assert.That(result, Is.EqualTo("https://images.igdb.com/igdb/image/upload/t_cover_big/co1234.jpg"));
    }

    [Test]
    public void FixCoverUrl_ReplacesThumbWithCoverBig()
    {
        var result = IgdbClient.FixCoverUrl("https://images.igdb.com/igdb/image/upload/t_thumb/co1234.jpg");
        Assert.That(result, Is.EqualTo("https://images.igdb.com/igdb/image/upload/t_cover_big/co1234.jpg"));
    }

    [Test]
    public void FixCoverUrl_WithNull_ReturnsNull()
    {
        var result = IgdbClient.FixCoverUrl(null);
        Assert.That(result, Is.Null);
    }

    #endregion
}
