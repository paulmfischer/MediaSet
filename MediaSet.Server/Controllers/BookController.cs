using MediaSet.Data.Models;
using MediaSet.Data.Repositories;
using MediaSet.Server.MappingService;
using MediaSet.Shared.ViewModels;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;

namespace MediaSet.Server.Controllers
{
    [Route("api/[controller]")]
    public class BookController : Controller
    {
        private IBookRepository BookService { get; set; }
        private BookMappingService BookMappingService { get; set; }

        public BookController(IBookRepository bookService, BookMappingService bookMappingService)
        {
            BookService = bookService;
            BookMappingService = bookMappingService;
        }

        [HttpGet("[action]")]
        public IList<BookViewModel> List()
        {
            var books = BookService.GetAll();
            var viewBooks = new List<BookViewModel>();

            foreach (var book in books)
            {
                viewBooks.Add(BookMappingService.MapToViewBook(book));
            }

            return viewBooks;
        }

        [HttpGet("[action]/{Id}")]
        public EditBookViewModel Detail(int Id)
        {
            var book = BookService.Get(Id);
            var viewBook = BookMappingService.MapToEditBook(book);

            return viewBook;
        }

        [HttpPost("[action]")]
        public Book Add([FromBody] AddBookViewModel newBook)
        {
            var book = BookMappingService.MapToNewBook(newBook);
            book = BookService.Add(book);

            return book;
        }

        [HttpPost("[action]")]
        public Book Update([FromBody] EditBookViewModel editBook)
        {
            var book = BookMappingService.MapEditToSaveBook(editBook);
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
