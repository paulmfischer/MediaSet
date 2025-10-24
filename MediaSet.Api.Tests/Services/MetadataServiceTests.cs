using NUnit.Framework;
using Moq;
using Bogus;
using MediaSet.Api.Services;
using MediaSet.Api.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;

namespace MediaSet.Api.Tests.Services;

[TestFixture]
public class MetadataServiceNewTests
{
    private Mock<IEntityService<Book>> _bookServiceMock;
    private Mock<IEntityService<Movie>> _movieServiceMock;
    private Mock<IEntityService<Game>> _gameServiceMock;
    private Mock<IEntityService<Music>> _musicServiceMock;
    private Mock<ICacheService> _cacheServiceMock;
    private Mock<IOptions<CacheSettings>> _cacheSettingsMock;
    private Mock<ILogger<MetadataService>> _loggerMock;
    private MetadataService _metadataService;
    private ServiceProvider _serviceProvider;
    private CacheSettings _cacheSettings;

    [SetUp]
    public void Setup()
    {
        _bookServiceMock = new Mock<IEntityService<Book>>();
        _movieServiceMock = new Mock<IEntityService<Movie>>();
        _gameServiceMock = new Mock<IEntityService<Game>>();
        _musicServiceMock = new Mock<IEntityService<Music>>();
        _cacheServiceMock = new Mock<ICacheService>();
        _cacheSettingsMock = new Mock<IOptions<CacheSettings>>();
        _loggerMock = new Mock<ILogger<MetadataService>>();

        _cacheSettings = new CacheSettings
        {
            EnableCaching = true,
            DefaultCacheDurationMinutes = 10,
            MetadataCacheDurationMinutes = 10,
            StatsCacheDurationMinutes = 10
        };

        _cacheSettingsMock.Setup(x => x.Value).Returns(_cacheSettings);

        // Setup cache to return null (cache miss) by default
        _cacheServiceMock.Setup(c => c.GetAsync<List<string>>(It.IsAny<string>()))
            .ReturnsAsync((List<string>)null);

        var services = new ServiceCollection();
        services.AddScoped<IEntityService<Book>>(_ => _bookServiceMock.Object);
        services.AddScoped<IEntityService<Movie>>(_ => _movieServiceMock.Object);
        services.AddScoped<IEntityService<Game>>(_ => _gameServiceMock.Object);
        services.AddScoped<IEntityService<Music>>(_ => _musicServiceMock.Object);

        _serviceProvider = services.BuildServiceProvider();
        _metadataService = new MetadataService(
            _serviceProvider,
            _cacheServiceMock.Object,
            _cacheSettingsMock.Object,
            _loggerMock.Object);
    }

    [TearDown]
    public void TearDown()
    {
        _serviceProvider?.Dispose();
    }

    [Test]
    public async Task GetMetadata_WithFormats_WithBooks_ShouldReturnDistinctFormats()
    {
        // Arrange
        var books = new List<Book>
        {
            new Book { Format = "Hardcover" },
            new Book { Format = "Paperback" },
            new Book { Format = "Hardcover" }, // Duplicate
            new Book { Format = "eBook" }
        };

        _bookServiceMock.Setup(s  => s.GetListAsync(It.IsAny<CancellationToken>())).ReturnsAsync(books);

        // Act
        var result = await _metadataService.GetMetadata(MediaTypes.Books, "Format");

        // Assert
        Assert.That(result.Count(), Is.EqualTo(3));
        Assert.That(result, Contains.Item("Hardcover"));
        Assert.That(result, Contains.Item("Paperback"));
        Assert.That(result, Contains.Item("eBook"));
        Assert.That(result, Is.Ordered);
    }

    [Test]
    public async Task GetMetadata_WithFormats_WithMovies_ShouldReturnDistinctFormats()
    {
        // Arrange
        var movies = new List<Movie>
        {
            new Movie { Format = "DVD" },
            new Movie { Format = "Blu-ray" },
            new Movie { Format = "DVD" }
        };

        _movieServiceMock.Setup(s  => s.GetListAsync(It.IsAny<CancellationToken>())).ReturnsAsync(movies);

        // Act
        var result = await _metadataService.GetMetadata(MediaTypes.Movies, "Format");

        // Assert
        Assert.That(result.Count(), Is.EqualTo(2));
        Assert.That(result, Contains.Item("DVD"));
        Assert.That(result, Contains.Item("Blu-ray"));
    }

