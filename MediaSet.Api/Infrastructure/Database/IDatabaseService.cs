using MongoDB.Driver;

namespace MediaSet.Api.Infrastructure.Database;

public interface IDatabaseService
{
    IMongoCollection<TEntity> GetCollection<TEntity>();
}
