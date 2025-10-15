using NUnit.Framework;
using MediaSet.Api.Helpers;
using MediaSet.Api.Models;

namespace MediaSet.Api.Tests.Helpers;

[TestFixture]
public class EntityExtensionsTests
{
    [Test]
    public void IsEmpty_EmptyBook_ReturnsTrue()
    {
        // Arrange
        var book = new Book();

        // Act
        var result = book.IsEmpty();

        // Assert
        Assert.That(result, Is.True);
    }

    [Test]
    public void IsEmpty_BookWithTitle_ReturnsFalse()
    {
        // Arrange
        var book = new Book { Title = "Some Title" };

        // Act
        var result = book.IsEmpty();

        // Assert
        Assert.That(result, Is.False);
    }

    [Test]
    public void IsEmpty_BookWithISBN_ReturnsFalse()
    {
        // Arrange
        var book = new Book { ISBN = "978-0-123456-78-9" };

        // Act
        var result = book.IsEmpty();

        // Assert
        Assert.That(result, Is.False);
    }

    [Test]
    public void IsEmpty_BookWithFormat_ReturnsFalse()
    {
        // Arrange
        var book = new Book { Format = "Hardcover" };

        // Act
        var result = book.IsEmpty();

        // Assert
        Assert.That(result, Is.False);
    }

    [Test]
    public void IsEmpty_BookWithPlot_ReturnsFalse()
    {
        // Arrange
        var book = new Book { Plot = "A great story" };

        // Act
        var result = book.IsEmpty();

        // Assert
        Assert.That(result, Is.False);
    }

    [Test]
    public void IsEmpty_BookWithPublicationDate_ReturnsFalse()
    {
        // Arrange
        var book = new Book { PublicationDate = "2020-01-01" };

        // Act
        var result = book.IsEmpty();

        // Assert
        Assert.That(result, Is.False);
    }

    [Test]
    public void IsEmpty_BookWithPublisher_ReturnsFalse()
    {
        // Arrange
        var book = new Book { Publisher = "Publisher Name" };

        // Act
        var result = book.IsEmpty();

        // Assert
        Assert.That(result, Is.False);
    }

    [Test]
    public void IsEmpty_BookWithPages_ReturnsFalse()
    {
        // Arrange
        var book = new Book { Pages = 300 };

        // Act
        var result = book.IsEmpty();

        // Assert
        Assert.That(result, Is.False);
    }

    [Test]
    public void IsEmpty_BookWithAuthors_ReturnsFalse()
    {
        // Arrange
        var book = new Book { Authors = ["Author Name"] };

        // Act
        var result = book.IsEmpty();

        // Assert
        Assert.That(result, Is.False);
    }

    [Test]
    public void IsEmpty_BookWithGenres_ReturnsFalse()
    {
        // Arrange
        var book = new Book { Genres = ["Fiction"] };

        // Act
        var result = book.IsEmpty();

        // Assert
        Assert.That(result, Is.False);
    }

    [Test]
    public void IsEmpty_EmptyMovie_ReturnsTrue()
    {
        // Arrange
        var movie = new Movie();

        // Act
        var result = movie.IsEmpty();

        // Assert
        Assert.That(result, Is.True);
    }

    [Test]
    public void IsEmpty_MovieWithTitle_ReturnsFalse()
    {
        // Arrange
        var movie = new Movie { Title = "Movie Title" };

        // Act
        var result = movie.IsEmpty();

        // Assert
        Assert.That(result, Is.False);
    }

    [Test]
    public void IsEmpty_MovieWithBarcode_ReturnsFalse()
    {
        // Arrange
        var movie = new Movie { Barcode = "123456789" };

        // Act
        var result = movie.IsEmpty();

        // Assert
        Assert.That(result, Is.False);
    }

    [Test]
    public void IsEmpty_MovieWithFormat_ReturnsFalse()
    {
        // Arrange
        var movie = new Movie { Format = "Blu-ray" };

        // Act
        var result = movie.IsEmpty();

        // Assert
        Assert.That(result, Is.False);
    }

    [Test]
    public void IsEmpty_MovieWithReleaseDate_ReturnsFalse()
    {
        // Arrange
        var movie = new Movie { ReleaseDate = "2020-01-01" };

        // Act
        var result = movie.IsEmpty();

        // Assert
        Assert.That(result, Is.False);
    }

    [Test]
    public void IsEmpty_MovieWithRating_ReturnsFalse()
    {
        // Arrange
        var movie = new Movie { Rating = "PG-13" };

        // Act
        var result = movie.IsEmpty();

        // Assert
        Assert.That(result, Is.False);
    }

    [Test]
    public void IsEmpty_MovieWithRuntime_ReturnsFalse()
    {
        // Arrange
        var movie = new Movie { Runtime = 120 };

        // Act
        var result = movie.IsEmpty();

        // Assert
        Assert.That(result, Is.False);
    }

    [Test]
    public void IsEmpty_MovieWithStudios_ReturnsFalse()
    {
        // Arrange
        var movie = new Movie { Studios = ["Studio Name"] };

        // Act
        var result = movie.IsEmpty();

        // Assert
        Assert.That(result, Is.False);
    }

    [Test]
    public void IsEmpty_MovieWithGenres_ReturnsFalse()
    {
        // Arrange
        var movie = new Movie { Genres = ["Action"] };

        // Act
        var result = movie.IsEmpty();

        // Assert
        Assert.That(result, Is.False);
    }

    [Test]
    public void IsEmpty_MovieWithPlot_ReturnsFalse()
    {
        // Arrange
        var movie = new Movie { Plot = "An exciting movie" };

        // Act
        var result = movie.IsEmpty();

        // Assert
        Assert.That(result, Is.False);
    }

    [Test]
    public void SetType_WithBookEntity_SetsTypeToBooksAndReturnsBook()
    {
        // Arrange
        var book = new Book { Title = "Test Book" };

        // Act
        var result = book.SetType();

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Type, Is.EqualTo(MediaTypes.Books));
    }

    [Test]
    public void SetType_WithMovieEntity_SetsTypeToMoviesAndReturnsMovie()
    {
        // Arrange
        var movie = new Movie { Title = "Test Movie" };

        // Act
        var result = movie.SetType();

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Type, Is.EqualTo(MediaTypes.Movies));
    }

    [Test]
    public void SetType_WithNullEntity_ReturnsNull()
    {
        // Arrange
#nullable enable
        Book? book = null;
#nullable restore

        // Act
        var result = book.SetType();

        // Assert
        Assert.That(result, Is.Null);
    }
}
