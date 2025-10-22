using NUnit.Framework;
using MediaSet.Api.Helpers;
using MediaSet.Api.Models;

namespace MediaSet.Api.Tests.Helpers;

[TestFixture]
public class EntityExtensionsTests
{
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
    public void SetType_WithGameEntity_SetsTypeToGamesAndReturnsGame()
    {
        // Arrange
        var game = new Game { Title = "Test Game" };

        // Act
        var result = game.SetType();

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Type, Is.EqualTo(MediaTypes.Games));
    }

    [Test]
    public void SetType_WithMusicEntity_SetsTypeToMusicAndReturnsMusic()
    {
        // Arrange
        var music = new Music { Title = "Test Song" };

        // Act
        var result = music.SetType();

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Type, Is.EqualTo(MediaTypes.Musics));
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
