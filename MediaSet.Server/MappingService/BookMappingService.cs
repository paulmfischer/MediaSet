using MediaSet.Data.Models;
using MediaSet.Shared.ViewModels;
using System;

namespace MediaSet.Server.MappingService
{
    public class BookMappingService
    {
        public BookViewModel MapToViewBook(Book book)
        {
            var publisher = book.Publisher != null ?
                new PublisherViewModel { Id = book.Publisher.Id, Name = book.Publisher.Name } : null;

            return new BookViewModel
            {
                Id = book.Id,
                MediaId = book.Media.Id,
                ISBN = book.Media.ISBN,
                NumberOfPages = book.NumberOfPages,
                Title = book.Media.Title,
                SortTitle = book.Media.SortTitle,
                SubTitle = book.SubTitle,
                PublisherViewModel = publisher
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
                    MediaType = MediaType.Book,
                    SortTitle = newBook.SortTitle
                },
                NumberOfPages = newBook.NumberOfPages,
                SubTitle = newBook.SubTitle
            };

            if (newBook.PublisherId.HasValue)
            {
                book.PublisherId = newBook.PublisherId;
            } else if (newBook.PublisherViewModel != null)
            {
                book.Publisher = new Publisher {
                    Name = newBook.PublisherViewModel.Name,
                    MediaType = MediaType.Book
                };
            }

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
                Title = book.Media.Title,
                MediaTypeId = (int)book.Media.MediaType,
                SubTitle = book.SubTitle,
                SortTitle = book.Media.SortTitle
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
                    ISBN = editBook.ISBN,
                    MediaType = (MediaType)editBook.MediaTypeId,
                    SortTitle = editBook.SortTitle
                },
                NumberOfPages = editBook.NumberOfPages,
                SubTitle = editBook.SubTitle
            };

            return book;
        }
    }
}
