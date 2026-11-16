using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MediaSet.Api.Infrastructure.Lookup.Clients.MusicBrainz;
using MediaSet.Api.Infrastructure.Lookup.Models;
using MediaSet.Api.Infrastructure.Lookup.Strategies;
using MediaSet.Api.Shared.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NUnit.Framework;

namespace MediaSet.Api.Tests.Infrastructure.Lookup.Strategies;

[TestFixture]
public class MusicLookupStrategyTests
{
    private Mock<IMusicBrainzClient> _client = null!;
    private MusicLookupStrategy _strategy = null!;

    [SetUp]
    public void SetUp()
    {
        _client = new Mock<IMusicBrainzClient>();
        _strategy = new MusicLookupStrategy(_client.Object, NullLogger<MusicLookupStrategy>.Instance);
    }

    [Test]
    public async Task LookupAsync_ReleaseWithReleaseGroup_ReturnsMbidExternalId()
    {
        _client.Setup(c => c.GetReleaseByBarcodeAsync("0123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MusicBrainzRelease { Id = "rel-1" });
        _client.Setup(c => c.GetReleaseByIdAsync("rel-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MusicBrainzRelease { Id = "rel-1", Title = "Album", ReleaseGroup = new MusicBrainzReleaseGroup { Id = "rg-9" } });

        var results = await _strategy.LookupAsync(IdentifierType.Upc, new Dictionary<string, string> { ["barcode"] = "0123" }, CancellationToken.None);

        Assert.That(results, Has.Count.EqualTo(1));
        Assert.That(results[0].ExternalId, Is.EqualTo("mbid:rg-9"));
    }

    [Test]
    public async Task LookupAsync_ReleaseWithoutReleaseGroup_ReturnsNoExternalId()
    {
        _client.Setup(c => c.GetReleaseByIdAsync("rel-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MusicBrainzRelease { Id = "rel-1", Title = "Album" });

        var results = await _strategy.LookupAsync(
            IdentifierType.Entity, new Dictionary<string, string> { ["confirmedProviderId"] = "rel-1" }, CancellationToken.None);

        Assert.That(results[0].ExternalId, Is.Null);
    }
}
