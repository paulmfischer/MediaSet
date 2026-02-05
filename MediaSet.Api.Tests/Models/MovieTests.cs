using MediaSet.Api.Features.Entities.Models;
using NUnit.Framework;
using MediaSet.Api.Models;

namespace MediaSet.Api.Tests.Models;

[TestFixture]
public class MovieTests
{
    [Test]
    public void Constructor_SetsTypeToMovies()
    {
        // Act
        var movie = new Movie();

        // Assert
        Assert.That(movie.Type, Is.EqualTo(MediaTypes.Movies));
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
}
