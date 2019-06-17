using MediaSet.Data.Models;
using MediaSet.Shared.ViewModels;

namespace MediaSet.Server.MappingService
{
    public class BookMappingService
    {
        public BookViewModel MapToViewBook(Book book)
        {
            return new BookViewModel
            {
                Id = book.Id,
                MediaId = book.Media.Id,
                ISBN = book.Media.ISBN,
                NumberOfPages = book.NumberOfPages,
                Title = book.Media.Title
            };
        }

        public Book MapToNewBook(AddBookViewModel newBook)
        {
            var book = new Book
            {
                Media = new Media
                {
                    Title = newBook.Title,
                    ISBN = newBook.ISBN,
                    MediaType = MediaType.Book
                },
                NumberOfPages = newBook.NumberOfPages,
                SubTitle = newBook.SubTitle
            };

            return book;
        }

        public EditBookViewModel MapToEditBook(Book book)
        {
            var viewBook = new EditBookViewModel()
            {
                Id = book.Id,
                MediaId = book.Media.Id,
                ISBN = book.Media.ISBN,
                NumberOfPages = book.NumberOfPages,
                Title = book.Media.Title
            };

            return viewBook;
        }

        public Book MapEditToSaveBook(EditBookViewModel editBook)
        {
            var book = new Book
            {
                Id = editBook.Id,
                Media = new Media
                {
                    Id = editBook.MediaId,
                    Title = editBook.Title,
                    ISBN = editBook.ISBN
                },
                NumberOfPages = editBook.NumberOfPages,
                SubTitle = editBook.SubTitle
            };

            return book;
        }
    }
}
