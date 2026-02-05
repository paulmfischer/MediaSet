using MediaSet.Api.Features.Entities.Models;
using System;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MediaSet.Api.Infrastructure.Lookup;
using MediaSet.Api.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;
using NUnit.Framework;

namespace MediaSet.Api.Tests.Infrastructure.Lookup;

[TestFixture]
public class GiantBombClientTests
{
    private Mock<HttpMessageHandler> _httpMessageHandlerMock = null!;
    private HttpClient _httpClient = null!;
    private Mock<ILogger<GiantBombClient>> _loggerMock = null!;
    private IOptions<GiantBombConfiguration> _configuration = null!;
    private GiantBombClient _client = null!;

    [SetUp]
    public void Setup()
    {
        _httpMessageHandlerMock = new Mock<HttpMessageHandler>();
        _httpClient = new HttpClient(_httpMessageHandlerMock.Object)
        {
            BaseAddress = new Uri("https://www.giantbomb.com/api/")
        };
        _loggerMock = new Mock<ILogger<GiantBombClient>>();
        _configuration = Options.Create(new GiantBombConfiguration
        {
            BaseUrl = "https://www.giantbomb.com/api/",
            ApiKey = "test-api-key",
            Timeout = 10
        });
        _client = new GiantBombClient(_httpClient, _configuration, _loggerMock.Object);
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
        var responseJson = JsonSerializer.Serialize(new
        {
            status_code = 1,
            error = "OK",
            number_of_total_results = 2,
            results = new[]
            {
                new
                {
                    id = 12345,
                    name = "Halo Infinite",
                    original_release_date = "2021-12-08",
                    deck = "Master Chief returns",
                    api_detail_url = "https://www.giantbomb.com/api/game/3030-12345/"
                },
                new
                {
                    id = 67890,
                    name = "Halo Infinite Campaign",
                    original_release_date = "2021-12-08",
                    deck = "The campaign",
                    api_detail_url = "https://www.giantbomb.com/api/game/3030-67890/"
                }
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
        var responseJson = JsonSerializer.Serialize(new
        {
            status_code = 1,
            error = "OK",
            number_of_total_results = 0,
            results = Array.Empty<object>()
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

        var result = await _client.SearchGameAsync("NonexistentGame", CancellationToken.None);

        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Count, Is.EqualTo(0));
    }

    [Test]
    public async Task SearchGameAsync_WithErrorStatusCode_ReturnsNull()
    {
        var responseJson = JsonSerializer.Serialize(new
        {
            status_code = 100,
            error = "Invalid API Key",
            number_of_total_results = 0,
            results = Array.Empty<object>()
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

        var result = await _client.SearchGameAsync("Test Game", CancellationToken.None);

        Assert.That(result, Is.Null);
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
    public async Task GetGameDetailsAsync_WithValidGuid_ReturnsDetails()
    {
        var responseJson = JsonSerializer.Serialize(new
        {
            status_code = 1,
            error = "OK",
            results = new
            {
                name = "Halo Infinite",
                genres = new[] { new { name = "Shooter" }, new { name = "Action" } },
                developers = new[] { new { name = "343 Industries" } },
                publishers = new[] { new { name = "Xbox Game Studios" } },
                platforms = new[] { new { name = "Xbox Series X|S", abbreviation = "XBSX" } },
                original_release_date = "2021-12-08",
                description = "<p>Master Chief returns in this epic adventure.</p>",
                deck = "Master Chief returns",
                original_game_rating = new[] { new { name = "ESRB: T" } }
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

        var result = await _client.GetGameDetailsAsync("3030-12345", CancellationToken.None);

        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Name, Is.EqualTo("Halo Infinite"));
        Assert.That(result.Genres, Has.Count.EqualTo(2));
        Assert.That(result.Genres![0].Name, Is.EqualTo("Shooter"));
        Assert.That(result.Developers, Has.Count.EqualTo(1));
        Assert.That(result.Developers![0].Name, Is.EqualTo("343 Industries"));
        Assert.That(result.Publishers, Has.Count.EqualTo(1));
        Assert.That(result.Platforms, Has.Count.EqualTo(1));
        Assert.That(result.Platforms![0].Name, Is.EqualTo("Xbox Series X|S"));
        Assert.That(result.Deck, Is.EqualTo("Master Chief returns"));
    }

    [Test]
    public async Task GetGameDetailsAsync_WithFullUrl_ReturnsDetails()
    {
        var responseJson = JsonSerializer.Serialize(new
        {
            status_code = 1,
            error = "OK",
            results = new
            {
                name = "Super Mario Odyssey",
                genres = new[] { new { name = "Platformer" } },
                developers = new[] { new { name = "Nintendo EPD" } },
                publishers = new[] { new { name = "Nintendo" } },
                platforms = new[] { new { name = "Nintendo Switch", abbreviation = "NSW" } },
                original_release_date = "2017-10-27",
                description = "Mario's adventure",
                deck = "Mario goes on an adventure",
                original_game_rating = new[] { new { name = "ESRB: E10+" } }
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

        var result = await _client.GetGameDetailsAsync(
            "https://www.giantbomb.com/api/game/3030-61765/",
            CancellationToken.None);

        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Name, Is.EqualTo("Super Mario Odyssey"));
    }

    [Test]
    public async Task GetGameDetailsAsync_WithErrorStatusCode_ReturnsNull()
    {
        var responseJson = JsonSerializer.Serialize(new
        {
            status_code = 101,
            error = "Object Not Found",
            results = (object?)null
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

        var result = await _client.GetGameDetailsAsync("invalid-id", CancellationToken.None);

        Assert.That(result, Is.Null);
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
            await _client.GetGameDetailsAsync("3030-12345", CancellationToken.None));
    }

    #endregion
}
