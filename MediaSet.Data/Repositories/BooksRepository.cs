using MediaSet.Data.BookData;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MediaSet.Data.Repositories
{
    public class BooksRepository : IBooksRepository
    {
        private readonly MediaSetContext context;

        public BooksRepository(MediaSetContext context)
        {
            this.context = context;
        }

        public async Task<IList<Book>> GetAll()
        {
            return await this.context.Books
                    .Include(book => book.Media)
                        .ThenInclude(media => media.Format)
                    .Include(book => book.Media)
                        .ThenInclude(media => media.MediaGenres)
                            .ThenInclude(mg => mg.Genre)
                    .Include(book => book.BookAuthors)
                        .ThenInclude(bookauthor => bookauthor.Author)
                    .Include(book => book.BookPublishers)
                        .ThenInclude(bookPublisher => bookPublisher.Publisher)
                    .Take(20)
                    .ToListAsync();
        }

        public async Task<Book> GetById(int id)
        {
            return await this.context.Books
                .Include(book => book.Media)
                        .ThenInclude(media => media.Format)
                    .Include(book => book.Media)
                        .ThenInclude(media => media.MediaGenres)
                            .ThenInclude(mg => mg.Genre)
                    .Include(book => book.BookAuthors)
                        .ThenInclude(bookauthor => bookauthor.Author)
                    .Include(book => book.BookPublishers)
                        .ThenInclude(bookPublisher => bookPublisher.Publisher)
                .FirstOrDefaultAsync(book => book.Id == id);
        }
    }
}
