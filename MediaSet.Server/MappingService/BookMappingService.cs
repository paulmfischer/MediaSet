using MediaSet.Data.Models;
using MediaSet.Shared.ViewModels;
using System.Collections.Generic;

namespace MediaSet.Server.MappingService
{
    public class BookMappingService
    {
        public BookViewModel MapToViewBook(Book book)
        {
            var publisher = book.Publisher != null ?
                new PublisherViewModel { Id = book.Publisher.Id, Name = book.Publisher.Name } : null;

            var format = book.Media.Format != null ?
                new FormatViewModel { Id = book.Media.Format.Id, Name = book.Media.Format.Name } : null;

            IList<AuthorViewModel> authors = null;

            if (book?.BookAuthors?.Count > 0)
            {
                authors = new List<AuthorViewModel>();
                foreach (var author in book.BookAuthors)
                {
                    authors.Add(new AuthorViewModel
                    {
                        Id = author.AuthorId,
                        Name = author.Author.Name,
                        SortName = author.Author.SortName
                    });
                }
            }

            return new BookViewModel
            {
                Id = book.Id,
                MediaId = book.Media.Id,
                ISBN = book.Media.ISBN,
                NumberOfPages = book.NumberOfPages,
                Title = book.Media.Title,
                SortTitle = book.Media.SortTitle,
                SubTitle = book.SubTitle,
                Publisher = publisher,
                Format = format,
                Authors = authors
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
            }
            else if (newBook.Publisher != null)
            {
                book.Publisher = new Publisher {
                    Name = newBook.Publisher.Name,
                    MediaType = MediaType.Book
                };
            }

            if (newBook.FormatId.HasValue)
            {
                book.Media.FormatId = newBook.FormatId;
            }
            else if (newBook.Format != null)
            {
                book.Media.Format = new Format
                {
                    Name = newBook.Format.Name,
                    MediaType = MediaType.Book
                };
            }

            if (newBook.Authors.Count > 0)
            {
                book.BookAuthors = new List<BookAuthor>();
                foreach (var auth in newBook.Authors)
                {
                    var bookAuth = new BookAuthor { Book = book };
                    if (auth.Id.HasValue)
                    {
                        bookAuth.AuthorId = auth.Id.Value;
                    }
                    else
                    {
                        bookAuth.Author = new Author { Name = auth.Name, SortName = auth.SortName };
                    }
                    book.BookAuthors.Add(bookAuth);
                }
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

            if (book.Publisher != null)
            {
                viewBook.Publisher = new PublisherViewModel
                {
                    Id = book.Publisher.Id,
                    Name = book.Publisher.Name
                };
            }

            if (book.Media.Format != null)
            {
                viewBook.Format = new FormatViewModel
                {
                    Id = book.Media.Format.Id,
                    Name = book.Media.Format.Name
                };
            }

            if (book?.BookAuthors?.Count > 0)
            {
                IList<AuthorViewModel> authors = new List<AuthorViewModel>();
                foreach (var author in book.BookAuthors)
                {
                    authors.Add(new AuthorViewModel
                    {
                        Id = author.AuthorId,
                        Name = author.Author.Name,
                        SortName = author.Author.SortName
                    });
                }

                viewBook.Authors = authors;
            }

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
                    SortTitle = editBook.SortTitle,
                    FormatId = editBook.FormatId
                },
                PublisherId = editBook.PublisherId,
                NumberOfPages = editBook.NumberOfPages,
                SubTitle = editBook.SubTitle
            };

            if (editBook.Publisher != null)
            {
                book.Publisher = new Publisher
                {
                    Name = editBook.Publisher.Name,
                    MediaType = MediaType.Book
                };
            }

            if (editBook.Format != null)
            {
                book.Media.Format = new Format
                {
                    Name = editBook.Format.Name,
                    MediaType = MediaType.Book
                };
            }

            return book;
        }
    }
}
