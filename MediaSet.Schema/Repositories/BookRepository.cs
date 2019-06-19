using MediaSet.Data.Models;

namespace MediaSet.Data.Repositories
{
    public class BookRepository: GenericRepository<Book>, IBookRepository
    {
        public BookRepository(IMediaSetDbContext context) : base(context) { }
    }
}
