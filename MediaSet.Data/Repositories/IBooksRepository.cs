using MediaSet.Data.BookData;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace MediaSet.Data.Repositories
{
    public interface IBooksRepository
    {
        Task<IList<Book>> GetAll();

        Task<Book> GetById(int id);
    }
}
