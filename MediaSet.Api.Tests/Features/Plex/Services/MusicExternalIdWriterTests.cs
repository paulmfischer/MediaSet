using System.Threading;
using System.Threading.Tasks;
using MediaSet.Api.Features.Plex.Services;
using MediaSet.Api.Infrastructure.Caching;
using MediaSet.Api.Infrastructure.Database;
using MediaSet.Api.Shared.Models;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using Moq;
using NUnit.Framework;

namespace MediaSet.Api.Tests.Features.Plex.Services;

[TestFixture]
public class MusicExternalIdWriterTests
{
    private const string Id = "65b000000000000000000001";

    private static BsonDocument Render(FilterDefinition<Music> filter) =>
        filter.Render(new RenderArgs<Music>(BsonSerializer.SerializerRegistry.GetSerializer<Music>(), BsonSerializer.SerializerRegistry));

    private static BsonDocument Render(UpdateDefinition<Music> update) =>
        update.Render(new RenderArgs<Music>(BsonSerializer.SerializerRegistry.GetSerializer<Music>(), BsonSerializer.SerializerRegistry)).AsBsonDocument;

    [Test]
    public void EmptyExternalIdFilter_MatchesOnlyTheAlbumWithAMissingNullOrBlankExternalId()
    {
        var doc = Render(MusicExternalIdWriter.EmptyExternalIdFilter(Id));

        var expected = new BsonDocument
        {
            { "_id", new ObjectId(Id) },
            {
                "$or", new BsonArray
                {
                    new BsonDocument("ExternalId", BsonNull.Value),
                    new BsonDocument("ExternalId", new BsonRegularExpression("^\\s*$"))
                }
            }
        };
        Assert.That(doc, Is.EqualTo(expected), doc.ToJson());
    }

    [Test]
    public void SetExternalIdUpdate_SetsOnlyExternalId()
    {
        var doc = Render(MusicExternalIdWriter.SetExternalIdUpdate("mbid:g1"));

        Assert.That(doc, Is.EqualTo(new BsonDocument("$set", new BsonDocument("ExternalId", "mbid:g1"))), doc.ToJson());
    }

    private static (MusicExternalIdWriter Writer, Mock<ICacheService> Cache) Build(long modified)
    {
        var collection = new Mock<IMongoCollection<Music>>();
        collection.Setup(c => c.UpdateOneAsync(It.IsAny<FilterDefinition<Music>>(), It.IsAny<UpdateDefinition<Music>>(), It.IsAny<UpdateOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UpdateResult.Acknowledged(modified, modified, null));
        var database = new Mock<IDatabaseService>();
        database.Setup(d => d.GetCollection<Music>()).Returns(collection.Object);
        var cache = new Mock<ICacheService>();
        return (new MusicExternalIdWriter(database.Object, cache.Object), cache);
    }

    [Test]
    public async Task SetIfEmptyAsync_Modified_ReturnsTrueAndInvalidatesTheMusicCaches()
    {
        var (writer, cache) = Build(1);

        var result = await writer.SetIfEmptyAsync(Id, "mbid:g1", CancellationToken.None);

        Assert.That(result, Is.True);
        cache.Verify(c => c.RemoveByPatternAsync("metadata:Musics:*"), Times.Once);
        cache.Verify(c => c.RemoveAsync("stats"), Times.Once);
    }

    [Test]
    public async Task SetIfEmptyAsync_NothingModified_ReturnsFalseAndKeepsTheCaches()
    {
        var (writer, cache) = Build(0);

        var result = await writer.SetIfEmptyAsync(Id, "mbid:g1", CancellationToken.None);

        Assert.That(result, Is.False);
        cache.Verify(c => c.RemoveByPatternAsync(It.IsAny<string>()), Times.Never);
        cache.Verify(c => c.RemoveAsync(It.IsAny<string>()), Times.Never);
    }
}
