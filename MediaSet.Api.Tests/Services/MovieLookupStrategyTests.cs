// Enable nullable annotations for matching API signatures
#nullable enable
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MediaSet.Api.Clients;
using MediaSet.Api.Models;
using MediaSet.Api.Services.Lookup;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;

namespace MediaSet.Api.Tests.Services;

[TestFixture]
public class MovieLookupStrategyTests
{
    private Mock<IProductLookupClient> _productClient = null!;
    private Mock<IMovieMetadataClient> _metadataClient = null!;
    private MovieLookupStrategy _strategy = null!;

    [SetUp]
    public void Setup()
    {
        _productClient = new Mock<IProductLookupClient>(MockBehavior.Strict);
        _metadataClient = new Mock<IMovieMetadataClient>(MockBehavior.Strict);
        var logger = Mock.Of<ILogger<MovieLookupStrategy>>();
        _strategy = new MovieLookupStrategy(_productClient.Object, _metadataClient.Object, logger);
    }

    [Test]
    public void SupportsIdentifierType_HandlesUpcAndEan()
    {
        Assert.That(_strategy.SupportsIdentifierType("upc"), Is.True);
        Assert.That(_strategy.SupportsIdentifierType("ean"), Is.True);
        Assert.That(_strategy.SupportsIdentifierType("isbn"), Is.False);
    }

    [Test]
    public async Task LookupAsync_ReturnsNull_WhenProductNotFound()
    {
        _productClient.Setup(p => p.LookupBarcodeAsync("000000000000", It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProductLookupResponse?)null);

        var result = await _strategy.LookupAsync("upc", "000000000000", CancellationToken.None);
        Assert.That(result, Is.Null);
    }

    [Test]
    public async Task LookupAsync_MapsMetadataAndInfersFormat()
    {
        var product = new ProductLookupResponse(
            Title: "Example Movie (Blu-ray + Digital) (2018)",
            Brand: "Brand",
            Category: "Movies & TV",
            Images: new List<string>(),
            RawJson: "{}"
        );

        _productClient.Setup(p => p.LookupBarcodeAsync("012345678905", It.IsAny<CancellationToken>()))
            .ReturnsAsync(product);

        var meta = new MovieMetadataResponse(
            Title: "Example Movie",
            Genres: new List<string> { "Action" },
            ProductionCompanies: new List<string> { "Studio" },
            Overview: "An action film",
            ReleaseDate: "2018-01-01",
            Runtime: 120,
            Certification: "PG-13",
            IsTvSeries: false
        );

        _metadataClient.Setup(m => m.SearchAndGetDetailsAsync("Example Movie", 2018, It.IsAny<CancellationToken>()))
            .ReturnsAsync(meta);

        var result = await _strategy.LookupAsync("upc", "012345678905", CancellationToken.None);

        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Title, Is.EqualTo("Example Movie"));
        Assert.That(result.Genres, Has.Count.EqualTo(1));
        Assert.That(result.Studios, Has.Count.EqualTo(1));
        Assert.That(result.Rating, Is.EqualTo("PG-13"));
        Assert.That(result.ReleaseDate, Is.EqualTo("2018-01-01"));
        Assert.That(result.Runtime, Is.EqualTo(120));
        Assert.That(result.Format, Is.EqualTo("Blu-ray"));
        Assert.That(result.IsTvSeries, Is.False);
    }
}
