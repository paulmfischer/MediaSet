using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Net;
using MediaSet.Api.Features.Lookup.Endpoints;
using MediaSet.Api.Infrastructure.Lookup;
using MediaSet.Api.Infrastructure.Lookup.Clients.MusicBrainz;
using MediaSet.Api.Shared.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;

namespace MediaSet.Api.Tests.Features.Lookup.Endpoints;

[TestFixture]
public class LookupProviderFailureFilterTests
{
    private Mock<ILogger> _logger = null!;
    private DefaultHttpContext _httpContext = null!;

    [SetUp]
    public void Setup()
    {
        _logger = new Mock<ILogger>();
        _logger.Setup(l => l.IsEnabled(It.IsAny<LogLevel>())).Returns(true);
        _httpContext = new DefaultHttpContext();
    }

    [Test]
    public void ToProblem_TooManyRequestsHttpException_Returns429ProblemDetails()
    {
        var ex = new HttpRequestException("rate limited", null, HttpStatusCode.TooManyRequests);

        var result = LookupProviderFailureFilter.ToProblem(ex, _httpContext, _logger.Object);

        var problem = AssertProblem(result, StatusCodes.Status429TooManyRequests);
        Assert.That(problem.ProblemDetails.Title, Is.EqualTo("Lookup provider rate limit exceeded"));
        Assert.That(problem.ProblemDetails.Detail, Does.Contain("try again"));
    }

    [TestCase(HttpStatusCode.ServiceUnavailable)]
    [TestCase(HttpStatusCode.InternalServerError)]
    [TestCase(HttpStatusCode.BadGateway)]
    [TestCase(HttpStatusCode.Unauthorized)]
    [TestCase(null)]
    public void ToProblem_OtherUpstreamFailure_Returns503ProblemDetails(HttpStatusCode? status)
    {
        var ex = new HttpRequestException("upstream failed", null, status);

        var result = LookupProviderFailureFilter.ToProblem(ex, _httpContext, _logger.Object);

        var problem = AssertProblem(result, StatusCodes.Status503ServiceUnavailable);
        Assert.That(problem.ProblemDetails.Title, Is.EqualTo("Lookup provider unavailable"));
    }

    [Test]
    public void ToProblem_QuotaLimitedProviderExceptionWithRetryAt_Returns429WithRetryAfterHeader()
    {
        var ex = new ProviderLookupException("api.example.test", ImageLookupOutcome.QuotaLimited, "quota",
            DateTime.UtcNow.AddSeconds(30));

        var result = LookupProviderFailureFilter.ToProblem(ex, _httpContext, _logger.Object);

        AssertProblem(result, StatusCodes.Status429TooManyRequests);
        Assert.That(int.Parse(_httpContext.Response.Headers.RetryAfter.ToString()), Is.InRange(1, 30));
    }

    [Test]
    public void ToProblem_RateLimitWithoutRetryInfo_OmitsRetryAfterHeader()
    {
        var ex = new HttpRequestException("rate limited", null, HttpStatusCode.TooManyRequests);

        LookupProviderFailureFilter.ToProblem(ex, _httpContext, _logger.Object);

        Assert.That(_httpContext.Response.Headers.ContainsKey("Retry-After"), Is.False);
    }

    [Test]
    public void ToProblem_ProviderFailure_LogsWarningWithProvider()
    {
        var ex = new ProviderLookupException("api.example.test", ImageLookupOutcome.ProviderUnavailable, "down");

        var result = LookupProviderFailureFilter.ToProblem(ex, _httpContext, _logger.Object);

        var problem = AssertProblem(result, StatusCodes.Status503ServiceUnavailable);
        Assert.That(problem.ProblemDetails.Detail, Does.Contain("api.example.test"));
        _logger.Verify(l => l.Log(
            LogLevel.Warning,
            It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((state, _) => $"{state}".Contains("api.example.test")),
            ex,
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Once);
        _logger.Verify(l => l.Log(
            LogLevel.Error,
            It.IsAny<EventId>(),
            It.IsAny<It.IsAnyType>(),
            It.IsAny<Exception?>(),
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Never);
    }

    [Test]
    public async Task MusicBrainzClient_ServiceUnavailable_ThrownExceptionMapsTo503NamingMusicBrainz()
    {
        var handler = new StubHandler(HttpStatusCode.ServiceUnavailable);
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://musicbrainz.test/") };
        var client = new MusicBrainzClient(http, Mock.Of<ILogger<MusicBrainzClient>>());

        var ex = await Assert.ThrowsAsync<HttpRequestException>(
            async () => await client.GetReleaseByIdAsync("abc", CancellationToken.None));
        var result = LookupProviderFailureFilter.ToProblem(ex!, _httpContext, _logger.Object);

        var problem = AssertProblem(result, StatusCodes.Status503ServiceUnavailable);
        Assert.That(problem.ProblemDetails.Detail, Does.Contain("MusicBrainz"));
    }

    private static ProblemHttpResult AssertProblem(object? result, int statusCode)
    {
        Assert.That(result, Is.InstanceOf<ProblemHttpResult>());
        var problem = (ProblemHttpResult)result!;
        Assert.That(problem.StatusCode, Is.EqualTo(statusCode));
        Assert.That(problem.ProblemDetails, Is.InstanceOf<ProblemDetails>());
        return problem;
    }

    private sealed class StubHandler(HttpStatusCode status) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(status));
    }
}
