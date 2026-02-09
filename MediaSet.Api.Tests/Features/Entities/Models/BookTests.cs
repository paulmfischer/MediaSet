using MediaSet.Api.Shared.Models;
using NUnit.Framework;

namespace MediaSet.Api.Tests.Features.Entities.Models;

[TestFixture]
public class BookTests
{
    [Test]
    public void Constructor_SetsTypeToBooks()
    {
        // Act
        var book = new Book();

        // Assert
        Assert.That(book.Type, Is.EqualTo(MediaTypes.Books));
    }

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
}
