using NUnit.Framework;
using Moq;
using MediaSet.Api.Services;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MediaSet.Api.Tests.Services;

[TestFixture]
public class LocalFileStorageProviderTests
{
    private Mock<ILogger<LocalFileStorageProvider>>? _loggerMock;
    private string? _testStoragePath;
    private LocalFileStorageProvider? _storageProvider;

    [SetUp]
    public void Setup()
    {
        _loggerMock = new Mock<ILogger<LocalFileStorageProvider>>();
        _testStoragePath = Path.Combine(Path.GetTempPath(), $"mediaset-test-{Guid.NewGuid()}");
        
        if (!Directory.Exists(_testStoragePath))
        {
            Directory.CreateDirectory(_testStoragePath);
        }

        _storageProvider = new LocalFileStorageProvider(_testStoragePath, _loggerMock!.Object);
    }

    [TearDown]
    public void Cleanup()
    {
        if (_testStoragePath != null && Directory.Exists(_testStoragePath))
        {
            Directory.Delete(_testStoragePath, recursive: true);
        }
    }

    #region SaveImageAsync Tests

    [Test]
    public async Task SaveImageAsync_WithValidData_SavesFile()
    {
        // Arrange
        var imageData = Encoding.UTF8.GetBytes("test image data");
        var relativePath = "books/123-abc.jpg";

        // Act
        await _storageProvider!.SaveImageAsync(imageData, relativePath, CancellationToken.None);

        // Assert
        var fullPath = Path.Combine(_testStoragePath!, relativePath);
        Assert.That(File.Exists(fullPath), Is.True);
        var savedData = await File.ReadAllBytesAsync(fullPath);
        Assert.That(savedData, Is.EqualTo(imageData));
    }

    [Test]
    public async Task SaveImageAsync_CreatesDirectory_IfNotExists()
    {
        // Arrange
        var imageData = Encoding.UTF8.GetBytes("test image data");
        var relativePath = "books/subfolder/123-abc.jpg";
        var expectedDirectory = Path.Combine(_testStoragePath!, "books", "subfolder");

        // Act
        await _storageProvider!.SaveImageAsync(imageData, relativePath, CancellationToken.None);

        // Assert
        Assert.That(Directory.Exists(expectedDirectory), Is.True);
    }

    #endregion

    #region GetImageAsync Tests

    [Test]
    public async Task GetImageAsync_WithExistingFile_ReturnsStream()
    {
        // Arrange
        var expectedData = Encoding.UTF8.GetBytes("test image data");
        var relativePath = "books/123-abc.jpg";
        var fullPath = Path.Combine(_testStoragePath!, relativePath);
        
        // Create the file
        var directory = Path.GetDirectoryName(fullPath);
        if (directory != null)
        {
            Directory.CreateDirectory(directory);
        }
        await File.WriteAllBytesAsync(fullPath, expectedData);

        // Act
        var result = await _storageProvider!.GetImageAsync(relativePath, CancellationToken.None);

        // Assert
        Assert.That(result, Is.Not.Null);
        var streamData = new MemoryStream();
        if (result != null)
        {
            await result.CopyToAsync(streamData);
        }
        Assert.That(streamData.ToArray(), Is.EqualTo(expectedData));
    }

    [Test]
    public async Task GetImageAsync_WithMissingFile_ReturnsNull()
    {
        // Arrange
        var relativePath = "books/nonexistent.jpg";

        // Act
        var result = await _storageProvider!.GetImageAsync(relativePath, CancellationToken.None);

        // Assert
        Assert.That(result, Is.Null);
    }

    #endregion

    #region DeleteImage Tests

    [Test]
    public async Task DeleteImage_WithExistingFile_DeletesFile()
    {
        // Arrange
        var imageData = Encoding.UTF8.GetBytes("test image data");
        var relativePath = "books/123-abc.jpg";
        var fullPath = Path.Combine(_testStoragePath!, relativePath);
        
        // Create the file
        var directory = Path.GetDirectoryName(fullPath);
        if (directory != null)
        {
            Directory.CreateDirectory(directory);
        }
        await File.WriteAllBytesAsync(fullPath, imageData);
        Assert.That(File.Exists(fullPath), Is.True);

        // Act
        _storageProvider!.DeleteImage(relativePath);

        // Assert
        Assert.That(File.Exists(fullPath), Is.False);
    }

    [Test]
    public void DeleteImage_WithMissingFile_DoesNotThrow()
    {
        // Arrange
        var relativePath = "books/nonexistent.jpg";

        // Act & Assert
        Assert.DoesNotThrow(() =>
            _storageProvider!.DeleteImage(relativePath)
        );
    }

    #endregion

    #region Exists Tests

    [Test]
    public async Task Exists_WithExistingFile_ReturnsTrue()
    {
        // Arrange
        var imageData = Encoding.UTF8.GetBytes("test");
        var relativePath = "books/123-abc.jpg";
        var fullPath = Path.Combine(_testStoragePath!, relativePath);
        
        var directory = Path.GetDirectoryName(fullPath);
        if (directory != null)
        {
            Directory.CreateDirectory(directory);
        }
        await File.WriteAllBytesAsync(fullPath, imageData);

        // Act
        var result = _storageProvider!.Exists(relativePath);

        // Assert
        Assert.That(result, Is.True);
    }

    [Test]
    public void Exists_WithMissingFile_ReturnsFalse()
    {
        // Act
        var result = _storageProvider!.Exists("books/nonexistent.jpg");

        // Assert
        Assert.That(result, Is.False);
    }

    #endregion
}
