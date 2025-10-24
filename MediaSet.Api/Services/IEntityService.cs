using MediaSet.Api.Models;
using MongoDB.Driver;

namespace MediaSet.Api.Services;

public interface IEntityService<TEntity> where TEntity : IEntity
{
    Task<IEnumerable<TEntity>> SearchAsync(string searchText, string orderBy, CancellationToken cancellationToken = default);
    Task<IEnumerable<TEntity>> GetListAsync(CancellationToken cancellationToken = default);
    Task<TEntity?> GetAsync(string id, CancellationToken cancellationToken = default);
    Task CreateAsync(TEntity newEntity, CancellationToken cancellationToken = default);
    Task<ReplaceOneResult> UpdateAsync(string id, TEntity updatedEntity, CancellationToken cancellationToken = default);
    Task<DeleteResult> RemoveAsync(string id, CancellationToken cancellationToken = default);
    Task BulkCreateAsync(IEnumerable<TEntity> newEntities, CancellationToken cancellationToken = default);
}
