using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Moq;
using Moq.Protected;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MediaSet.Api.Infrastructure.Lookup.Clients.UpcItemDb;
using MediaSet.Api.Infrastructure.Lookup.Models;

namespace MediaSet.Api.Tests.Infrastructure.Lookup.Clients.UpcItemDb;

[TestFixture]
public class UpcItemDbClientTests
{
    private Mock<ILogger<UpcItemDbClient>> _loggerMock = null!;
    private Mock<HttpMessageHandler> _httpMessageHandlerMock = null!;
    private HttpClient _httpClient = null!;
    private UpcItemDbConfiguration _configuration = null!;

    [SetUp]
    public void Setup()
    {
        _loggerMock = new Mock<ILogger<UpcItemDbClient>>();
        _httpMessageHandlerMock = new Mock<HttpMessageHandler>();
        _httpClient = new HttpClient(_httpMessageHandlerMock.Object)
        {
            BaseAddress = new Uri("https://api.upcitemdb.com/")
        };

        _configuration = new UpcItemDbConfiguration
        {
            BaseUrl = "https://api.upcitemdb.com/",
            Timeout = 10,
            MaxRequestsPerMinute = 100, // High limits for testing
            MaxRequestsPerDay = 1000,
            MinDelayBetweenRequestsMs = 0, // No delay for faster tests
            MaxRetryPauseSeconds = 5 // Short pause for fast tests
        };
    }

    [TearDown]
    public void TearDown()
    {
        _httpClient?.Dispose();
    }

    [Test]
    public async Task GetItemByCodeAsync_Success_ReturnsResult()
    {
        // Arrange
        var code = "012345678901";
        var responseJson = """
        {
            "code": "OK",
            "total": 1,
            "items": [
                {
                    "ean": "012345678901",
                    "title": "Test Product",
                    "brand": "Test Brand"
                }
            ]
        }
        """;

        SetupHttpResponse(HttpStatusCode.OK, responseJson, new Dictionary<string, string>
        {
            { "X-RateLimit-Limit", "100" },
            { "X-RateLimit-Remaining", "85" },
            { "X-RateLimit-Reset", "1644505200" }
        });

        var client = new UpcItemDbClient(_httpClient, Options.Create(_configuration), _loggerMock.Object);

        // Act
        var result = await client.GetItemByCodeAsync(code, CancellationToken.None);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Items, Has.Count.EqualTo(1));
        Assert.That(result.Items[0].Title, Is.EqualTo("Test Product"));

