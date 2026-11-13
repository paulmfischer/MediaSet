using System.Net;
using System.Net.Http;
using MediaSet.Api.Infrastructure.Lookup;
using MediaSet.Api.Shared.Extensions;
using MediaSet.Api.Tests.Observability;
using Microsoft.Extensions.Logging;
using NUnit.Framework;

namespace MediaSet.Api.Tests.Infrastructure.Lookup;

[TestFixture]
public class ProviderFailureLoggingTests
{
    [TestCase(HttpStatusCode.TooManyRequests)]
    [TestCase(HttpStatusCode.InternalServerError)]
    [TestCase(HttpStatusCode.BadGateway)]
    [TestCase(HttpStatusCode.ServiceUnavailable)]
    public void LogProviderHttpFailure_RateLimitOr5xx_LogsWarningWithProvider(HttpStatusCode status)
    {
        var logger = new LevelCapturingLogger<ProviderFailureLoggingTests>();

        logger.LogProviderHttpFailure(new HttpRequestException("boom", null, status), LogProviders.Tmdb, "HTTP error for {Title}", "x");

        Assert.That(logger.Entries, Has.Count.EqualTo(1));
        Assert.That(logger.Entries[0].Level, Is.EqualTo(LogLevel.Warning));
        Assert.That(logger.Providers[0], Is.EqualTo("TMDB"));
    }

    [TestCase(HttpStatusCode.Unauthorized)]
    [TestCase(HttpStatusCode.BadRequest)]
    [TestCase(null)]
    public void LogProviderHttpFailure_OtherFailures_LogsErrorWithProvider(HttpStatusCode? status)
    {
        var logger = new LevelCapturingLogger<ProviderFailureLoggingTests>();

        logger.LogProviderHttpFailure(new HttpRequestException("boom", null, status), LogProviders.Igdb, "HTTP error for {Title}", "x");

        Assert.That(logger.Entries, Has.Count.EqualTo(1));
        Assert.That(logger.Entries[0].Level, Is.EqualTo(LogLevel.Error));
        Assert.That(logger.Providers[0], Is.EqualTo("IGDB"));
    }
}
