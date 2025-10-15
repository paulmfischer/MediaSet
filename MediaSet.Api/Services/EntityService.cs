using System.Linq.Expressions;
using MediaSet.Api.Helpers;
using MediaSet.Api.Models;
using MongoDB.Driver;

namespace MediaSet.Api.Services;

public class EntityService<TEntity> : IEntityService<TEntity> where TEntity : IEntity
{
  private readonly IMongoCollection<TEntity> entityCollection;

  public EntityService(IDatabaseService databaseService)
  {
    entityCollection = databaseService.GetCollection<TEntity>();
  }

  public async Task<IEnumerable<TEntity>> SearchAsync(string searchText, string orderBy)
  {
    string orderByField = "";
    bool orderByAscending = true;
    if (!string.IsNullOrWhiteSpace(orderBy))
    {
      var orderByArgs = orderBy.Split(":");
      orderByField = orderByArgs[0];
      orderByAscending = string.IsNullOrWhiteSpace(orderByArgs[1]) || orderByArgs[1].ToLower().Equals("asc");
    }
    var entitySearch = entityCollection.Find(entity => entity.Title.ToLower().Contains(searchText.ToLower()));
    Expression<Func<TEntity, object>> sortFn = entity => entity.Title;

    if (orderByAscending)
    {
      entitySearch.SortBy(sortFn);
    }
    else
    {
      entitySearch.SortByDescending(sortFn);
    }
    return (await entitySearch.ToListAsync()).Select(entity => entity.SetType());
  }

  public async Task<IEnumerable<TEntity>> GetListAsync() => (await entityCollection.Find(_ => true).SortBy(entity => entity.Title).ToListAsync()).Select(entity => entity.SetType());

  public async Task<TEntity?> GetAsync(string id) => (await entityCollection.Find(x => x.Id == id).FirstOrDefaultAsync()).SetType();

  public Task CreateAsync(TEntity newEntity) => entityCollection.InsertOneAsync(newEntity);

  public Task<ReplaceOneResult> UpdateAsync(string id, TEntity updatedEntity) => entityCollection.ReplaceOneAsync(x => x.Id == id, updatedEntity);

  public Task<DeleteResult> RemoveAsync(string id) => entityCollection.DeleteOneAsync(x => x.Id == id);

  public Task BulkCreateAsync(IEnumerable<TEntity> newEntities) => entityCollection.InsertManyAsync(newEntities);
}
