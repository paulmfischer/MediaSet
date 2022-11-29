namespace API.Data;

public interface IEntityService<T>
{
    Task<List<T>> GetAll();
    Task<Movie?> GetById(int Id);
}