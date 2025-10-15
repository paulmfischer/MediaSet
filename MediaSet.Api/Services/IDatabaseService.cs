using MongoDB.Driver;

namespace MediaSet.Api.Services;

public interface IDatabaseService
{
  IMongoCollection<TEntity> GetCollection<TEntity>();
}
