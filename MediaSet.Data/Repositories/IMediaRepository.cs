using System.Collections.Generic;
using System.Threading.Tasks;

namespace MediaSet.Data.Repositories
{
    public interface IMediaRepository<T>
    {
        Task<IList<T>> GetAll();

        Task<T> GetById(int id);
    }
}
