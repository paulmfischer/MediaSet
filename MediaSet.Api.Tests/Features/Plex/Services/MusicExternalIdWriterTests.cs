using MediaSet.Api.Features.Plex.Services;
using MediaSet.Api.Shared.Models;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using NUnit.Framework;

namespace MediaSet.Api.Tests.Features.Plex.Services;

[TestFixture]
public class MusicExternalIdWriterTests
{
    private static BsonDocument Render(FilterDefinition<Music> filter) =>
        filter.Render(new RenderArgs<Music>(BsonSerializer.SerializerRegistry.GetSerializer<Music>(), BsonSerializer.SerializerRegistry));

    [Test]
    public void EmptyExternalIdFilter_MatchesOnlyTheAlbumWithAMissingNullOrBlankExternalId()
    {
        var id = ObjectId.GenerateNewId().ToString();

        var doc = Render(MusicExternalIdWriter.EmptyExternalIdFilter(id));

        var json = doc.ToJson();
        Assert.That(json, Does.Contain("_id"));
        Assert.That(json, Does.Contain(id));
        Assert.That(json, Does.Contain("$or"));
        Assert.That(json, Does.Contain("ExternalId"));
        Assert.That(json, Does.Contain("$regularExpression"));
        Assert.That(json, Does.Contain("null"));
    }

    [Test]
    public void EmptyExternalIdFilter_NeverTouchesOtherFields()
    {
        var doc = Render(MusicExternalIdWriter.EmptyExternalIdFilter(ObjectId.GenerateNewId().ToString()));

        Assert.That(doc.ToJson(), Does.Not.Contain("Title"));
    }
}
