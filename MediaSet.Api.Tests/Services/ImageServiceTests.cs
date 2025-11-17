using NUnit.Framework;
using Moq;
using Moq.Protected;
using MediaSet.Api.Services;
using MediaSet.Api.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Http;
using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace MediaSet.Api.Tests.Services;

[TestFixture]
public class ImageServiceTests : IDisposable
{
    private Mock<IImageStorageProvider>? _storageProviderMock;
    private Mock<ILogger<ImageService>>? _loggerMock;
    private ImageConfiguration? _imageConfig;
    private ImageService? _imageService;
    private HttpClient? _httpClient;

    [SetUp]
    public void Setup()
    {
        _storageProviderMock = new Mock<IImageStorageProvider>();
        _loggerMock = new Mock<ILogger<ImageService>>();

        _imageConfig = new ImageConfiguration
        {
            StoragePath = "/test/images",
            MaxFileSizeMb = 5,
            AllowedMimeTypes = "image/jpeg,image/png",
            HttpTimeoutSeconds = 30,
            MaxDownloadSizeMb = 10,
            StripExifData = true
        };

        _httpClient = new HttpClient();
        _httpClient.Timeout = TimeSpan.FromSeconds(_imageConfig.HttpTimeoutSeconds);

        _imageService = new ImageService(
            _storageProviderMock!.Object,
            Options.Create(_imageConfig!),
            _httpClient,
            _loggerMock!.Object);
    }

    [TearDown]
    public void TearDown()
    {
        _httpClient?.Dispose();
    }

    public void Dispose()
    {
        _httpClient?.Dispose();
        GC.SuppressFinalize(this);
    }

    #region GetImageStreamAsync Tests

    [Test]
    public async Task GetImageStreamAsync_WithValidPath_ReturnsStream()
    {
        // Arrange
        var imagePath = "books/123-abc123.jpg";
        var imageData = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 };
        var expectedStream = new MemoryStream(imageData);

