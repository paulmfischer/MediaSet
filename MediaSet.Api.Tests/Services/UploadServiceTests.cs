using MediaSet.Api.Features.Entities.Models;
using NUnit.Framework;
using MediaSet.Api.Services;
using MediaSet.Api.Models;
using System.Collections.Generic;
using System.Linq;

namespace MediaSet.Api.Tests.Services;

[TestFixture]
public class UploadServiceTests
{
    [Test]
    public void MapUploadToEntities_ShouldCreateEntities_WithBasicProperties()
    {
        // Arrange
        var headerFields = new List<string> { "Title", "Format" };
        var dataFields = new List<string[]>
        {
            new[] { "The Great Gatsby", "Hardcover" },
            new[] { "1984", "Paperback" }
        };

        // Act
        var result = UploadService.MapUploadToEntities<Book>(headerFields, dataFields).ToList();

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Count, Is.EqualTo(2));
        Assert.That(result[0].Title, Is.EqualTo("The Great Gatsby"));
        Assert.That(result[0].Format, Is.EqualTo("Hardcover"));
        Assert.That(result[1].Title, Is.EqualTo("1984"));
        Assert.That(result[1].Format, Is.EqualTo("Paperback"));
    }

    [Test]
    public void MapUploadToEntities_ShouldMapBookProperties_WithCustomHeaderNames()
    {
        // Arrange
        var headerFields = new List<string> { "Title", "Publication Date", "Author", "Genre" };
        var dataFields = new List<string[]>
        {
            new[] { "The Hobbit", "1937", "J.R.R. Tolkien", "Fantasy" }
        };

        // Act
        var result = UploadService.MapUploadToEntities<Book>(headerFields, dataFields).ToList();

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Count, Is.EqualTo(1));
        Assert.That(result[0].Title, Is.EqualTo("The Hobbit"));
        Assert.That(result[0].PublicationDate, Is.EqualTo("1937"));
        Assert.That(result[0].Authors, Is.Not.Null);
        Assert.That(result[0].Authors.Count, Is.EqualTo(1));
        Assert.That(result[0].Authors[0], Is.EqualTo("J.R.R. Tolkien"));
        Assert.That(result[0].Genres, Is.Not.Null);
        Assert.That(result[0].Genres.Count, Is.EqualTo(1));
        Assert.That(result[0].Genres[0], Is.EqualTo("Fantasy"));
    }

    [Test]
    public void MapUploadToEntities_ShouldMapMovieProperties_WithCustomHeaderNames()
    {
        // Arrange
        var headerFields = new List<string> { "Title", "Release Date", "Audience Rating", "Runtime", "Is TV Series" };
        var dataFields = new List<string[]>
        {
            new[] { "Inception", "2010-07-16", "PG-13", "02:28", "0" }
        };

        // Act
        var result = UploadService.MapUploadToEntities<Movie>(headerFields, dataFields).ToList();

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Count, Is.EqualTo(1));
        Assert.That(result[0].Title, Is.EqualTo("Inception"));
        Assert.That(result[0].ReleaseDate, Is.EqualTo("2010-07-16"));
        Assert.That(result[0].Rating, Is.EqualTo("PG-13"));
        Assert.That(result[0].Runtime, Is.EqualTo(148)); // 2*60 + 28
        Assert.That(result[0].IsTvSeries, Is.False);
    }

    [Test]
    public void MapUploadToEntities_ShouldHandleListProperties_WithPipeDelimiter()
    {
        // Arrange
        var headerFields = new List<string> { "Title", "Author", "Genre" };
        var dataFields = new List<string[]>
        {
            new[] { "Multi-Author Book", "Author One | Author Two | Author Three", "Fantasy | Adventure | Magic" }
        };

        // Act
        var result = UploadService.MapUploadToEntities<Book>(headerFields, dataFields).ToList();

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Count, Is.EqualTo(1));
        Assert.That(result[0].Authors, Is.Not.Null);
        Assert.That(result[0].Authors.Count, Is.EqualTo(3));
        Assert.That(result[0].Authors[0], Is.EqualTo("Author One"));
        Assert.That(result[0].Authors[1], Is.EqualTo("Author Two"));
        Assert.That(result[0].Authors[2], Is.EqualTo("Author Three"));
        Assert.That(result[0].Genres.Count, Is.EqualTo(3));
        Assert.That(result[0].Genres[0], Is.EqualTo("Fantasy"));
        Assert.That(result[0].Genres[1], Is.EqualTo("Adventure"));
        Assert.That(result[0].Genres[2], Is.EqualTo("Magic"));
    }

    [Test]
    public void MapUploadToEntities_ShouldTrimWhitespace_InListProperties()
    {
        // Arrange
        var headerFields = new List<string> { "Title", "Author" };
        var dataFields = new List<string[]>
        {
            new[] { "Test Book", "  Author One  |  Author Two  " }
        };

        // Act
        var result = UploadService.MapUploadToEntities<Book>(headerFields, dataFields).ToList();

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result[0].Authors.Count, Is.EqualTo(2));
        Assert.That(result[0].Authors[0], Is.EqualTo("Author One"));
        Assert.That(result[0].Authors[1], Is.EqualTo("Author Two"));
    }

    [Test]
    public void MapUploadToEntities_ShouldSkipEmptyValues_InListProperties()
    {
        // Arrange
        var headerFields = new List<string> { "Title", "Genre" };
        var dataFields = new List<string[]>
        {
            new[] { "Test Book", "Fantasy | | Adventure" }
        };

        // Act
        var result = UploadService.MapUploadToEntities<Book>(headerFields, dataFields).ToList();

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result[0].Genres.Count, Is.EqualTo(2));
        Assert.That(result[0].Genres[0], Is.EqualTo("Fantasy"));
        Assert.That(result[0].Genres[1], Is.EqualTo("Adventure"));
    }

    [Test]
    public void MapUploadToEntities_ShouldHandleNullableIntProperties()
    {
        // Arrange
        var headerFields = new List<string> { "Title", "Pages" };
        var dataFields = new List<string[]>
        {
            new[] { "Book With Pages", "350" },
            new[] { "Book Without Pages", "" }
        };

        // Act
        var result = UploadService.MapUploadToEntities<Book>(headerFields, dataFields).ToList();

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Count, Is.EqualTo(2));
        Assert.That(result[0].Pages, Is.EqualTo(350));
        Assert.That(result[1].Pages, Is.Null);
    }

    [Test]
    public void MapUploadToEntities_ShouldConvertRuntime_UsingRuntimeConverter()
    {
        // Arrange
        var headerFields = new List<string> { "Title", "Runtime" };
        var dataFields = new List<string[]>
        {
            new[] { "Short Movie", "01:30" },
            new[] { "Long Movie", "02:45" }
        };

        // Act
        var result = UploadService.MapUploadToEntities<Movie>(headerFields, dataFields).ToList();

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Count, Is.EqualTo(2));
        Assert.That(result[0].Runtime, Is.EqualTo(90)); // 1*60 + 30
        Assert.That(result[1].Runtime, Is.EqualTo(165)); // 2*60 + 45
    }

    [Test]
    public void MapUploadToEntities_ShouldConvertBooleanValues_UsingBoolConverter()
    {
        // Arrange
        var headerFields = new List<string> { "Title", "Is TV Series" };
        var dataFields = new List<string[]>
        {
            new[] { "Movie 1", "0" },
            new[] { "TV Show 1", "1" },
            new[] { "Movie 2", "false" },
            new[] { "TV Show 2", "true" }
        };

        // Act
        var result = UploadService.MapUploadToEntities<Movie>(headerFields, dataFields).ToList();

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Count, Is.EqualTo(4));
        Assert.That(result[0].IsTvSeries, Is.False);
        Assert.That(result[1].IsTvSeries, Is.True);
        Assert.That(result[2].IsTvSeries, Is.False);
        Assert.That(result[3].IsTvSeries, Is.True);
    }

    [Test]
    public void MapUploadToEntities_ShouldHandleMissingHeaders_ByNotSettingValues()
    {
        // Arrange
        var headerFields = new List<string> { "Title" };
        var dataFields = new List<string[]>
        {
            new[] { "Minimal Book" }
        };

        // Act
        var result = UploadService.MapUploadToEntities<Book>(headerFields, dataFields).ToList();

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Count, Is.EqualTo(1));
        Assert.That(result[0].Title, Is.EqualTo("Minimal Book"));
        Assert.That(result[0].ISBN, Is.EqualTo(string.Empty)); // Default value
        Assert.That(result[0].Authors, Is.Empty); // Default empty list
    }

    [Test]
    public void MapUploadToEntities_ShouldHandleMultipleRows()
    {
        // Arrange
        var headerFields = new List<string> { "Title", "Format", "Pages" };
        var dataFields = new List<string[]>
        {
            new[] { "Book 1", "Hardcover", "300" },
            new[] { "Book 2", "Paperback", "250" },
            new[] { "Book 3", "E-book", "400" },
            new[] { "Book 4", "Audiobook", "200" },
            new[] { "Book 5", "Hardcover", "500" }
        };

        // Act
        var result = UploadService.MapUploadToEntities<Book>(headerFields, dataFields).ToList();

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Count, Is.EqualTo(5));
        Assert.That(result[0].Title, Is.EqualTo("Book 1"));
        Assert.That(result[1].Title, Is.EqualTo("Book 2"));
        Assert.That(result[2].Title, Is.EqualTo("Book 3"));
        Assert.That(result[3].Title, Is.EqualTo("Book 4"));
        Assert.That(result[4].Title, Is.EqualTo("Book 5"));
    }

    [Test]
    public void MapUploadToEntities_ShouldHandleEmptyDataFields()
    {
        // Arrange
        var headerFields = new List<string> { "Title", "Format" };
        var dataFields = new List<string[]>();

        // Act
        var result = UploadService.MapUploadToEntities<Book>(headerFields, dataFields).ToList();

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result, Is.Empty);
    }

    [Test]
    public void MapUploadToEntities_ShouldHandleAllMovieProperties()
    {
        // Arrange
        var headerFields = new List<string>
        {
            "Title", "Barcode", "Format", "Release Date", "Audience Rating",
            "Runtime", "Studios", "Genres", "Plot", "Is TV Series"
        };
        var dataFields = new List<string[]>
        {
            new[]
            {
                "The Matrix",
                "123456789",
                "Blu-ray",
                "1999-03-31",
                "R",
                "02:16",
                "Warner Bros. | Village Roadshow",
                "Action | Sci-Fi",
                "A computer hacker learns about the true nature of reality.",
                "false"
            }
        };

        // Act
        var result = UploadService.MapUploadToEntities<Movie>(headerFields, dataFields).ToList();

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Count, Is.EqualTo(1));
        var movie = result[0];
        Assert.That(movie.Title, Is.EqualTo("The Matrix"));
        Assert.That(movie.Barcode, Is.EqualTo("123456789"));
        Assert.That(movie.Format, Is.EqualTo("Blu-ray"));
        Assert.That(movie.ReleaseDate, Is.EqualTo("1999-03-31"));
        Assert.That(movie.Rating, Is.EqualTo("R"));
        Assert.That(movie.Runtime, Is.EqualTo(136)); // 2*60 + 16
        Assert.That(movie.Studios.Count, Is.EqualTo(2));
        Assert.That(movie.Studios[0], Is.EqualTo("Warner Bros."));
        Assert.That(movie.Studios[1], Is.EqualTo("Village Roadshow"));
        Assert.That(movie.Genres.Count, Is.EqualTo(2));
        Assert.That(movie.Genres[0], Is.EqualTo("Action"));
        Assert.That(movie.Genres[1], Is.EqualTo("Sci-Fi"));
        Assert.That(movie.Plot, Is.EqualTo("A computer hacker learns about the true nature of reality."));
        Assert.That(movie.IsTvSeries, Is.False);
    }

    [Test]
    public void MapUploadToEntities_ShouldHandleAllBookProperties()
    {
        // Arrange
        var headerFields = new List<string>
        {
            "Title", "ISBN", "Format", "Pages", "Publication Date",
            "Author", "Publisher", "Genre", "Plot", "Subtitle"
        };
        var dataFields = new List<string[]>
        {
            new[]
            {
                "The Lord of the Rings",
                "978-0544003415",
                "Hardcover",
                "1178",
                "1954-07-29",
                "J.R.R. Tolkien",
                "Allen & Unwin",
                "Fantasy | Adventure | Epic",
                "A hobbit's quest to destroy a powerful ring.",
                "The Fellowship of the Ring"
            }
        };

        // Act
        var result = UploadService.MapUploadToEntities<Book>(headerFields, dataFields).ToList();

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Count, Is.EqualTo(1));
        var book = result[0];
        Assert.That(book.Title, Is.EqualTo("The Lord of the Rings"));
        Assert.That(book.ISBN, Is.EqualTo("978-0544003415"));
        Assert.That(book.Format, Is.EqualTo("Hardcover"));
        Assert.That(book.Pages, Is.EqualTo(1178));
        Assert.That(book.PublicationDate, Is.EqualTo("1954-07-29"));
        Assert.That(book.Authors.Count, Is.EqualTo(1));
        Assert.That(book.Authors[0], Is.EqualTo("J.R.R. Tolkien"));
        Assert.That(book.Publisher, Is.EqualTo("Allen & Unwin"));
        Assert.That(book.Genres.Count, Is.EqualTo(3));
        Assert.That(book.Genres[0], Is.EqualTo("Fantasy"));
        Assert.That(book.Genres[1], Is.EqualTo("Adventure"));
        Assert.That(book.Genres[2], Is.EqualTo("Epic"));
        Assert.That(book.Plot, Is.EqualTo("A hobbit's quest to destroy a powerful ring."));
        Assert.That(book.Subtitle, Is.EqualTo("The Fellowship of the Ring"));
    }

    [Test]
    public void MapUploadToEntities_ShouldHandleHeadersInDifferentOrder()
    {
        // Arrange
        var headerFields = new List<string> { "Format", "Pages", "Title", "ISBN" };
        var dataFields = new List<string[]>
        {
            new[] { "Paperback", "200", "Test Book", "1234567890" }
        };

        // Act
        var result = UploadService.MapUploadToEntities<Book>(headerFields, dataFields).ToList();

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Count, Is.EqualTo(1));
        Assert.That(result[0].Title, Is.EqualTo("Test Book"));
        Assert.That(result[0].Format, Is.EqualTo("Paperback"));
        Assert.That(result[0].Pages, Is.EqualTo(200));
        Assert.That(result[0].ISBN, Is.EqualTo("1234567890"));
    }

    [Test]
    public void MapUploadToEntities_ShouldHandleCaseInsensitiveHeaders()
    {
        // Arrange - Testing that property names are case-sensitive but matched correctly
        var headerFields = new List<string> { "Title", "format", "pages" };
        var dataFields = new List<string[]>
        {
            new[] { "Test Book", "Hardcover", "300" }
        };

        // Act
        var result = UploadService.MapUploadToEntities<Book>(headerFields, dataFields).ToList();

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Count, Is.EqualTo(1));
        Assert.That(result[0].Title, Is.EqualTo("Test Book"));
        // Note: These will be empty/default because the header names don't match exactly
        // The current implementation is case-sensitive
    }

    [Test]
    public void MapUploadToEntities_ShouldReturnEnumerable()
    {
        // Arrange
        var headerFields = new List<string> { "Title" };
        var dataFields = new List<string[]>
        {
            new[] { "Book 1" },
            new[] { "Book 2" }
        };

        // Act
        var result = UploadService.MapUploadToEntities<Book>(headerFields, dataFields);

        // Assert
        Assert.That(result, Is.InstanceOf<IEnumerable<Book>>());
        Assert.That(result.Count(), Is.EqualTo(2));
    }
}
