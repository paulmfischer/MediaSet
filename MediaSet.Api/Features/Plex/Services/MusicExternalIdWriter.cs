using MediaSet.Api.Infrastructure.Caching;
using MediaSet.Api.Infrastructure.Database;
using MediaSet.Api.Shared.Models;
using MongoDB.Bson;
using MongoDB.Driver;

namespace MediaSet.Api.Features.Plex.Services;

public interface IMusicExternalIdWriter
{
    /// <summary>
    /// Sets the album's ExternalId, touching no other field, only while the ExternalId is still missing, null or blank.
    /// False when the album is gone or already has an id.
    /// </summary>
    Task<bool> SetIfEmptyAsync(string musicId, string externalId, CancellationToken cancellationToken);
}

public class MusicExternalIdWriter(IDatabaseService database, ICacheService cache) : IMusicExternalIdWriter
{
    /// <summary>The album with this id and an ExternalId that is missing, null, empty or whitespace.</summary>
    public static FilterDefinition<Music> EmptyExternalIdFilter(string musicId)
    {
        var f = Builders<Music>.Filter;
        return f.Eq(m => m.Id, musicId)
            & (f.Eq(m => m.ExternalId, null) | f.Regex(m => m.ExternalId, new BsonRegularExpression("^\\s*$")));
    }

    /// <summary>The update: sets ExternalId and nothing else.</summary>
    public static UpdateDefinition<Music> SetExternalIdUpdate(string externalId) =>
        Builders<Music>.Update.Set(m => m.ExternalId, externalId);

    public async Task<bool> SetIfEmptyAsync(string musicId, string externalId, CancellationToken cancellationToken)
    {
        var result = await database.GetCollection<Music>().UpdateOneAsync(
            EmptyExternalIdFilter(musicId),
            SetExternalIdUpdate(externalId),
            cancellationToken: cancellationToken);
        if (result.ModifiedCount == 0)
        {
            return false;
        }

        // Same invalidation as EntityService.UpdateAsync, which this write bypasses
        await cache.RemoveByPatternAsync($"metadata:{MediaTypes.Musics}:*");
        await cache.RemoveAsync("stats");
        return true;
    }
}
