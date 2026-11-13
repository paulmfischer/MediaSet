using System;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Net;
using System.Net.Http.Json;
using MediaSet.Api.Infrastructure.Lookup.Clients.OpenLibrary;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using NUnit.Framework;

namespace MediaSet.Api.Tests.Features.Lookup.Endpoints;

[TestFixture]
public class LookupProviderFailureEndpointTests : IntegrationTestBase
{
    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;
    private Mock<IOpenLibraryClient> _openLibraryClientMock = null!;

    [SetUp]
    public void Setup()
    {
        _openLibraryClientMock = new Mock<IOpenLibraryClient>();
        _factory = CreateWebApplicationFactory().WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                foreach (var descriptor in services.Where(d => d.ServiceType == typeof(IOpenLibraryClient)).ToList())
                {
                    services.Remove(descriptor);
                }

                services.AddScoped<IOpenLibraryClient>(_ => _openLibraryClientMock.Object);
            }));
        _client = _factory.CreateClient();
    }

    [TearDown]
    public void TearDown()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    [Test]
    public async Task Lookup_ProviderRateLimited_Returns429ProblemDetails()
    {
        _openLibraryClientMock
            .Setup(c => c.GetReadableBookByIsbnAsync("9780134685991", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("rate limited", null, HttpStatusCode.TooManyRequests));

        var response = await _client.GetAsync("/lookup/books/isbn/9780134685991");
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.TooManyRequests));
        Assert.That(problem?.Title, Is.EqualTo("Lookup provider rate limit exceeded"));
    }

    [Test]
    public async Task Lookup_ProviderServerError_Returns503ProblemDetails()
    {
        _openLibraryClientMock
            .Setup(c => c.GetReadableBookByIsbnAsync("9780134685991", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("bad gateway", null, HttpStatusCode.BadGateway));

        var response = await _client.GetAsync("/lookup/books/isbn/9780134685991");
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.ServiceUnavailable));
        Assert.That(problem?.Title, Is.EqualTo("Lookup provider unavailable"));
    }

    [Test]
    public async Task Lookup_ProviderNetworkFailure_Returns503ProblemDetails()
    {
        _openLibraryClientMock
            .Setup(c => c.GetReadableBookByIsbnAsync("9780134685991", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("connection refused"));

        var response = await _client.GetAsync("/lookup/books/isbn/9780134685991");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.ServiceUnavailable));
    }

    [Test]
    public async Task Lookup_ProviderTimeout_Returns503ProblemDetails()
    {
        _openLibraryClientMock
            .Setup(c => c.GetReadableBookByIsbnAsync("9780134685991", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TaskCanceledException("timed out", new TimeoutException()));

        var response = await _client.GetAsync("/lookup/books/isbn/9780134685991");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.ServiceUnavailable));
    }

    [Test]
    public async Task Lookup_ProviderRejectsCredentials_Returns503WithCredentialsMessage()
    {
        _openLibraryClientMock
            .Setup(c => c.GetReadableBookByIsbnAsync("9780134685991", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("unauthorized", null, HttpStatusCode.Unauthorized));

        var response = await _client.GetAsync("/lookup/books/isbn/9780134685991");
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.ServiceUnavailable));
        Assert.That(problem?.Title, Is.EqualTo("Lookup provider rejected credentials"));
    }
}
