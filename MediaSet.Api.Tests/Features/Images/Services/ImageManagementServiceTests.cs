using MediaSet.Api.Features.Images.Services;
using MediaSet.Api.Infrastructure.Caching;
using MediaSet.Api.Infrastructure.DataAccess;
using MediaSet.Api.Infrastructure.Storage;
using MediaSet.Api.Shared.Models;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MediaSet.Api.Tests.Features.Images.Services;

[TestFixture]
public class ImageManagementServiceTests
{
    private Mock<IImageStorageProvider> _storageProviderMock = null!;
    private Mock<IEntityService<Book>> _bookServiceMock = null!;
    private Mock<IEntityService<Movie>> _movieServiceMock = null!;
    private Mock<IEntityService<Game>> _gameServiceMock = null!;
    private Mock<IEntityService<Music>> _musicServiceMock = null!;
    private Mock<ICacheService> _cacheServiceMock = null!;
    private Mock<ILogger<ImageManagementService>> _loggerMock = null!;
    private ImageManagementService _imageManagementService = null!;

    [SetUp]
    public void Setup()
    {
        _storageProviderMock = new Mock<IImageStorageProvider>();
        _bookServiceMock = new Mock<IEntityService<Book>>();
        _movieServiceMock = new Mock<IEntityService<Movie>>();
        _gameServiceMock = new Mock<IEntityService<Game>>();
        _musicServiceMock = new Mock<IEntityService<Music>>();
        _cacheServiceMock = new Mock<ICacheService>();
        _loggerMock = new Mock<ILogger<ImageManagementService>>();

        _storageProviderMock.Setup(s => s.ListFiles())
            .Returns(new List<(string, long)>());

        _bookServiceMock.Setup(s => s.GetListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Book>());
        _movieServiceMock.Setup(s => s.GetListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Movie>());
        _gameServiceMock.Setup(s => s.GetListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Game>());
        _musicServiceMock.Setup(s => s.GetListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Music>());

        _imageManagementService = new ImageManagementService(
            _storageProviderMock.Object,
            _bookServiceMock.Object,
            _movieServiceMock.Object,
            _gameServiceMock.Object,
            _musicServiceMock.Object,
            _cacheServiceMock.Object,
            _loggerMock.Object);
    }

    [Test]
    public async Task DeleteOrphanedImagesAsync_ShouldReturnZero_WhenNoOrphanedFiles()
    {
        // Act
        var result = await _imageManagementService.DeleteOrphanedImagesAsync();

        // Assert
        Assert.That(result, Is.EqualTo(0));
        _storageProviderMock.Verify(s => s.DeleteImage(It.IsAny<string>()), Times.Never);
    }

    [Test]
    public async Task DeleteOrphanedImagesAsync_ShouldDeleteOrphanedFiles_AndReturnCount()
    {
        // Arrange
        _storageProviderMock.Setup(s => s.ListFiles())
            .Returns(new List<(string RelativePath, long SizeBytes)>
            {
                ("books/file1.jpg", 1000),
                ("books/orphaned.jpg", 500),
            });

        var book = new Book { Id = "1", Title = "Test", Format = "Paperback", CoverImage = new Image { FilePath = "books/file1.jpg" } };
        _bookServiceMock.Setup(s => s.GetListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Book> { book });

        // Act
        var result = await _imageManagementService.DeleteOrphanedImagesAsync();

        // Assert
        Assert.That(result, Is.EqualTo(1));
        _storageProviderMock.Verify(s => s.DeleteImage("books/orphaned.jpg"), Times.Once);
        _storageProviderMock.Verify(s => s.DeleteImage("books/file1.jpg"), Times.Never);
    }

    [Test]
    public async Task DeleteOrphanedImagesAsync_ShouldInvalidateImageStatsCache()
    {
        // Act
        await _imageManagementService.DeleteOrphanedImagesAsync();

        // Assert
        _cacheServiceMock.Verify(c => c.RemoveAsync("image-stats"), Times.Once);
    }

    [Test]
    public async Task DeleteOrphanedImagesAsync_ShouldDeleteMultipleOrphanedFiles()
    {
        // Arrange
        _storageProviderMock.Setup(s => s.ListFiles())
            .Returns(new List<(string RelativePath, long SizeBytes)>
            {
                ("books/orphaned1.jpg", 500),
                ("books/orphaned2.jpg", 300),
                ("movies/orphaned3.jpg", 700),
            });

        // Act
        var result = await _imageManagementService.DeleteOrphanedImagesAsync();

        // Assert
        Assert.That(result, Is.EqualTo(3));
        _storageProviderMock.Verify(s => s.DeleteImage(It.IsAny<string>()), Times.Exactly(3));
    }

    [Test]
    public async Task DeleteOrphanedImagesAsync_ShouldNotDeleteReferencedFiles()
    {
        // Arrange
        _storageProviderMock.Setup(s => s.ListFiles())
            .Returns(new List<(string RelativePath, long SizeBytes)>
            {
                ("books/file1.jpg", 1000),
                ("movies/file2.jpg", 2000),
            });

        var book = new Book { Id = "1", Title = "Book", Format = "Paperback", CoverImage = new Image { FilePath = "books/file1.jpg" } };
        var movie = new Movie { Id = "2", Title = "Movie", Format = "Blu-ray", CoverImage = new Image { FilePath = "movies/file2.jpg" } };
        _bookServiceMock.Setup(s => s.GetListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Book> { book });
        _movieServiceMock.Setup(s => s.GetListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Movie> { movie });

        // Act
        var result = await _imageManagementService.DeleteOrphanedImagesAsync();

        // Assert
        Assert.That(result, Is.EqualTo(0));
        _storageProviderMock.Verify(s => s.DeleteImage(It.IsAny<string>()), Times.Never);
    }
}
