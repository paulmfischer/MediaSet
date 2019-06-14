using MediaSet.Schema;
using MediaSet.Schema.Models;
using MediaSet.Shared;
using MediaSet.Shared.ViewModels;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MediaSet.Server.Controllers
{
    [Route("api/[controller]")]
    public class SampleDataController : Controller
    {
        private static string[] Summaries = new[]
        {
            "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
        };
        private IBookService BookService { get; set; }

        public SampleDataController(IBookService _bookService)
        {
            BookService = _bookService;
        }

        [HttpGet("[action]")]
        public IEnumerable<WeatherForecast> WeatherForecasts()
        {
            var rng = new Random();
            return Enumerable.Range(1, 5).Select(index => new WeatherForecast
            {
                Date = DateTime.Now.AddDays(index),
                TemperatureC = rng.Next(-20, 55),
                Summary = Summaries[rng.Next(Summaries.Length)]
            });
        }

        [HttpGet("[action]")]
        public IList<BookViewModel> Books()
        {
            var books = BookService.GetBooks();
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
        public EditBookViewModel Book(int Id)
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
