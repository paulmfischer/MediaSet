using MediaSet.Data.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MediaSet.Data
{
    public interface IBookService
    {
        IEnumerable<Book> GetBooks();

        Book Get(int bookId);

        Book Add(Book book);

        Book Update(Book book);

        void Delete(int bookId);
    }

    public class BookService : IBookService
    {
        private readonly MediaSetDbContext dbContext;

        public BookService(MediaSetDbContext context)
        {
            dbContext = context;
        }

        public IEnumerable<Book> GetBooks()
        {
            return dbContext.Books.AsNoTracking().AsEnumerable();
        }

        public Book Add(Book book)
        {
            dbContext.Books.Add(book);
            dbContext.SaveChanges();

            return book;
        }

        public Book Get(int bookId)
        {
            return dbContext.Books.Find(bookId);
        }

        public Book Update(Book book)
        {
            dbContext.Entry(book).State = EntityState.Modified;
            dbContext.SaveChanges();

            return book;
        }

        public void Delete(int bookId)
        {
            var book = new Book { Id = bookId };
            dbContext.Remove(book);
            dbContext.SaveChanges();
        }
    }
}
