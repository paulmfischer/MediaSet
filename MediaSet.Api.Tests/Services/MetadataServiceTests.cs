using NUnit.Framework;
using Moq;
using Bogus;
using MediaSet.Api.Services;
using MediaSet.Api.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;

namespace MediaSet.Api.Tests.Services;

[TestFixture]
public class MetadataServiceNewTests
{
    private Mock<IEntityService<Book>> _bookServiceMock;
    private Mock<IEntityService<Movie>> _movieServiceMock;
    private Mock<IEntityService<Game>> _gameServiceMock;
    private Mock<IEntityService<Music>> _musicServiceMock;
    private MetadataService _metadataService;
    private ServiceProvider _serviceProvider;

    [SetUp]
    public void Setup()
    {
        _bookServiceMock = new Mock<IEntityService<Book>>();
        _movieServiceMock = new Mock<IEntityService<Movie>>();
        _gameServiceMock = new Mock<IEntityService<Game>>();
        _musicServiceMock = new Mock<IEntityService<Music>>();

        var services = new ServiceCollection();
        services.AddScoped<IEntityService<Book>>(_ => _bookServiceMock.Object);
        services.AddScoped<IEntityService<Movie>>(_ => _movieServiceMock.Object);
        services.AddScoped<IEntityService<Game>>(_ => _gameServiceMock.Object);
        services.AddScoped<IEntityService<Music>>(_ => _musicServiceMock.Object);

        _serviceProvider = services.BuildServiceProvider();
        _metadataService = new MetadataService(_serviceProvider);
    }

    [TearDown]
    public void TearDown()
    {
        _serviceProvider?.Dispose();
    }

    [Test]
    public async Task GetFormats_WithBooks_ShouldReturnDistinctFormats()
    {
        // Arrange
        var books = new List<Book>
        {
            new Book { Format = "Hardcover" },
            new Book { Format = "Paperback" },
            new Book { Format = "Hardcover" }, // Duplicate
            new Book { Format = "eBook" }
        };

        _bookServiceMock.Setup(s => s.GetListAsync()).ReturnsAsync(books);

        // Act
        var result = await _metadataService.GetFormats(MediaTypes.Books);

        // Assert
        Assert.That(result.Count(), Is.EqualTo(3));
        Assert.That(result, Contains.Item("Hardcover"));
        Assert.That(result, Contains.Item("Paperback"));
        Assert.That(result, Contains.Item("eBook"));
        Assert.That(result, Is.Ordered);
    }

    [Test]
    public async Task GetFormats_WithMovies_ShouldReturnDistinctFormats()
    {
        // Arrange
        var movies = new List<Movie>
        {
            new Movie { Format = "DVD" },
            new Movie { Format = "Blu-ray" },
            new Movie { Format = "DVD" }
        };

        _movieServiceMock.Setup(s => s.GetListAsync()).ReturnsAsync(movies);

        // Act
        var result = await _metadataService.GetFormats(MediaTypes.Movies);

        // Assert
        Assert.That(result.Count(), Is.EqualTo(2));
        Assert.That(result, Contains.Item("DVD"));
        Assert.That(result, Contains.Item("Blu-ray"));
    }

    [Test]
    public async Task GetGenres_WithBooks_ShouldReturnDistinctGenres()
    {
        // Arrange
        var books = new List<Book>
        {
            new Book { Genres = ["Fiction", "Mystery"] },
            new Book { Genres = ["Mystery", "Thriller"] },
            new Book { Genres = ["Sci-Fi"] }
        };

        _bookServiceMock.Setup(s => s.GetListAsync()).ReturnsAsync(books);

        // Act
        var result = await _metadataService.GetGenres(MediaTypes.Books);

        // Assert
        Assert.That(result.Count(), Is.EqualTo(4));
        Assert.That(result, Contains.Item("Fiction"));
        Assert.That(result, Contains.Item("Mystery"));
        Assert.That(result, Contains.Item("Thriller"));
        Assert.That(result, Contains.Item("Sci-Fi"));
    }