    [Test]
    public async Task GetMetadata_WithGenres_WithBooks_ShouldReturnDistinctGenres()
    {
        // Arrange
        var books = new List<Book>
        {
            new Book { Genres = ["Fiction", "Mystery"] },
            new Book { Genres = ["Mystery", "Thriller"] },
            new Book { Genres = ["Sci-Fi"] }
        };

        _bookServiceMock.Setup(s  => s.GetListAsync(It.IsAny<CancellationToken>())).ReturnsAsync(books);

        // Act
        var result = await _metadataService.GetMetadata(MediaTypes.Books, "Genres");

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

        _bookServiceMock.Setup(s  => s.GetListAsync(It.IsAny<CancellationToken>())).ReturnsAsync(books);

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

        _movieServiceMock.Setup(s  => s.GetListAsync(It.IsAny<CancellationToken>())).ReturnsAsync(movies);

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

        _musicServiceMock.Setup(s  => s.GetListAsync(It.IsAny<CancellationToken>())).ReturnsAsync(musics);

        // Act
        var result = await _metadataService.GetMetadata(MediaTypes.Musics, "Artist");

        // Assert
        Assert.That(result.Count(), Is.EqualTo(2));
        Assert.That(result, Contains.Item("The Beatles"));
        Assert.That(result, Contains.Item("Queen"));
    }

    [Test]
    public async Task GetMetadata_WithFormats_ShouldTrimWhitespace()
    {
        // Arrange
        var books = new List<Book>
        {
            new Book { Format = "  Hardcover  " },
            new Book { Format = "Paperback  " }
        };

        _bookServiceMock.Setup(s  => s.GetListAsync(It.IsAny<CancellationToken>())).ReturnsAsync(books);

        // Act
        var result = await _metadataService.GetMetadata(MediaTypes.Books, "Format");

        // Assert
        Assert.That(result, Contains.Item("Hardcover"));
        Assert.That(result, Contains.Item("Paperback"));
        Assert.That(result.All(f => f == f.Trim()), Is.True);
    }

    [Test]
    public async Task GetMetadata_WithFormats_ShouldExcludeEmptyValues()
    {
        // Arrange
        var books = new List<Book>
        {
            new Book { Format = "Hardcover" },
            new Book { Format = "" },
            new Book { Format = "   " },
            new Book { Format = "Paperback" }
        };

        _bookServiceMock.Setup(s  => s.GetListAsync(It.IsAny<CancellationToken>())).ReturnsAsync(books);

        // Act
        var result = await _metadataService.GetMetadata(MediaTypes.Books, "Format");

        // Assert
        Assert.That(result.Count(), Is.EqualTo(2));
        Assert.That(result, Contains.Item("Hardcover"));
        Assert.That(result, Contains.Item("Paperback"));
    }

    [Test]
    public async Task GetMetadata_WithFormats_ShouldReturnEmpty_WhenNoEntitiesExist()
    {
        // Arrange
        _bookServiceMock.Setup(s  => s.GetListAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<Book>());

        // Act
        var result = await _metadataService.GetMetadata(MediaTypes.Books, "Format");

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
        _bookServiceMock.Setup(s  => s.GetListAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<Book>());

        // Act & Assert
        Assert.ThrowsAsync<ArgumentException>(async () => 
            await _metadataService.GetMetadata(MediaTypes.Books, "NonExistentProperty"));
    }

    [Test]
    public async Task GetMetadata_ShouldBeCaseInsensitive_WithLowercasePropertyName()
    {
        // Arrange
        var books = new List<Book>
        {
            new Book { Authors = ["J.K. Rowling", "Stephen King"] },
            new Book { Authors = ["Stephen King"] }
        };

        _bookServiceMock.Setup(s  => s.GetListAsync(It.IsAny<CancellationToken>())).ReturnsAsync(books);

        // Act
        var result = await _metadataService.GetMetadata(MediaTypes.Books, "authors");

        // Assert
        Assert.That(result.Count(), Is.EqualTo(2));
        Assert.That(result, Contains.Item("J.K. Rowling"));
        Assert.That(result, Contains.Item("Stephen King"));
    }

