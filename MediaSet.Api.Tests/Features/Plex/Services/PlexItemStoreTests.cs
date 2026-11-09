using System.Threading;
using System.Threading.Tasks;
using MediaSet.Api.Features.Plex.Models;
using MediaSet.Api.Features.Plex.Services;
using MediaSet.Api.Infrastructure.Database;
using MongoDB.Driver;
using Moq;
using NUnit.Framework;

namespace MediaSet.Api.Tests.Features.Plex.Services;

[TestFixture]
public class PlexItemStoreTests
{
    [Test]
    public async Task EnsureCreatedAsync_CreatesNamedPlexItemIdIndexInPlexItemsCollection()
    {
        var indexes = new Mock<IMongoIndexManager<PlexItemData>>();
        var collection = new Mock<IMongoCollection<PlexItemData>>();
        collection.SetupGet(c => c.Indexes).Returns(indexes.Object);
        var database = new Mock<IDatabaseService>();
        database.Setup(d => d.GetCollection<PlexItemData>("PlexItems")).Returns(collection.Object);

        await new PlexItemStore(database.Object).EnsureCreatedAsync(CancellationToken.None);

        indexes.Verify(i => i.CreateOneAsync(
            It.Is<CreateIndexModel<PlexItemData>>(m => m.Options.Name == PlexItemStore.PlexItemIdIndexName),
            It.IsAny<CreateOneIndexOptions>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
