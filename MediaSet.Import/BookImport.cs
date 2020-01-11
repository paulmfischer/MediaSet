using MediaSet.Data;
using MediaSet.Data.BookData;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MediaSet.Import
{
    public static class BookImport
    {
        public static void Import(IList<string> dataRows)
        {
            var title = "Title";
            var subTitle = "Sub Title";
            var formatTitle = "Format";
            var authorTitle = "Author";
            var genreTitle = "Genre";
            var publicaitonYear = "Publication Year";
            var publisherTitle = "Publisher";
            var deweyTitle = "Dewey";
            var isbn = "ISBN";
            // var lengthTitle = "Length";
            var numPagesTitle = "No. of Pages";
            var plot = "Plot";
            // var pubDate = "Publication Date";
            int BookMediaType = 1;

            var fields = dataRows[0].Split(";").Select((v, i) => new { Key = v, Value = i }).ToDictionary(o => o.Key, o => o.Value);

            IDictionary<string, Genre> genres = new Dictionary<string, Genre>();
            IDictionary<string, Format> formats = new Dictionary<string, Format>();
            IDictionary<string, Publisher> publishers = new Dictionary<string, Publisher>();
            IDictionary<string, Author> authors = new Dictionary<string, Author>();


            foreach (var bookData in dataRows.Skip(1))
            {
                var bookProperties = bookData.Split(";");

                var movieGenre = bookProperties[fields[genreTitle]].Trim();
                if (!string.IsNullOrWhiteSpace(movieGenre))
                {
                    foreach (var gen in movieGenre.Split(","))
                    {
                        var g = gen.Trim();
                        genres.AddIfDoesNotExist(g, () => new Genre { Name = g, MediaTypeId = BookMediaType });
                    }
                }

                var formatName = bookProperties[fields[formatTitle]].Trim();
                formats.AddIfDoesNotExist(formatName, () => new Format { Name = formatName, MediaTypeId = BookMediaType });

                var bookAuthors = bookProperties[fields[authorTitle]].Trim();
                if (!string.IsNullOrWhiteSpace(bookAuthors))
                {
                    foreach (var auth in bookAuthors.Split(","))
                    {
                        authors.AddIfDoesNotExist(auth.Trim());
                    }
                }

                var bookPublishers = bookProperties[fields[publisherTitle]].Trim();
                if (!string.IsNullOrWhiteSpace(bookPublishers))
                {
                    foreach (var pub in bookPublishers.Split(","))
                    {
                        publishers.AddIfDoesNotExist(pub.Trim(), () => new Publisher { Name = pub.Trim(), MediaTypeId = BookMediaType });
                    }
                }
            }

            using (var context = new MediaSetContext())
            {
                context.AddRange(formats.Select(x => x.Value));
                context.AddRange(genres.Select(x => x.Value));
                context.AddRange(authors.Select(x => x.Value));
                context.AddRange(publishers.Select(x => x.Value));

                context.SaveChanges();
            }

            IList<Book> books = new List<Book>();

            foreach (var bookData in dataRows.Skip(1))
            {
                var bookProperties = bookData.Split(";");
                Format format;
                Publisher publisher;
                using (var context = new MediaSetContext())
                {
                    publisher = context.Publishers.FirstOrDefault(x => x.Name == bookProperties[fields[publisherTitle]].Trim());
                    format = context.Formats.FirstOrDefault(x => x.Name == bookProperties[fields[formatTitle]].Trim());

                    var media = new Media
                    {
                        Title = bookProperties[fields[title]],
                        MediaTypeId = BookMediaType
                    };

                    if (format != null)
                    {
                        media.Format = format;
                        media.FormatId = format.Id;
                    }

                    var book = new Book
                    {
                        Media = media,
                        ISBN = bookProperties[fields[isbn]],
                        Plot = bookProperties[fields[plot]],
                        SubTitle = bookProperties[fields[subTitle]],
                        PublicationDate = bookProperties[fields[publicaitonYear]]
                    };
                    book.Dewey = bookProperties[fields[deweyTitle]];
                    
                    var numPages = bookProperties[fields[numPagesTitle]];
                    if (!string.IsNullOrWhiteSpace(numPages))
                    {
                        book.NumberOfPages = int.Parse(numPages);
                    }

                    media.Book = book;

                    var bookGenres = bookProperties[fields[genreTitle]].Trim();
                    if (!string.IsNullOrWhiteSpace(bookGenres))
                    {
                        media.MediaGenres = new List<MediaGenre>();
                        foreach (var gen in bookGenres.Split(","))
                        {
                            var genre = context.Genres.FirstOrDefault(x => x.Name == gen.Trim());
                            media.MediaGenres.Add(new MediaGenre { Genre = genre, GenreId = genre.Id, Media = book.Media, MediaId = book.Media.Id });
                        }
                    }

                    var bookAuthors = bookProperties[fields[authorTitle]].Trim();
                    if (!string.IsNullOrWhiteSpace(bookAuthors))
                    {
                        book.BookAuthors = new List<BookAuthor>();
                        foreach (var auth in bookAuthors.Split(","))
                        {
                            var dbAuthor = context.Authors.FirstOrDefault(x => x.Name == auth.Trim());
                            if (!book.BookAuthors.Any(x => x.AuthorId == dbAuthor.Id))
                            {
                                book.BookAuthors.Add(new BookAuthor { Author = dbAuthor, AuthorId = dbAuthor.Id, Book = book, BookId = book.Id });
                            }
                        }
                    }

                    var bookPublishers = bookProperties[fields[publisherTitle]].Trim();
                    if (!string.IsNullOrWhiteSpace(bookPublishers))
                    {
                        book.BookPublishers = new List<BookPublisher>();
                        foreach (var pub in bookPublishers.Split(","))
                        {
                            if (!string.IsNullOrWhiteSpace(pub))
                            {
                                var dbPublisher = context.Publishers.FirstOrDefault(x => x.Name == pub.Trim());
                                book.BookPublishers.Add(new BookPublisher { Publisher = dbPublisher, PublisherId = dbPublisher.Id, Book = book, BookId = book.Id });
                            }
                        }
                    }

                    context.Books.Add(book);
                    context.SaveChanges();
                }
            }
        }
    }
}
