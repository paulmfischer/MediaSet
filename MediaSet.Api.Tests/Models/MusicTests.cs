using MediaSet.Api.Features.Entities.Models;
using NUnit.Framework;
using MediaSet.Api.Features.Entities.Models;

namespace MediaSet.Api.Tests.Models;

[TestFixture]
public class MusicTests
{
    [Test]
    public void Constructor_SetsTypeToMusics()
    {
        // Act
        var music = new Music();

        // Assert
        Assert.That(music.Type, Is.EqualTo(MediaTypes.Musics));
    }

    [Test]
    public void IsEmpty_EmptyMusic_ReturnsTrue()
    {
        // Arrange
        var music = new Music();

        // Act
        var result = music.IsEmpty();

        // Assert
        Assert.That(result, Is.True);
    }

    [Test]
    public void IsEmpty_MusicWithTitle_ReturnsFalse()
    {
        // Arrange
        var music = new Music { Title = "Song Title" };

        // Act
        var result = music.IsEmpty();

        // Assert
        Assert.That(result, Is.False);
    }

    [Test]
    public void IsEmpty_MusicWithFormat_ReturnsFalse()
    {
        // Arrange
        var music = new Music { Format = "CD" };

        // Act
        var result = music.IsEmpty();

        // Assert
        Assert.That(result, Is.False);
    }

    [Test]
    public void IsEmpty_MusicWithArtist_ReturnsFalse()
    {
        // Arrange
        var music = new Music { Artist = "Artist Name" };

        // Act
        var result = music.IsEmpty();

        // Assert
        Assert.That(result, Is.False);
    }

    [Test]
    public void IsEmpty_MusicWithReleaseDate_ReturnsFalse()
    {
        // Arrange
        var music = new Music { ReleaseDate = "2022-03-15" };

        // Act
        var result = music.IsEmpty();

        // Assert
        Assert.That(result, Is.False);
    }

    [Test]
    public void IsEmpty_MusicWithGenres_ReturnsFalse()
    {
        // Arrange
        var music = new Music { Genres = ["Rock"] };

        // Act
        var result = music.IsEmpty();

        // Assert
        Assert.That(result, Is.False);
    }

    [Test]
    public void IsEmpty_MusicWithDuration_ReturnsFalse()
    {
        // Arrange
        var music = new Music { Duration = 240 };

        // Act
        var result = music.IsEmpty();

        // Assert
        Assert.That(result, Is.False);
    }

    [Test]
    public void IsEmpty_MusicWithLabel_ReturnsFalse()
    {
        // Arrange
        var music = new Music { Label = "Record Label" };

        // Act
        var result = music.IsEmpty();

        // Assert
        Assert.That(result, Is.False);
    }

    [Test]
    public void IsEmpty_MusicWithBarcode_ReturnsFalse()
    {
        // Arrange
        var music = new Music { Barcode = "123456789012" };

        // Act
        var result = music.IsEmpty();

        // Assert
        Assert.That(result, Is.False);
    }

    [Test]
    public void IsEmpty_MusicWithTracks_ReturnsFalse()
    {
        // Arrange
        var music = new Music { Tracks = 12 };

        // Act
        var result = music.IsEmpty();

        // Assert
        Assert.That(result, Is.False);
    }

    [Test]
    public void IsEmpty_MusicWithDiscs_ReturnsFalse()
    {
        // Arrange
        var music = new Music { Discs = 2 };

        // Act
        var result = music.IsEmpty();

        // Assert
        Assert.That(result, Is.False);
    }

    [Test]
    public void IsEmpty_MusicWithDiscList_ReturnsFalse()
    {
        // Arrange
        var music = new Music { DiscList = [new Disc { TrackNumber = 1, Title = "Track 1" }] };

        // Act
        var result = music.IsEmpty();

        // Assert
        Assert.That(result, Is.False);
    }
}
