using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using MediaSet.Api.Features.Health.Services;
using NUnit.Framework;

namespace MediaSet.Api.Tests.Features.Health.Services;

[TestFixture]
public class HealthCheckProbeTests
{
    private static readonly Uri Endpoint = new("http://localhost:8080/health");

    [Test]
    public async Task RunAsync_SuccessfulResponse_RequestsHealthAndReturnsZero()
    {
        // Arrange
        using var handler = new StubHandler((request, _) =>
        {
            Assert.That(request.Method, Is.EqualTo(HttpMethod.Get));
            Assert.That(request.RequestUri, Is.EqualTo(Endpoint));
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        });
        using var client = new HttpClient(handler);

        // Act
        var exitCode = await HealthCheckProbe.RunAsync(client, Endpoint);

        // Assert
        Assert.That(exitCode, Is.Zero);
    }

    [TestCase(HttpStatusCode.Redirect)]
    [TestCase(HttpStatusCode.BadRequest)]
    [TestCase(HttpStatusCode.InternalServerError)]
    [TestCase(HttpStatusCode.ServiceUnavailable)]
    public async Task RunAsync_UnsuccessfulResponse_ReturnsOne(HttpStatusCode statusCode)
    {
        // Arrange
        using var handler = new StubHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(statusCode)));
        using var client = new HttpClient(handler);

        // Act
        var exitCode = await HealthCheckProbe.RunAsync(client, Endpoint);

        // Assert
        Assert.That(exitCode, Is.EqualTo(1));
    }

    [Test]
    public async Task RunAsync_ConnectionFailure_ReturnsOne()
    {
        // Arrange
        using var handler = new StubHandler((_, _) => throw new HttpRequestException("Connection refused"));
        using var client = new HttpClient(handler);

        // Act
        var exitCode = await HealthCheckProbe.RunAsync(client, Endpoint);

        // Assert
        Assert.That(exitCode, Is.EqualTo(1));
    }

    [Test]
    public async Task RunAsync_RequestTimesOut_ReturnsOne()
    {
        // Arrange
        using var handler = new StubHandler(async (_, cancellationToken) =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        using var client = new HttpClient(handler) { Timeout = TimeSpan.FromMilliseconds(50) };

        // Act
        var exitCode = await HealthCheckProbe.RunAsync(client, Endpoint);

        // Assert
        Assert.That(exitCode, Is.EqualTo(1));
    }

    private sealed class StubHandler(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> sendAsync) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) => sendAsync(request, cancellationToken);
    }
}
