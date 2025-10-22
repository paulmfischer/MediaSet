using NUnit.Framework;
using MediaSet.Api.Models;

namespace MediaSet.Api.Tests.Models;

[TestFixture]
public class GameTests
{
    [Test]
    public void IsEmpty_EmptyGame_ReturnsTrue()
    {
        // Arrange
        var game = new Game();

        // Act
        var result = game.IsEmpty();

        // Assert
        Assert.That(result, Is.True);
    }

    [Test]
    public void IsEmpty_GameWithTitle_ReturnsFalse()
    {
        // Arrange
        var game = new Game { Title = "Game Title" };

        // Act
        var result = game.IsEmpty();

        // Assert
        Assert.That(result, Is.False);
    }

    [Test]
    public void IsEmpty_GameWithBarcode_ReturnsFalse()
    {
        // Arrange
        var game = new Game { Barcode = "987654321" };

        // Act
        var result = game.IsEmpty();

        // Assert
        Assert.That(result, Is.False);
    }

    [Test]
    public void IsEmpty_GameWithFormat_ReturnsFalse()
    {
        // Arrange
        var game = new Game { Format = "Digital" };

        // Act
        var result = game.IsEmpty();

        // Assert
        Assert.That(result, Is.False);
    }

    [Test]
    public void IsEmpty_GameWithReleaseDate_ReturnsFalse()
    {
        // Arrange
        var game = new Game { ReleaseDate = "2021-07-01" };

        // Act
        var result = game.IsEmpty();

        // Assert
        Assert.That(result, Is.False);
    }

    [Test]
    public void IsEmpty_GameWithRating_ReturnsFalse()
    {
        // Arrange
        var game = new Game { Rating = "M" };

        // Act
        var result = game.IsEmpty();

        // Assert
        Assert.That(result, Is.False);
    }

    [Test]
    public void IsEmpty_GameWithPublishers_ReturnsFalse()
    {
        // Arrange
        var game = new Game { Publishers = ["Game Publisher"] };

        // Act
        var result = game.IsEmpty();

        // Assert
        Assert.That(result, Is.False);
    }

    [Test]
    public void IsEmpty_GameWithPlatform_ReturnsFalse()
    {
        // Arrange
        var game = new Game { Platform = "PC" };

        // Act
        var result = game.IsEmpty();

        // Assert
        Assert.That(result, Is.False);
    }

    [Test]
    public void IsEmpty_GameWithDevelopers_ReturnsFalse()
    {
        // Arrange
        var game = new Game { Developers = ["Studio"] };

        // Act
        var result = game.IsEmpty();

        // Assert
        Assert.That(result, Is.False);
    }

    [Test]
    public void IsEmpty_GameWithGenres_ReturnsFalse()
    {
        // Arrange
        var game = new Game { Genres = ["Adventure"] };

        // Act
        var result = game.IsEmpty();

        // Assert
        Assert.That(result, Is.False);
    }

    [Test]
    public void IsEmpty_GameWithDescription_ReturnsFalse()
    {
        // Arrange
        var game = new Game { Description = "A fun game" };

        // Act
        var result = game.IsEmpty();

        // Assert
        Assert.That(result, Is.False);
    }
}
