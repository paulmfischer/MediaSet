using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MediaSet.Api.Infrastructure.Lookup;
using MediaSet.Api.Infrastructure.Lookup.Clients.GoogleBooks;
using MediaSet.Api.Shared.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Moq.Protected;
using NUnit.Framework;

namespace MediaSet.Api.Tests.Infrastructure.Lookup.Clients.GoogleBooks;

[TestFixture]
public class GoogleBooksClientTests
{
    private Mock<HttpMessageHandler> _handler = null!;
    private HttpClient _httpClient = null!;
    private GoogleBooksClient _client = null!;
    private readonly List<Uri> _requests = [];

    [SetUp]
    public void Setup()
    {
        _requests.Clear();
        _handler = new Mock<HttpMessageHandler>();
        _httpClient = new HttpClient(_handler.Object) { BaseAddress = new Uri("https://www.googleapis.com/books/v1/") };
        _client = new GoogleBooksClient(_httpClient, NullLogger<GoogleBooksClient>.Instance);
    }

    [TearDown]
    public void TearDown() => _httpClient.Dispose();

    private void Respond(HttpStatusCode status, string body = "{}")
    {
        _handler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((request, _) => _requests.Add(request.RequestUri!))
            .ReturnsAsync(() => new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") });
    }

    private static string Volume(string isbn13, string? thumbnail) => $$"""
        {
          "id": "vol1",
          "volumeInfo": {
            "title": "Dune",
            "industryIdentifiers": [
              { "type": "ISBN_10", "identifier": "0441013597" },
              { "type": "ISBN_13", "identifier": "{{isbn13}}" }
            ]{{(thumbnail == null ? "" : $$""", "imageLinks": { "smallThumbnail": "http://books.google.com/small", "thumbnail": "{{thumbnail}}" }""")}}
          }
        }
        """;

    [Test]
    public async Task GetCoverUrlByIsbnAsync_VolumeListsIsbn_ReturnsHttpsThumbnailWithoutCurl()
    {
        Respond(HttpStatusCode.OK, $$"""{ "totalItems": 1, "items": [{{Volume("9780441013593",
            "http://books.google.com/books/content?id=vol1&printsec=frontcover&img=1&zoom=1&edge=curl&source=gbs_api")}}] }""");

        var url = await _client.GetCoverUrlByIsbnAsync("978-0-441-01359-3");

        Assert.That(url, Is.EqualTo("https://books.google.com/books/content?id=vol1&printsec=frontcover&img=1&zoom=1&source=gbs_api"));
        Assert.That(_requests, Has.Count.EqualTo(1));
        Assert.That(_requests[0].PathAndQuery, Does.StartWith("/books/v1/volumes?q=isbn:9780441013593"));
    }

    [Test]
    public async Task GetCoverUrlByIsbnAsync_OnlyOtherEditionReturned_ReturnsNull()
    {
        Respond(HttpStatusCode.OK, $$"""{ "items": [{{Volume("9780000000002", "http://books.google.com/other")}}] }""");

        var url = await _client.GetCoverUrlByIsbnAsync("9780441013593");

        Assert.That(url, Is.Null);
    }

    [Test]
    public async Task GetCoverUrlByIsbnAsync_Isbn10MatchesListedIdentifier_ReturnsCover()
    {
        Respond(HttpStatusCode.OK, $$"""{ "items": [{{Volume("9780441013593", "https://books.google.com/cover")}}] }""");

        var url = await _client.GetCoverUrlByIsbnAsync("0-441-01359-7");

        Assert.That(url, Is.EqualTo("https://books.google.com/cover"));
    }

    [Test]
    public async Task GetCoverUrlByIsbnAsync_VolumeWithoutImageLinks_ReturnsNull()
    {
        Respond(HttpStatusCode.OK, $$"""{ "items": [{{Volume("9780441013593", null)}}] }""");

        var url = await _client.GetCoverUrlByIsbnAsync("9780441013593");

        Assert.That(url, Is.Null);
    }

    [Test]
    public async Task GetCoverUrlByIsbnAsync_NoItems_ReturnsNull()
    {
        Respond(HttpStatusCode.OK, """{ "kind": "books#volumes", "totalItems": 0 }""");

        var url = await _client.GetCoverUrlByIsbnAsync("9780441013593");

        Assert.That(url, Is.Null);
    }

    [Test]
    public async Task GetCoverUrlByIsbnAsync_InvalidIsbn_ReturnsNullWithoutRequest()
    {
        Respond(HttpStatusCode.OK);

        var url = await _client.GetCoverUrlByIsbnAsync("not-an-isbn");

        Assert.That(url, Is.Null);
        Assert.That(_requests, Is.Empty);
    }

    [TestCase(HttpStatusCode.TooManyRequests, ImageLookupOutcome.QuotaLimited)]
    [TestCase(HttpStatusCode.ServiceUnavailable, ImageLookupOutcome.ProviderUnavailable)]
    public async Task GetCoverUrlByIsbnAsync_ProviderFailure_ThrowsClassifiedProviderException(HttpStatusCode status, ImageLookupOutcome outcome)
    {
        Respond(status);

        var ex = await Assert.ThrowsAsync<ProviderLookupException>(async () => await _client.GetCoverUrlByIsbnAsync("9780441013593"));

        Assert.That(ex!.Provider, Is.EqualTo("GoogleBooks"));
        Assert.That(ex.Outcome, Is.EqualTo(outcome));
    }

    [Test]
    public async Task GetCoverUrlByIsbnAsync_MalformedJson_ReturnsNull()
    {
        Respond(HttpStatusCode.OK, "{ not json");

        var url = await _client.GetCoverUrlByIsbnAsync("9780441013593");

        Assert.That(url, Is.Null);
    }
}