        // Verify rate limit headers were logged
        VerifyLogContains(LogLevel.Information, "UpcItemDb rate limit status");
    }

    [Test]
    public async Task GetItemByCodeAsync_EnforcesMinimumDelayBetweenRequests()
    {
        // Arrange
        var code1 = "012345678901";
        var code2 = "098765432109";
        var responseJson = """{"code": "OK", "total": 0, "items": []}""";

        SetupHttpResponse(HttpStatusCode.OK, responseJson);

        var config = new UpcItemDbConfiguration
        {
            BaseUrl = "https://api.upcitemdb.com/",
            Timeout = 10,
            MaxRequestsPerMinute = 100,
            MaxRequestsPerDay = 1000,
            MinDelayBetweenRequestsMs = 200, // 200ms delay for fast tests
            MaxRetryPauseSeconds = 5
        };

        var client = new UpcItemDbClient(_httpClient, Options.Create(config), _loggerMock.Object);

        // Act
        var startTime = DateTime.UtcNow;
        await client.GetItemByCodeAsync(code1, CancellationToken.None);
        await client.GetItemByCodeAsync(code2, CancellationToken.None);
        var endTime = DateTime.UtcNow;

        // Assert - Should take at least MinDelayBetweenRequestsMs
        var elapsed = endTime - startTime;
        Assert.That(elapsed.TotalMilliseconds, Is.GreaterThanOrEqualTo(150)); // Allow 50ms margin
    }

    [Test]
    public async Task GetItemByCodeAsync_BurstRateLimit_PausesAndRetries()
    {
        // Arrange
        var code = "012345678901";
        var resetTime = DateTimeOffset.UtcNow.AddSeconds(1).ToUnixTimeSeconds(); // 1 second pause for fast tests
        var responseJson = """{"code": "OK", "total": 1, "items": [{"ean": "012345678901", "title": "Test"}]}""";

        // First call returns 429, second call returns success
        var callCount = 0;
        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(() =>
            {
                callCount++;
                if (callCount == 1)
                {
                    var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
                    response.Headers.Add("X-RateLimit-Reset", resetTime.ToString());
                    return response;
                }
                else
                {
                    var response = new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent(responseJson)
                    };
                    response.Headers.Add("X-RateLimit-Limit", "100");
                    response.Headers.Add("X-RateLimit-Remaining", "85");
                    return response;
                }
            });

        var client = new UpcItemDbClient(_httpClient, Options.Create(_configuration), _loggerMock.Object);

        // Act
        var result = await client.GetItemByCodeAsync(code, CancellationToken.None);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(callCount, Is.EqualTo(2), "Should make two HTTP calls (initial + retry)");

        // Verify warning was logged
        VerifyLogContains(LogLevel.Warning, "burst rate limit hit");
    }

    [Test]
    public async Task GetItemByCodeAsync_DailyRateLimit_ReturnsNull()
    {
        // Arrange
        var code = "012345678901";
        var resetTime = DateTimeOffset.UtcNow.AddHours(10).ToUnixTimeSeconds(); // Far in future = daily limit

        var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
        response.Headers.Add("X-RateLimit-Reset", resetTime.ToString());

        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(response);

        var client = new UpcItemDbClient(_httpClient, Options.Create(_configuration), _loggerMock.Object);

        // Act
        var result = await client.GetItemByCodeAsync(code, CancellationToken.None);

        // Assert
        Assert.That(result, Is.Null, "Should return null for daily limit");
        VerifyLogContains(LogLevel.Error, "daily rate limit exceeded");
    }

    [Test]
    public async Task GetItemByCodeAsync_ParsesAndLogsRateLimitHeaders()
    {
        // Arrange
        var code = "012345678901";
        var responseJson = """{"code": "OK", "total": 0, "items": []}""";

        SetupHttpResponse(HttpStatusCode.OK, responseJson, new Dictionary<string, string>
        {
            { "X-RateLimit-Limit", "100" },
            { "X-RateLimit-Remaining", "42" },
            { "X-RateLimit-Reset", "1644505200" },
            { "X-RateLimit-Current", "58" }
        });

        var client = new UpcItemDbClient(_httpClient, Options.Create(_configuration), _loggerMock.Object);

        // Act
        await client.GetItemByCodeAsync(code, CancellationToken.None);

        // Assert
        VerifyLogContains(LogLevel.Information, "Limit=100");
        VerifyLogContains(LogLevel.Information, "Remaining=42");
        VerifyLogContains(LogLevel.Information, "Reset=1644505200");
        VerifyLogContains(LogLevel.Information, "Current=58");
    }

    [Test]
    public async Task GetItemByCodeAsync_SerializesConcurrentRequests()
    {
        // Arrange
        var responseJson = """{"code": "OK", "total": 0, "items": []}""";
        var concurrentCallsCount = 0;
        var maxConcurrentCalls = 0;

        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .Returns(async () =>
            {
                Interlocked.Increment(ref concurrentCallsCount);
                var currentCount = concurrentCallsCount;
                if (currentCount > maxConcurrentCalls)
                {
                    maxConcurrentCalls = currentCount;
                }

                await Task.Delay(100); // Simulate API latency

                Interlocked.Decrement(ref concurrentCallsCount);

                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(responseJson)
                };
            });

        var client = new UpcItemDbClient(_httpClient, Options.Create(_configuration), _loggerMock.Object);

        // Act - Make 3 concurrent requests
        var tasks = new[]
        {
            client.GetItemByCodeAsync("111111111111", CancellationToken.None),
            client.GetItemByCodeAsync("222222222222", CancellationToken.None),
            client.GetItemByCodeAsync("333333333333", CancellationToken.None)
        };

        await Task.WhenAll(tasks);

        // Assert
        Assert.That(maxConcurrentCalls, Is.EqualTo(1), "Should serialize requests (max 1 concurrent)");
    }

    [Test]
    [Ignore("This test can take up to 60 seconds due to minute-based rate limiting. Run manually if needed.")]
    public async Task GetItemByCodeAsync_ApproachingMinuteLimit_PausesProactively()
    {
        // Arrange
        var responseJson = """{"code": "OK", "total": 0, "items": []}""";
        SetupHttpResponse(HttpStatusCode.OK, responseJson);

        var config = new UpcItemDbConfiguration
        {
            BaseUrl = "https://api.upcitemdb.com/",
            Timeout = 10,
            MaxRequestsPerMinute = 2,
            MaxRequestsPerDay = 1000,
            MinDelayBetweenRequestsMs = 0,
            MaxRetryPauseSeconds = 65
        };

        var client = new UpcItemDbClient(_httpClient, Options.Create(config), _loggerMock.Object);

        // Act - Make 3 requests (should pause before third)
        await client.GetItemByCodeAsync("111111111111", CancellationToken.None);
        await client.GetItemByCodeAsync("222222222222", CancellationToken.None);

        var startTime = DateTime.UtcNow;
        await client.GetItemByCodeAsync("333333333333", CancellationToken.None);
        var elapsed = DateTime.UtcNow - startTime;

        // Assert - Should have paused until next minute (could take 0-60 seconds)
        Assert.That(elapsed.TotalSeconds, Is.GreaterThan(0.1), "Should pause when approaching per-minute limit");
        VerifyLogContains(LogLevel.Warning, "Approaching per-minute rate limit");
    }

    [Test]
    public async Task GetItemByCodeAsync_NonSuccessStatus_ReturnsNull()
    {
        // Arrange
        var code = "012345678901";

        SetupHttpResponse(HttpStatusCode.NotFound, """{"code": "NOT_FOUND", "message": "Product not found"}""");

        var client = new UpcItemDbClient(_httpClient, Options.Create(_configuration), _loggerMock.Object);

        // Act
        var result = await client.GetItemByCodeAsync(code, CancellationToken.None);

        // Assert
        Assert.That(result, Is.Null);
        VerifyLogContains(LogLevel.Warning, "returned status code");
    }

    [Test]
    public async Task GetItemByCodeAsync_InvalidResetHeader_ReturnsNullOnRateLimit()
    {
        // Arrange
        var code = "012345678901";

        var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
        response.Headers.Add("X-RateLimit-Reset", "invalid"); // Invalid reset time

        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(response);

        var client = new UpcItemDbClient(_httpClient, Options.Create(_configuration), _loggerMock.Object);

        // Act
        var result = await client.GetItemByCodeAsync(code, CancellationToken.None);

        // Assert
        Assert.That(result, Is.Null);
        VerifyLogContains(LogLevel.Warning, "no valid reset time");
    }

    [Test]
    public async Task GetItemByCodeAsync_EmptyResultSet_ReturnsEmpty()
    {
        // Arrange
        var code = "012345678901";
        var responseJson = """{"code": "OK", "total": 0, "items": []}""";

        SetupHttpResponse(HttpStatusCode.OK, responseJson);

        var client = new UpcItemDbClient(_httpClient, Options.Create(_configuration), _loggerMock.Object);

        // Act
        var result = await client.GetItemByCodeAsync(code, CancellationToken.None);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Items, Is.Empty);
    }

    [Test]
    public async Task GetItemByCodeAsync_RetryAfterBurstLimitFails_ReturnsNull()
    {
        // Arrange
        var code = "012345678901";
        var resetTime = DateTimeOffset.UtcNow.AddSeconds(1).ToUnixTimeSeconds();

        // Both calls return 429
        var response429 = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
        response429.Headers.Add("X-RateLimit-Reset", resetTime.ToString());

        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(response429);

        var client = new UpcItemDbClient(_httpClient, Options.Create(_configuration), _loggerMock.Object);

        // Act
        var result = await client.GetItemByCodeAsync(code, CancellationToken.None);

        // Assert
        Assert.That(result, Is.Null, "Should return null if retry also fails");
        VerifyLogContains(LogLevel.Warning, "Retry after burst rate limit pause failed");
    }

    private void SetupHttpResponse(HttpStatusCode statusCode, string content, Dictionary<string, string>? headers = null)
    {
        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(() =>
            {
                var response = new HttpResponseMessage(statusCode)
                {
                    Content = new StringContent(content)
                };

                if (headers != null)
                {
                    foreach (var header in headers)
                    {
                        response.Headers.Add(header.Key, header.Value);
                    }
                }

                return response;
            });
    }

    private void VerifyLogContains(LogLevel level, string messageSubstring)
    {
        _loggerMock.Verify(
            x => x.Log(
                level,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains(messageSubstring)),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }
}
