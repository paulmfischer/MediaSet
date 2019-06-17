using MediaSet.Data.Models;
using MediaSet.Data.Services;
using MediaSet.Server.MappingService;
using MediaSet.Shared.ViewModels;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;

namespace MediaSet.Server.Controllers
{
    [Route("api/[controller]")]
    public class BookController : Controller
    {
        private IBookService BookService { get; set; }
        private BookMappingService BookMappingService { get; set; }

        public BookController(IBookService bookService, BookMappingService bookMappingService)
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
                //    new BookViewModel
                //{
                //    Id = book.Id,
                //    MediaId = book.Media.Id,
                //    ISBN = book.Media.ISBN,
                //    NumberOfPages = book.NumberOfPages,
                //    Title = book.Media.Title
                //});
            }

            return viewBooks;
        }

        [HttpGet("[action]/{Id}")]
        public EditBookViewModel Detail(int Id)
        {
            var book = BookService.Get(Id);
            var viewBook = BookMappingService.MapToEditBook(book);
            //var viewBook = new EditBookViewModel()
            //{
            //    Id = book.Id,
            //    MediaId = book.Media.Id,
            //    ISBN = book.Media.ISBN,
            //    NumberOfPages = book.NumberOfPages,
            //    Title = book.Media.Title
            //};

            return viewBook;
        }

        [HttpPost("[action]")]
        public Book Add([FromBody] AddBookViewModel newBook)
        {
            //var book = new Book
            //{
            //    Media = new Media
            //    {
            //        Title = newBook.Title,
            //        ISBN = newBook.ISBN,
            //        MediaType = MediaType.Book
            //    },
            //    NumberOfPages = newBook.NumberOfPages,
            //    SubTitle = newBook.SubTitle
            //};
            var book = BookMappingService.MapToNewBook(newBook);
            book = BookService.Add(book);

            return book;
        }

        [HttpPost("[action]")]
        public Book Update([FromBody] EditBookViewModel editBook)
        {
            //var book = new Book
            //{
            //    Id = editBook.Id,
            //    Media = new Media
            //    {
            //        Id = editBook.MediaId,
            //        Title = editBook.Title,
            //        ISBN = editBook.ISBN
            //    },
            //    NumberOfPages = editBook.NumberOfPages,
            //    SubTitle = editBook.SubTitle
            //};
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
