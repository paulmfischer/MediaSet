using Entities;

namespace Repositories;

public interface IRepository<T> where T : IEntity
{
    IEnumerable<T> GetList();

    Task<T?> Get(int id);

    Task<T> Create(T data);

    Task<T> Update(T data);

    Task Delete(int id);
}