    [Test]
    public async Task GetMetadata_ShouldBeCaseInsensitive_WithUppercasePropertyName()
    {
        // Arrange
        var books = new List<Book>
        {
            new Book { Authors = ["J.K. Rowling", "Stephen King"] },
            new Book { Authors = ["Stephen King"] }
        };

        _bookServiceMock.Setup(s  => s.GetListAsync(It.IsAny<CancellationToken>())).ReturnsAsync(books);

        // Act
        var result = await _metadataService.GetMetadata(MediaTypes.Books, "AUTHORS");

        // Assert
        Assert.That(result.Count(), Is.EqualTo(2));
        Assert.That(result, Contains.Item("J.K. Rowling"));
        Assert.That(result, Contains.Item("Stephen King"));
    }

    [Test]
    public async Task GetMetadata_ShouldBeCaseInsensitive_WithMixedCasePropertyName()
    {
        // Arrange
        var books = new List<Book>
        {
            new Book { Authors = ["J.K. Rowling", "Stephen King"] },
            new Book { Authors = ["Stephen King"] }
        };

        _bookServiceMock.Setup(s  => s.GetListAsync(It.IsAny<CancellationToken>())).ReturnsAsync(books);

        // Act
        var result = await _metadataService.GetMetadata(MediaTypes.Books, "AuThOrS");

        // Assert
        Assert.That(result.Count(), Is.EqualTo(2));
        Assert.That(result, Contains.Item("J.K. Rowling"));
        Assert.That(result, Contains.Item("Stephen King"));
    }

    [Test]
    public async Task GetMetadata_ShouldBeCaseInsensitive_ForFormatProperty()
    {
        // Arrange
        var books = new List<Book>
        {
            new Book { Format = "Hardcover" },
            new Book { Format = "Paperback" }
        };

        _bookServiceMock.Setup(s  => s.GetListAsync(It.IsAny<CancellationToken>())).ReturnsAsync(books);

        // Act
        var result = await _metadataService.GetMetadata(MediaTypes.Books, "format");

        // Assert
        Assert.That(result.Count(), Is.EqualTo(2));
        Assert.That(result, Contains.Item("Hardcover"));
        Assert.That(result, Contains.Item("Paperback"));
    }

    [Test]
    public async Task GetMetadata_ShouldBeCaseInsensitive_ForGenresProperty()
    {
        // Arrange
        var books = new List<Book>
        {
            new Book { Genres = ["Fiction", "Mystery"] },
            new Book { Genres = ["Thriller"] }
        };

        _bookServiceMock.Setup(s  => s.GetListAsync(It.IsAny<CancellationToken>())).ReturnsAsync(books);

        // Act
        var result = await _metadataService.GetMetadata(MediaTypes.Books, "genres");

        // Assert
        Assert.That(result.Count(), Is.EqualTo(3));
        Assert.That(result, Contains.Item("Fiction"));
        Assert.That(result, Contains.Item("Mystery"));
        Assert.That(result, Contains.Item("Thriller"));
    }

    [Test]
    public async Task GetMetadata_ShouldBeCaseInsensitive_ForMovieStudios()
    {
        // Arrange
        var movies = new List<Movie>
        {
            new Movie { Studios = ["Warner Bros", "Universal"] },
            new Movie { Studios = ["Disney"] }
        };

        _movieServiceMock.Setup(s  => s.GetListAsync(It.IsAny<CancellationToken>())).ReturnsAsync(movies);

        // Act
        var result = await _metadataService.GetMetadata(MediaTypes.Movies, "studios");

        // Assert
        Assert.That(result.Count(), Is.EqualTo(3));
        Assert.That(result, Contains.Item("Warner Bros"));
        Assert.That(result, Contains.Item("Universal"));
        Assert.That(result, Contains.Item("Disney"));
    }

    [Test]
    public async Task GetMetadata_ShouldBeCaseInsensitive_ForMusicArtist()
    {
        // Arrange
        var musics = new List<Music>
        {
            new Music { Artist = "The Beatles" },
            new Music { Artist = "Queen" }
        };

        _musicServiceMock.Setup(s  => s.GetListAsync(It.IsAny<CancellationToken>())).ReturnsAsync(musics);

        // Act
        var result = await _metadataService.GetMetadata(MediaTypes.Musics, "artist");

        // Assert
        Assert.That(result.Count(), Is.EqualTo(2));
        Assert.That(result, Contains.Item("The Beatles"));
        Assert.That(result, Contains.Item("Queen"));
    }

