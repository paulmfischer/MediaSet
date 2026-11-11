using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace MediaSet.Api.Infrastructure.Database;

public class DatabaseService : IDatabaseService
{
    private readonly IMongoDatabase _mongoDatabase;

    public DatabaseService(IOptions<MediaSetDatabaseSettings> mediaSetDatabaseSettings)
    {
        var dbSettings = mediaSetDatabaseSettings.Value;
        var mongoClient = new MongoClient(dbSettings.ConnectionString);
        _mongoDatabase = mongoClient.GetDatabase(dbSettings.DatabaseName);
    }

    public virtual IMongoCollection<TEntity> GetCollection<TEntity>() => _mongoDatabase.GetCollection<TEntity>($"{typeof(TEntity).Name}s");

    public virtual IMongoCollection<TEntity> GetCollection<TEntity>(string collectionName) => _mongoDatabase.GetCollection<TEntity>(collectionName);
}
