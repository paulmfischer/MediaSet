using MongoDB.Driver;

namespace MediaSet.Api.Infrastructure.Database;

public interface IDatabaseService
{
    IMongoCollection<TEntity> GetCollection<TEntity>();

    /// <summary>A collection with an explicit name, for types whose default name (type name plus s) does not fit.</summary>
    IMongoCollection<TEntity> GetCollection<TEntity>(string collectionName);
}
