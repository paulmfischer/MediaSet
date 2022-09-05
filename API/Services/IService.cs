using Models;

namespace Services;

public interface IService<T, Y> where T : IModel where Y : IModel
{
    IEnumerable<T> GetList();

    Task<T?> Get(int id);

    Task<T> Create(Y data);

    Task<T> Update(T data);

    Task Delete(int id);
}