        _storageProviderMock!
            .Setup(sp => sp.GetImageAsync(imagePath, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedStream);

        // Act
        var result = await _imageService!.GetImageStreamAsync(imagePath, CancellationToken.None);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result, Is.EqualTo(expectedStream));
    }

    [Test]
    public async Task GetImageStreamAsync_WithMissingImage_ReturnsNull()
    {
        // Arrange
        var imagePath = "books/123-abc123.jpg";

        _storageProviderMock!
            .Setup(sp => sp.GetImageAsync(imagePath, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Stream?)null);

        // Act
        var result = await _imageService!.GetImageStreamAsync(imagePath, CancellationToken.None);

        // Assert
        Assert.That(result, Is.Null);
    }

    #endregion

    #region DeleteImageAsync Tests

    [Test]
    public void DeleteImageAsync_WithValidPath_DeletesImage()
    {
        // Arrange
        var imagePath = "books/123-abc123.jpg";

        _storageProviderMock!.Setup(sp => sp.DeleteImage(imagePath)).Verifiable();

        // Act
        _imageService!.DeleteImageAsync(imagePath);

        // Assert
        _storageProviderMock.Verify(sp => sp.DeleteImage(imagePath), Times.Once);
    }

    #endregion

    #region SaveImageAsync Tests

    [Test]
    public async Task SaveImageAsync_WithValidFile_SavesSuccessfully()
    {
        // Arrange
        var entityType = "books";
        var entityId = "123";
        var fileName = "cover.jpg";
        var fileContent = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 };
        var formFile = CreateFormFile(fileName, fileContent, "image/jpeg");

        _storageProviderMock!
            .Setup(sp => sp.SaveImageAsync(It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _imageService!.SaveImageAsync(formFile, entityType, entityId, CancellationToken.None);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.FileName, Is.EqualTo(fileName));
        Assert.That(result.ContentType, Is.EqualTo("image/jpeg"));
        Assert.That(result.FileSize, Is.EqualTo(fileContent.Length));
        Assert.That(result.FilePath, Does.StartWith(entityType));
        _storageProviderMock.Verify(sp => sp.SaveImageAsync(It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public void SaveImageAsync_WithNullFile_ThrowsArgumentException()
    {
        // Arrange
        var entityType = "books";
        var entityId = "123";

        // Act & Assert
        var ex = Assert.ThrowsAsync<ArgumentException>(async () =>
            await _imageService!.SaveImageAsync(null!, entityType, entityId, CancellationToken.None));
        Assert.That(ex?.ParamName, Is.EqualTo("file"));
    }

    [Test]
    public void SaveImageAsync_WithUnsupportedMimeType_ThrowsArgumentException()
    {
        // Arrange
        var entityType = "books";
        var entityId = "123";
        var fileName = "file.pdf";
        var fileContent = new byte[] { 0x25, 0x50, 0x44, 0x46 };
        var formFile = CreateFormFile(fileName, fileContent, "application/pdf");

        // Act & Assert
        var ex = Assert.ThrowsAsync<ArgumentException>(async () =>
            await _imageService!.SaveImageAsync(formFile, entityType, entityId, CancellationToken.None));
        Assert.That(ex?.Message, Does.Contain("Unsupported file type"));
    }

    [Test]
    public void SaveImageAsync_WithFileSizeExceedsLimit_ThrowsArgumentException()
    {
        // Arrange
        var entityType = "books";
        var entityId = "123";
        var fileName = "large.jpg";
        var fileSize = (5 * 1024 * 1024) + 1; // Exceeds 5MB limit
        var fileContent = new byte[fileSize];
        var formFile = CreateFormFile(fileName, fileContent, "image/jpeg");

        // Act & Assert
        var ex = Assert.ThrowsAsync<ArgumentException>(async () =>
            await _imageService!.SaveImageAsync(formFile, entityType, entityId, CancellationToken.None));
        Assert.That(ex?.Message, Does.Contain("exceeds maximum allowed size"));
    }

    [Test]
    public void SaveImageAsync_WithNullEntityType_ThrowsArgumentException()
    {
        // Arrange
        var fileContent = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 };
        var formFile = CreateFormFile("cover.jpg", fileContent, "image/jpeg");

        // Act & Assert
        Assert.ThrowsAsync<ArgumentException>(async () =>
            await _imageService!.SaveImageAsync(formFile, null!, "123", CancellationToken.None));
    }

    #endregion

    #region DownloadAndSaveImageAsync Tests

    [Test]
    public async Task DownloadAndSaveImageAsync_WithValidUrl_DownloadsSavesSuccessfully()
    {
        // Arrange
        var imageUrl = "https://example.com/image.jpg";
        var entityType = "movies";
        var entityId = "456";
        var imageData = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 };

        var content = new ByteArrayContent(imageData);
        content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/jpeg");
        content.Headers.ContentLength = imageData.Length;

        var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = content };

        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(r => r.RequestUri != null && r.RequestUri.ToString() == imageUrl),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(response);

        using (var httpClient = new HttpClient(handlerMock.Object))
        {
            var imageService = new ImageService(
                _storageProviderMock!.Object,
                Options.Create(_imageConfig!),
                httpClient,
                _loggerMock!.Object);

            _storageProviderMock
                .Setup(sp => sp.SaveImageAsync(It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Act
            var result = await imageService.DownloadAndSaveImageAsync(imageUrl, entityType, entityId, CancellationToken.None);

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result.ContentType, Is.EqualTo("image/jpeg"));
            Assert.That(result.FileSize, Is.EqualTo(imageData.Length));
            Assert.That(result.FilePath, Does.StartWith(entityType));
            _storageProviderMock.Verify(sp => sp.SaveImageAsync(It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        }
    }

    [Test]
    public void DownloadAndSaveImageAsync_WithNullUrl_ThrowsArgumentException()
    {
        // Act & Assert
        var ex = Assert.ThrowsAsync<ArgumentException>(async () =>
            await _imageService!.DownloadAndSaveImageAsync(null!, "movies", "456", CancellationToken.None));
        Assert.That(ex?.ParamName, Is.EqualTo("imageUrl"));
    }

    [Test]
    public void DownloadAndSaveImageAsync_WithInvalidUrl_ThrowsArgumentException()
    {
        // Arrange
        var invalidUrl = "not-a-valid-url";

        // Act & Assert
        var ex = Assert.ThrowsAsync<ArgumentException>(async () =>
            await _imageService!.DownloadAndSaveImageAsync(invalidUrl, "movies", "456", CancellationToken.None));
        Assert.That(ex?.Message, Does.Contain("valid HTTP or HTTPS URL"));
    }

    [Test]
    public void DownloadAndSaveImageAsync_WithFtpUrl_ThrowsArgumentException()
    {
        // Arrange
        var ftpUrl = "ftp://example.com/image.jpg";

        // Act & Assert
        var ex = Assert.ThrowsAsync<ArgumentException>(async () =>
            await _imageService!.DownloadAndSaveImageAsync(ftpUrl, "movies", "456", CancellationToken.None));
        Assert.That(ex?.Message, Does.Contain("valid HTTP or HTTPS URL"));
    }

    [Test]
    public void DownloadAndSaveImageAsync_WithUnsupportedMimeType_ThrowsArgumentException()
    {
        // Arrange
        var imageUrl = "https://example.com/file.pdf";
        var entityType = "movies";
        var entityId = "456";
        var pdfData = new byte[] { 0x25, 0x50, 0x44, 0x46 };

        var content = new ByteArrayContent(pdfData);
        content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/pdf");

        var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = content };

        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(response);

        using (var httpClient = new HttpClient(handlerMock.Object))
        {
            var imageService = new ImageService(
                _storageProviderMock!.Object,
                Options.Create(_imageConfig!),
                httpClient,
                _loggerMock!.Object);

            // Act & Assert
            var ex = Assert.ThrowsAsync<ArgumentException>(async () =>
                await imageService.DownloadAndSaveImageAsync(imageUrl, entityType, entityId, CancellationToken.None));
            Assert.That(ex?.Message, Does.Contain("Unsupported image type"));
        }
    }

    [Test]
    public void DownloadAndSaveImageAsync_WithDownloadSizeExceedsLimit_ThrowsArgumentException()
    {
        // Arrange
        var imageUrl = "https://example.com/large-image.jpg";
        var entityType = "movies";
        var entityId = "456";
        var largeData = new byte[(10 * 1024 * 1024) + 1]; // Exceeds 10MB limit

        var content = new ByteArrayContent(largeData);
        content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/jpeg");
        content.Headers.ContentLength = largeData.Length;

        var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = content };

        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(response);

        using (var httpClient = new HttpClient(handlerMock.Object))
        {
            var imageService = new ImageService(
                _storageProviderMock!.Object,
                Options.Create(_imageConfig!),
                httpClient,
                _loggerMock!.Object);

            // Act & Assert
            var ex = Assert.ThrowsAsync<ArgumentException>(async () =>
                await imageService.DownloadAndSaveImageAsync(imageUrl, entityType, entityId, CancellationToken.None));
            Assert.That(ex?.Message, Does.Contain("exceeds maximum allowed size"));
        }
    }

    #endregion

    /// <summary>
    /// Helper method to create a FormFile from raw data for testing.
    /// Note: The returned FormFile manages the MemoryStream lifetime and will dispose it.
    /// </summary>
    private static IFormFile CreateFormFile(string fileName, byte[] fileContent, string contentType)
    {
        var stream = new MemoryStream(fileContent);
        return new FormFile(stream, 0, fileContent.Length, "file", fileName)
        {
            Headers = new HeaderDictionary(),
            ContentType = contentType
        };
    }
}
