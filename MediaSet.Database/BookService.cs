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
    }

    public class BookService : IBookService
    {
        private IMediaSetDbContext dbContext;

        private IMediaSetDbContext GetDbContext()
        {
            return dbContext;
        }

        private void SetDbContext(IMediaSetDbContext value)
        {
            dbContext = value;
        }

        public BookService()
        {
            SetDbContext(new MediaSetDbContext());
        }

        public IEnumerable<Book> GetBooks()
        {
            //using (var context = new MediaSetDbContext())
            //{
            //    return context.Books.AsNoTracking().ToList();
            //}

            return GetDbContext().Books.AsNoTracking().AsEnumerable(); //.ToList();
        }

    }
}
