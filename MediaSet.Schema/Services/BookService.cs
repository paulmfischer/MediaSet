﻿using MediaSet.Data.Models;
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

        public IEnumerable<Book> GetAll()
        {
            return dbContext.Books.Include(b => b.Media).AsNoTracking().AsEnumerable();
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