    [Test]
    public async Task GetMetadata_WithAuthors_ShouldReturnDistinctAuthors()
    {
        // Arrange
        var books = new List<Book>
        {
            new Book { Authors = ["J.K. Rowling", "Stephen King"] },
            new Book { Authors = ["Stephen King"] }
        };

        _bookServiceMock.Setup(s => s.GetListAsync()).ReturnsAsync(books);

        // Act
        var result = await _metadataService.GetMetadata(MediaTypes.Books, "Authors");

        // Assert
        Assert.That(result.Count(), Is.EqualTo(2));
        Assert.That(result, Contains.Item("J.K. Rowling"));
        Assert.That(result, Contains.Item("Stephen King"));
    }

    [Test]
    public async Task GetMetadata_WithStudios_ShouldReturnDistinctStudios()
    {
        // Arrange
        var movies = new List<Movie>
        {
            new Movie { Studios = ["Warner Bros", "Universal"] },
            new Movie { Studios = ["Disney"] }
        };

        _movieServiceMock.Setup(s => s.GetListAsync()).ReturnsAsync(movies);

        // Act
        var result = await _metadataService.GetMetadata(MediaTypes.Movies, "Studios");

        // Assert
        Assert.That(result.Count(), Is.EqualTo(3));
        Assert.That(result, Contains.Item("Warner Bros"));
        Assert.That(result, Contains.Item("Universal"));
        Assert.That(result, Contains.Item("Disney"));
    }

    [Test]
    public async Task GetMetadata_WithArtist_ShouldReturnDistinctArtists()
    {
        // Arrange
        var musics = new List<Music>
        {
            new Music { Artist = "The Beatles" },
            new Music { Artist = "Queen" },
            new Music { Artist = "The Beatles" }
        };

        _musicServiceMock.Setup(s => s.GetListAsync()).ReturnsAsync(musics);

        // Act
        var result = await _metadataService.GetMetadata(MediaTypes.Musics, "Artist");

        // Assert
        Assert.That(result.Count(), Is.EqualTo(2));
        Assert.That(result, Contains.Item("The Beatles"));
        Assert.That(result, Contains.Item("Queen"));
    }

    [Test]
    public async Task GetFormats_ShouldTrimWhitespace()
    {
        // Arrange
        var books = new List<Book>
        {
            new Book { Format = "  Hardcover  " },
            new Book { Format = "Paperback  " }
        };

        _bookServiceMock.Setup(s => s.GetListAsync()).ReturnsAsync(books);

        // Act
        var result = await _metadataService.GetFormats(MediaTypes.Books);

        // Assert
        Assert.That(result, Contains.Item("Hardcover"));
        Assert.That(result, Contains.Item("Paperback"));
        Assert.That(result.All(f => f == f.Trim()), Is.True);
    }

    [Test]
    public async Task GetFormats_ShouldExcludeEmptyValues()
    {
        // Arrange
        var books = new List<Book>
        {
            new Book { Format = "Hardcover" },
            new Book { Format = "" },
            new Book { Format = "   " },
            new Book { Format = "Paperback" }
        };

        _bookServiceMock.Setup(s => s.GetListAsync()).ReturnsAsync(books);

        // Act
        var result = await _metadataService.GetFormats(MediaTypes.Books);

        // Assert
        Assert.That(result.Count(), Is.EqualTo(2));
        Assert.That(result, Contains.Item("Hardcover"));
        Assert.That(result, Contains.Item("Paperback"));
    }

    [Test]
    public async Task GetFormats_ShouldReturnEmpty_WhenNoEntitiesExist()
    {
        // Arrange
        _bookServiceMock.Setup(s => s.GetListAsync()).ReturnsAsync(new List<Book>());

        // Act
        var result = await _metadataService.GetFormats(MediaTypes.Books);

        // Assert
        Assert.That(result, Is.Empty);
    }

    [Test]
    public void GetMetadata_ShouldThrowException_ForInvalidMediaType()
    {
        // Act & Assert
        Assert.ThrowsAsync<ArgumentException>(async () => 
            await _metadataService.GetMetadata((MediaTypes)999, "Format"));
    }

    [Test]
    public void GetMetadata_ShouldThrowException_ForInvalidProperty()
    {
        // Arrange
        _bookServiceMock.Setup(s => s.GetListAsync()).ReturnsAsync(new List<Book>());

        // Act & Assert
        Assert.ThrowsAsync<ArgumentException>(async () => 
            await _metadataService.GetMetadata(MediaTypes.Books, "NonExistentProperty"));
    }
}
