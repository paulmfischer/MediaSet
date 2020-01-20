using MediaSet.Data.BookData;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Threading.Tasks;

namespace MediaSet.Data.Repositories
{
    public class BooksRepository : BaseRepository<Book>, IBooksRepository
    {
        private readonly MediaSetContext context;

        public BooksRepository(MediaSetContext context) : base(context)
        {
            this.context = context;
        }

        //public async Task<IList<Book>> GetAll()
        //{
        //    return await this.GetBaseQuery().ToListAsync();
        //}

        public override IQueryable<Book> GetBaseQuery()
        {
            return this.context.Books
                .Include(book => book.Media)
                        .ThenInclude(media => media.Format)
                    .Include(book => book.Media)
                        .ThenInclude(media => media.MediaGenres)
                            .ThenInclude(mg => mg.Genre)
                    .Include(book => book.BookAuthors)
                        .ThenInclude(bookauthor => bookauthor.Author)
                    .Include(book => book.BookPublishers)
                        .ThenInclude(bookPublisher => bookPublisher.Publisher);
        }

        public override Task<int> GetTotalEntities()
        {
            return this.context.Books.CountAsync();
        }

        //public async Task<Book> GetById(int id)
        //{
        //    return await this.GetBaseQuery().FirstOrDefaultAsync(book => book.Id == id);
        //}

        //public async Task<PagedResult<Book>> Paged(int skip, int take)
        //{
        //    return new PagedResult<Book>
        //    {
        //        Items = await this.GetBaseQuery().Skip(skip).Take(take).ToListAsync(),
        //        Total = await this.context.Books.CountAsync()
        //    };
        //}

        //private IQueryable<Book> GetBaseQuery()
        //{
        //    return this.context.Books
        //        .Include(book => book.Media)
        //                .ThenInclude(media => media.Format)
        //            .Include(book => book.Media)
        //                .ThenInclude(media => media.MediaGenres)
        //                    .ThenInclude(mg => mg.Genre)
        //            .Include(book => book.BookAuthors)
        //                .ThenInclude(bookauthor => bookauthor.Author)
        //            .Include(book => book.BookPublishers)
        //                .ThenInclude(bookPublisher => bookPublisher.Publisher);
        //}
    }
}