    #region Caching Tests

    [Test]
    public async Task GetMetadata_ShouldReturnCachedValue_WhenCacheHit()
    {
        // Arrange
        var cachedFormats = new List<string> { "Cached1", "Cached2" };
        _cacheServiceMock.Setup(c => c.GetAsync<List<string>>("metadata:Books:Format"))
            .ReturnsAsync(cachedFormats);

        // Act
        var result = await _metadataService.GetMetadata(MediaTypes.Books, "Format");

        // Assert
        Assert.That(result, Is.EqualTo(cachedFormats));
        _bookServiceMock.Verify(s => s.GetListAsync(), Times.Never);
        _cacheServiceMock.Verify(c => c.GetAsync<List<string>>("metadata:Books:Format"), Times.Once);
    }

    [Test]
    public async Task GetMetadata_ShouldFetchFromDatabase_WhenCacheMiss()
    {
        // Arrange
        var books = new List<Book>
        {
            new Book { Format = "Hardcover" },
            new Book { Format = "Paperback" }
        };

        _cacheServiceMock.Setup(c => c.GetAsync<List<string>>(It.IsAny<string>()))
            .ReturnsAsync((List<string>)null);
        _bookServiceMock.Setup(s => s.GetListAsync()).ReturnsAsync(books);

        // Act
        var result = await _metadataService.GetMetadata(MediaTypes.Books, "Format");

        // Assert
        Assert.That(result.Count(), Is.EqualTo(2));
        _bookServiceMock.Verify(s => s.GetListAsync(), Times.Once);
        _cacheServiceMock.Verify(c => c.SetAsync(
            "metadata:Books:Format",
            It.IsAny<List<string>>(),
            null), Times.Once);
    }

    [Test]
    public async Task GetMetadata_ShouldStoreResultInCache_AfterDatabaseFetch()
    {
        // Arrange
        var books = new List<Book>
        {
            new Book { Format = "Hardcover" },
            new Book { Format = "Paperback" },
            new Book { Format = "Hardcover" }
        };

        _cacheServiceMock.Setup(c => c.GetAsync<List<string>>(It.IsAny<string>()))
            .ReturnsAsync((List<string>)null);
        _bookServiceMock.Setup(s => s.GetListAsync()).ReturnsAsync(books);

        // Act
        await _metadataService.GetMetadata(MediaTypes.Books, "Format");

        // Assert
        _cacheServiceMock.Verify(c => c.SetAsync(
            "metadata:Books:Format",
            It.Is<List<string>>(list => list.Count == 2 && list.Contains("Hardcover") && list.Contains("Paperback")),
            null), Times.Once);
    }

    [Test]
    public async Task GetMetadata_ShouldUseDifferentCacheKeys_ForDifferentMediaTypes()
    {
        // Arrange
        var books = new List<Book> { new Book { Format = "Hardcover" } };
        var movies = new List<Movie> { new Movie { Format = "DVD" } };

        _bookServiceMock.Setup(s => s.GetListAsync()).ReturnsAsync(books);
        _movieServiceMock.Setup(s => s.GetListAsync()).ReturnsAsync(movies);

        // Act
        await _metadataService.GetMetadata(MediaTypes.Books, "Format");
        await _metadataService.GetMetadata(MediaTypes.Movies, "Format");

        // Assert
        _cacheServiceMock.Verify(c => c.GetAsync<List<string>>("metadata:Books:Format"), Times.Once);
        _cacheServiceMock.Verify(c => c.GetAsync<List<string>>("metadata:Movies:Format"), Times.Once);
    }

    [Test]
    public async Task GetMetadata_ShouldUseDifferentCacheKeys_ForDifferentProperties()
    {
        // Arrange
        var books = new List<Book>
        {
            new Book { Format = "Hardcover", Genres = ["Fiction"] }
        };

        _bookServiceMock.Setup(s => s.GetListAsync()).ReturnsAsync(books);

        // Act
        await _metadataService.GetMetadata(MediaTypes.Books, "Format");
        await _metadataService.GetMetadata(MediaTypes.Books, "Genres");

        // Assert
        _cacheServiceMock.Verify(c => c.GetAsync<List<string>>("metadata:Books:Format"), Times.Once);
        _cacheServiceMock.Verify(c => c.GetAsync<List<string>>("metadata:Books:Genres"), Times.Once);
    }

    #endregion
}
