using System;
using System.Threading;
using System.Threading.Tasks;
using MediaSet.Api.Features.Plex.Models;
using MediaSet.Api.Features.Plex.Services;
using MediaSet.Api.Infrastructure.Database;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
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

    [Test]
    public void Serialize_TvDocumentWithScopeAndSeasons_RoundTripsThroughBson()
    {
        var data = new PlexItemData
        {
            Id = "65a000000000000000000002",
            PlexItemId = "2001",
            OnPlex = true,
            Scope = new PlexScope(PlexScopeKinds.Range, 1, 3),
            Seasons = [new PlexSeasonData { Index = 2, PlexSeasonId = "2102", EpisodeCount = 13, DateAdded = new DateTime(2024, 1, 2, 0, 0, 0, DateTimeKind.Utc), DeepLink = "link" }],
            LastSyncedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        };

        var document = data.ToBsonDocument();
        var back = BsonSerializer.Deserialize<PlexItemData>(document);

        Assert.That(document["Scope"].AsBsonDocument.Contains("IsReadable"), Is.False);
        Assert.That(back.Scope, Is.EqualTo(data.Scope));
        Assert.That(back.Seasons, Has.Count.EqualTo(1));
        Assert.That(back.Seasons[0].PlexSeasonId, Is.EqualTo("2102"));
        Assert.That(back.Seasons[0].EpisodeCount, Is.EqualTo(13));
        Assert.That(back.Seasons[0].DateAdded, Is.EqualTo(data.Seasons[0].DateAdded));
    }

    [Test]
    public void Deserialize_MovieDocumentWithoutScopeOrSeasons_HasNullScopeAndNoSeasons()
    {
        var document = new PlexItemData { Id = "65a000000000000000000001", PlexItemId = "1001" }.ToBsonDocument();
        document.Remove("Scope");
        document.Remove("Seasons");

        var back = BsonSerializer.Deserialize<PlexItemData>(document);

        Assert.That(back.Scope, Is.Null);
        Assert.That(back.Seasons, Is.Empty);
    }
}
