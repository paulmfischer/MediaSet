using MediaSet.Api.Models;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace MediaSet.Api.Services;

public class DatabaseService
{
  private readonly IMongoDatabase mongoDatabase;

  public DatabaseService(IOptions<MediaSetDatabaseSettings> mediaSetDatabaseSettings)
  {
    var dbSettings = mediaSetDatabaseSettings.Value;
    var mongoClient = new MongoClient(dbSettings.ConnectionString);
    mongoDatabase = mongoClient.GetDatabase(dbSettings.DatabaseName);
  }
  
  public IMongoCollection<TEntity> GetCollection<TEntity>() => mongoDatabase.GetCollection<TEntity>($"{typeof(TEntity).Name}s");
}