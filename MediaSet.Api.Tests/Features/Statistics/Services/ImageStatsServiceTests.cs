using MediaSet.Api.Features.Statistics.Models;
using MediaSet.Api.Features.Statistics.Services;
using MediaSet.Api.Infrastructure.Caching;
using MediaSet.Api.Infrastructure.DataAccess;
using MediaSet.Api.Infrastructure.Storage;
using MediaSet.Api.Shared.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MediaSet.Api.Tests.Features.Statistics.Services;

[TestFixture]
public class ImageStatsServiceTests
{
    private Mock<IImageStorageProvider> _storageProviderMock = null!;
    private Mock<IEntityService<Book>> _bookServiceMock = null!;
    private Mock<IEntityService<Movie>> _movieServiceMock = null!;
    private Mock<IEntityService<Game>> _gameServiceMock = null!;
    private Mock<IEntityService<Music>> _musicServiceMock = null!;
    private Mock<ICacheService> _cacheServiceMock = null!;
    private Mock<IOptions<CacheSettings>> _cacheSettingsMock = null!;
    private Mock<ILogger<ImageStatsService>> _loggerMock = null!;
    private ImageStatsService _imageStatsService = null!;

    [SetUp]
    public void Setup()
    {
        _storageProviderMock = new Mock<IImageStorageProvider>();
        _bookServiceMock = new Mock<IEntityService<Book>>();
        _movieServiceMock = new Mock<IEntityService<Movie>>();
        _gameServiceMock = new Mock<IEntityService<Game>>();
        _musicServiceMock = new Mock<IEntityService<Music>>();
        _cacheServiceMock = new Mock<ICacheService>();
        _cacheSettingsMock = new Mock<IOptions<CacheSettings>>();
        _loggerMock = new Mock<ILogger<ImageStatsService>>();

        _cacheSettingsMock.Setup(x => x.Value).Returns(new CacheSettings
        {
            EnableCaching = true,
            StatsCacheDurationMinutes = 10
        });

        _cacheServiceMock.Setup(c => c.GetAsync<ImageStats>(It.IsAny<string>()))
            .ReturnsAsync((ImageStats?)null);

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

        _imageStatsService = new ImageStatsService(
            _storageProviderMock.Object,
            _bookServiceMock.Object,
            _movieServiceMock.Object,
            _gameServiceMock.Object,
            _musicServiceMock.Object,
            _cacheServiceMock.Object,
            _cacheSettingsMock.Object,
            _loggerMock.Object);
    }

    #region GetImageStatsAsync Tests

    [Test]
    public async Task GetImageStatsAsync_ShouldReturnCachedStats_WhenCacheHit()
    {
        // Arrange
        var cachedStats = new ImageStats(5, 1024, new Dictionary<string, int>(), new Dictionary<string, long>(),
            new List<BrokenImageLink>(), new List<OrphanedImageFile>(), new List<ImageLookupFailure>(), DateTime.UtcNow);
        _cacheServiceMock.Setup(c => c.GetAsync<ImageStats>(It.IsAny<string>()))
            .ReturnsAsync(cachedStats);

        // Act
        var result = await _imageStatsService.GetImageStatsAsync();

        // Assert
        Assert.That(result, Is.EqualTo(cachedStats));
        _storageProviderMock.Verify(s => s.ListFiles(), Times.Never);
    }

    [Test]
    public async Task GetImageStatsAsync_ShouldCompute_WhenCacheMiss()
    {
        // Arrange
        _cacheServiceMock.Setup(c => c.GetAsync<ImageStats>(It.IsAny<string>()))
            .ReturnsAsync((ImageStats?)null);

        // Act
        var result = await _imageStatsService.GetImageStatsAsync();

        // Assert
        Assert.That(result, Is.Not.Null);
        _storageProviderMock.Verify(s => s.ListFiles(), Times.Once);
    }

    #endregion

    #region Computation Tests

    [Test]
    public async Task GetImageStatsAsync_ShouldReturnEmptyStats_WhenNoFiles()
    {
        // Act
        var result = await _imageStatsService.GetImageStatsAsync();

        // Assert
        Assert.That(result!.TotalFiles, Is.EqualTo(0));
        Assert.That(result.TotalSizeBytes, Is.EqualTo(0));
        Assert.That(result.OrphanedFiles, Is.Empty);
        Assert.That(result.BrokenLinks, Is.Empty);
    }

