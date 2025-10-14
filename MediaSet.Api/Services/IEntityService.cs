using MediaSet.Api.Models;
using MongoDB.Driver;

namespace MediaSet.Api.Services;

public interface IEntityService<TEntity> where TEntity : IEntity
{
  Task<IEnumerable<TEntity>> SearchAsync(string searchText, string orderBy);
  Task<IEnumerable<TEntity>> GetListAsync();
  Task<TEntity?> GetAsync(string id);
  Task CreateAsync(TEntity newEntity);
  Task<ReplaceOneResult> UpdateAsync(string id, TEntity updatedEntity);
  Task<DeleteResult> RemoveAsync(string id);
  Task BulkCreateAsync(IEnumerable<TEntity> newEntities);
}
