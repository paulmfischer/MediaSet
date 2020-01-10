using MediaSet.Data.BookData;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace MediaSet.Data.Repositories
{
    public class BooksRepository : IBooksRepository
    {
        private readonly IMediaSetContext context;

        public BooksRepository(IMediaSetContext context)
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
                    .Include(book => book.BookAuthors)
                    .Include(book => book.BookPublishers)
                    .ToListAsync();
        }

        public async Task<Book> GetById(int id)
        {
            return await this.context.Books.FirstOrDefaultAsync(book => book.Id == id);
        }
    }
}