    [Test]
    public async Task GetImageStatsAsync_ShouldCountTotalFilesAndSize()
    {
        // Arrange
        _storageProviderMock.Setup(s => s.ListFiles())
            .Returns(new List<(string RelativePath, long SizeBytes)>
            {
                ("books/file1.jpg", 1000),
                ("books/file2.jpg", 2000),
                ("movies/file3.jpg", 3000),
            });

        // Act
        var result = await _imageStatsService.GetImageStatsAsync();

        // Assert
        Assert.That(result!.TotalFiles, Is.EqualTo(3));
        Assert.That(result.TotalSizeBytes, Is.EqualTo(6000));
    }

    [Test]
    public async Task GetImageStatsAsync_ShouldGroupByEntityType()
    {
        // Arrange
        _storageProviderMock.Setup(s => s.ListFiles())
            .Returns(new List<(string RelativePath, long SizeBytes)>
            {
                ("books/file1.jpg", 1000),
                ("books/file2.jpg", 2000),
                ("movies/file3.jpg", 3000),
            });

        // Act
        var result = await _imageStatsService.GetImageStatsAsync();

        // Assert
        Assert.That(result!.FilesByEntityType["books"], Is.EqualTo(2));
        Assert.That(result.FilesByEntityType["movies"], Is.EqualTo(1));
        Assert.That(result.SizeByEntityType["books"], Is.EqualTo(3000));
        Assert.That(result.SizeByEntityType["movies"], Is.EqualTo(3000));
    }

    [Test]
    public async Task GetImageStatsAsync_ShouldDetectOrphanedFiles()
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

        _storageProviderMock.Setup(s => s.Exists("books/file1.jpg")).Returns(true);

        // Act
        var result = await _imageStatsService.GetImageStatsAsync();

        // Assert
        Assert.That(result!.OrphanedFiles, Has.Count.EqualTo(1));
        Assert.That(result.OrphanedFiles[0].RelativePath, Is.EqualTo("books/orphaned.jpg"));
        Assert.That(result.OrphanedFiles[0].SizeBytes, Is.EqualTo(500));
    }

    [Test]
    public async Task GetImageStatsAsync_ShouldDetectBrokenLinks_WithEntityDetails()
    {
        // Arrange
        _storageProviderMock.Setup(s => s.ListFiles())
            .Returns(new List<(string RelativePath, long SizeBytes)>());

        var book = new Book { Id = "1", Title = "Missing Cover Book", Format = "Paperback", CoverImage = new Image { FilePath = "books/missing.jpg" } };
        _bookServiceMock.Setup(s => s.GetListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Book> { book });

        _storageProviderMock.Setup(s => s.Exists("books/missing.jpg")).Returns(false);

        // Act
        var result = await _imageStatsService.GetImageStatsAsync();

        // Assert
        Assert.That(result!.BrokenLinks, Has.Count.EqualTo(1));
        Assert.That(result.BrokenLinks[0].EntityId, Is.EqualTo("1"));
        Assert.That(result.BrokenLinks[0].EntityType, Is.EqualTo("books"));
        Assert.That(result.BrokenLinks[0].Title, Is.EqualTo("Missing Cover Book"));
        Assert.That(result.BrokenLinks[0].MissingFilePath, Is.EqualTo("books/missing.jpg"));
    }

    [Test]
    public async Task GetImageStatsAsync_ShouldSkipEntities_WithNoCoverImage()
    {
        // Arrange
        var book = new Book { Id = "1", Title = "No Cover", Format = "Paperback" };
        _bookServiceMock.Setup(s => s.GetListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Book> { book });

        // Act
        var result = await _imageStatsService.GetImageStatsAsync();

        // Assert
        Assert.That(result!.BrokenLinks, Is.Empty);
        _storageProviderMock.Verify(s => s.Exists(It.IsAny<string>()), Times.Never);
    }

    [Test]
    public async Task GetImageStatsAsync_ShouldCacheResult()
    {
        // Act
        await _imageStatsService.GetImageStatsAsync();

        // Assert
        _cacheServiceMock.Verify(
            c => c.SetAsync(It.IsAny<string>(), It.IsAny<ImageStats>(), It.IsAny<TimeSpan?>()),
            Times.Once);
    }

    [Test]
    public async Task GetImageStatsAsync_ShouldSetLastUpdated()
    {
        // Arrange
        var before = DateTime.UtcNow.AddSeconds(-1);

        // Act
        var result = await _imageStatsService.GetImageStatsAsync();

        // Assert
        Assert.That(result!.LastUpdated, Is.GreaterThan(before));
    }

    #endregion
}
