using MediaSet.Data.Models;
using MediaSet.Data.Services;
using MediaSet.Shared.ViewModels;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;

namespace MediaSet.Server.Controllers
{
    [Route("api/[controller]")]
    public class BookController : Controller
    {
        private IBookService BookService { get; set; }

        public BookController(IBookService _bookService)
        {
            BookService = _bookService;
        }

        [HttpGet("[action]")]
        public IList<BookViewModel> List()
        {
            var books = BookService.GetAll();
            var viewBooks = new List<BookViewModel>();

            foreach (var book in books)
            {
                viewBooks.Add(new BookViewModel
                {
                    Id = book.Id,
                    ISBN = book.ISBN,
                    NumberOfPages = book.NumberOfPages,
                    Title = book.Title
                });
            }

            return viewBooks;
        }

        [HttpGet("[action]/{Id}")]
        public EditBookViewModel Detail(int Id)
        {
            var book = BookService.Get(Id);
            var viewBook = new EditBookViewModel()
            {
                Id = book.Id,
                ISBN = book.ISBN,
                NumberOfPages = book.NumberOfPages,
                Title = book.Title
            };

            return viewBook;
        }

        [HttpPost("[action]")]
        public Book Add([FromBody] AddBookViewModel newBook)
        {
            var book = new Book
            {
                Title = newBook.Title,
                ISBN = newBook.ISBN,
                NumberOfPages = newBook.NumberOfPages,
                SubTitle = newBook.SubTitle
            };
            book = BookService.Add(book);

            return book;
        }

        [HttpPost("[action]")]
        public Book Update([FromBody] EditBookViewModel editBook)
        {
            var book = new Book
            {
                Id = editBook.Id,
                Title = editBook.Title,
                ISBN = editBook.ISBN,
                NumberOfPages = editBook.NumberOfPages,
                SubTitle = editBook.SubTitle
            };
            book = BookService.Update(book);

            return book;
        }

        [HttpDelete("[action]/{Id}")]
        public void Delete(int Id)
        {
            BookService.Delete(Id);
        }
    }
}
