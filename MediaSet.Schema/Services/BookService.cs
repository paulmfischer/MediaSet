using MediaSet.Data.Models;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;

namespace MediaSet.Data.Services
{
    public class BookService : IBookService
    {
        private readonly IMediaSetDbContext dbContext;

        public BookService(IMediaSetDbContext context)
        {
            dbContext = context;
        }
        
        private IQueryable<Book> Books()
        {
            return dbContext.Books
                .Include(b => b.Media)
                .Include(b => b.Publisher);
        }

        public IEnumerable<Book> GetAll()
        {
            return Books().AsNoTracking().AsEnumerable();
        }

        public Book Add(Book book)
        {
            dbContext.Books.Add(book);
            dbContext.SaveChanges();

            return book;
        }

        public Book Get(int bookId)
        {
            return Books().FirstOrDefault(b => b.Id.Equals(bookId));
        }

        public Book Update(Book book)
        {
            dbContext.Entry(book.Media).State = EntityState.Modified;
            dbContext.Entry(book).State = EntityState.Modified;
            dbContext.SaveChanges();

            return book;
        }

        public void Delete(int bookId)
        {
            var book = Get(bookId);
            dbContext.Remove(book);
            dbContext.Remove(book.Media);
            dbContext.SaveChanges();
        }
    }
}
