using MediaSet.Api.Features.Images.Services;
using MediaSet.Api.Infrastructure.Lookup.Models;
using MediaSet.Api.Infrastructure.Lookup.Strategies;
using MediaSet.Api.Infrastructure.Storage;
using MediaSet.Api.Shared.Models;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MediaSet.Api.Tests.Features.Images.Services;

[TestFixture]
public class ImageLookupServiceTests
{
    private Mock<IServiceProvider> _serviceProviderMock = null!;
    private Mock<IImageService> _imageServiceMock = null!;
    private Mock<ILogger<ImageLookupService>> _loggerMock = null!;
    private Mock<ILookupStrategy<BookResponse>> _bookStrategyMock = null!;
    private Mock<ILookupStrategy<MovieResponse>> _movieStrategyMock = null!;
    private LookupStrategyFactory _strategyFactory = null!;
    private ImageLookupService _service = null!;

    private static readonly Image _savedImage = new() { FilePath = "test/image.jpg" };

    [SetUp]
    public void Setup()
    {
        _bookStrategyMock = new Mock<ILookupStrategy<BookResponse>>();
        _movieStrategyMock = new Mock<ILookupStrategy<MovieResponse>>();

        _serviceProviderMock = new Mock<IServiceProvider>();
        _serviceProviderMock
            .Setup(sp => sp.GetService(typeof(IEnumerable<ILookupStrategy<BookResponse>>)))
            .Returns(new ILookupStrategy<BookResponse>[] { _bookStrategyMock.Object });
        _serviceProviderMock
            .Setup(sp => sp.GetService(typeof(IEnumerable<ILookupStrategy<MovieResponse>>)))
            .Returns(new ILookupStrategy<MovieResponse>[] { _movieStrategyMock.Object });

        _imageServiceMock = new Mock<IImageService>();
        _loggerMock = new Mock<ILogger<ImageLookupService>>();

        _strategyFactory = new LookupStrategyFactory(_serviceProviderMock.Object);
        _service = new ImageLookupService(_strategyFactory, _imageServiceMock.Object, _loggerMock.Object);
    }

    #region Id validation

    [Test]
    public async Task LookupAndSaveImageAsync_ReturnsPermanentFailure_WhenEntityHasNoId()
    {
        var book = new Book { Id = null, Title = "Dune", ISBN = "9780441013593" };

        var result = await _service.LookupAndSaveImageAsync(book, MediaTypes.Books, CancellationToken.None);

        Assert.That(result.Success, Is.False);
        Assert.That(result.PermanentFailure, Is.True);
    }

    #endregion

    #region Identifier priority — Books

    [Test]
    public async Task LookupAndSaveImageAsync_UsesIsbn_WhenBookHasIsbn()
    {
        // Arrange
        var book = new Book { Id = "1", Title = "Dune", ISBN = "9780441013593" };

        _bookStrategyMock
            .Setup(s => s.CanHandle(MediaTypes.Books, IdentifierType.Isbn))
            .Returns(true);
        _bookStrategyMock
            .Setup(s => s.LookupAsync(
                IdentifierType.Isbn,
                It.Is<IReadOnlyDictionary<string, string>>(d => d["isbn"] == "9780441013593"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<BookResponse>
            {
                new("Dune", "", [], 0, [], "", [], ImageUrl: "https://example.com/dune.jpg")
            });
        _imageServiceMock
            .Setup(s => s.DownloadAndSaveImageAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(_savedImage);

        // Act
        var result = await _service.LookupAndSaveImageAsync(book, MediaTypes.Books, CancellationToken.None);

        // Assert
        Assert.That(result.Success, Is.True);
        _bookStrategyMock.Verify(s => s.LookupAsync(
            IdentifierType.Isbn,
            It.Is<IReadOnlyDictionary<string, string>>(d => d.ContainsKey("isbn")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task LookupAndSaveImageAsync_FallsBackToTitle_WhenBookIsbnIsEmpty()
    {
        // Arrange
        var book = new Book { Id = "1", Title = "Dune", ISBN = "" };

        _bookStrategyMock
            .Setup(s => s.CanHandle(MediaTypes.Books, IdentifierType.Entity))
            .Returns(true);
        _bookStrategyMock
            .Setup(s => s.LookupAsync(
                IdentifierType.Entity,
                It.Is<IReadOnlyDictionary<string, string>>(d => d["title"] == "Dune"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<BookResponse>
            {
                new("Dune", "", [], 0, [], "", [], ImageUrl: "https://example.com/dune.jpg")
            });
        _imageServiceMock
            .Setup(s => s.DownloadAndSaveImageAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(_savedImage);

        // Act
        var result = await _service.LookupAndSaveImageAsync(book, MediaTypes.Books, CancellationToken.None);

        // Assert
        Assert.That(result.Success, Is.True);
        _bookStrategyMock.Verify(s => s.LookupAsync(
            IdentifierType.Entity,
            It.Is<IReadOnlyDictionary<string, string>>(d => d.ContainsKey("title") && d["title"] == "Dune"),
            It.IsAny<CancellationToken>()), Times.Once);
        _bookStrategyMock.Verify(s => s.LookupAsync(
            IdentifierType.Isbn,
            It.IsAny<IReadOnlyDictionary<string, string>>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task LookupAndSaveImageAsync_ReturnsPermanentFailure_WhenAllBookIdentifierFieldsEmpty()
    {
        var book = new Book { Id = "1", Title = "", ISBN = "" };

        var result = await _service.LookupAndSaveImageAsync(book, MediaTypes.Books, CancellationToken.None);

        Assert.That(result.Success, Is.False);
        Assert.That(result.PermanentFailure, Is.True);
        Assert.That(result.ErrorMessage, Does.Contain("lookup identifier"));
    }

    #endregion

    #region Identifier priority — Movies

    [Test]
    public async Task LookupAndSaveImageAsync_UsesUpc_WhenMovieHasBarcode()
    {
        // Arrange
        var movie = new Movie { Id = "2", Title = "Inception", Barcode = "883929149575" };

        _movieStrategyMock
            .Setup(s => s.CanHandle(MediaTypes.Movies, IdentifierType.Upc))
            .Returns(true);
        _movieStrategyMock
            .Setup(s => s.LookupAsync(
                IdentifierType.Upc,
                It.Is<IReadOnlyDictionary<string, string>>(d => d["upc"] == "883929149575"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<MovieResponse>
            {
                new("Inception", [], [], "", "", null, "", "", ImageUrl: "https://example.com/inception.jpg")
            });
        _imageServiceMock
            .Setup(s => s.DownloadAndSaveImageAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(_savedImage);

        // Act
        var result = await _service.LookupAndSaveImageAsync(movie, MediaTypes.Movies, CancellationToken.None);

        // Assert
        Assert.That(result.Success, Is.True);
        _movieStrategyMock.Verify(s => s.LookupAsync(
            IdentifierType.Upc,
            It.Is<IReadOnlyDictionary<string, string>>(d => d.ContainsKey("upc") && d["upc"] == "883929149575"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task LookupAndSaveImageAsync_FallsBackToTitle_WhenMovieBarcodeIsEmpty()
    {
        // Arrange
        var movie = new Movie { Id = "2", Title = "Inception", Barcode = "" };

        _movieStrategyMock
            .Setup(s => s.CanHandle(MediaTypes.Movies, IdentifierType.Entity))
            .Returns(true);
        _movieStrategyMock
            .Setup(s => s.LookupAsync(
                IdentifierType.Entity,
                It.Is<IReadOnlyDictionary<string, string>>(d => d["title"] == "Inception"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<MovieResponse>
            {
                new("Inception", [], [], "", "", null, "", "", ImageUrl: "https://example.com/inception.jpg")
            });
        _imageServiceMock
            .Setup(s => s.DownloadAndSaveImageAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(_savedImage);

        // Act
        var result = await _service.LookupAndSaveImageAsync(movie, MediaTypes.Movies, CancellationToken.None);

        // Assert
        Assert.That(result.Success, Is.True);
        _movieStrategyMock.Verify(s => s.LookupAsync(
            IdentifierType.Entity,
            It.Is<IReadOnlyDictionary<string, string>>(d => d.ContainsKey("title") && d["title"] == "Inception"),
            It.IsAny<CancellationToken>()), Times.Once);
        _movieStrategyMock.Verify(s => s.LookupAsync(
            IdentifierType.Upc,
            It.IsAny<IReadOnlyDictionary<string, string>>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task LookupAndSaveImageAsync_ReturnsPermanentFailure_WhenAllMovieIdentifierFieldsEmpty()
    {
        var movie = new Movie { Id = "2", Title = "", Barcode = "" };

        var result = await _service.LookupAndSaveImageAsync(movie, MediaTypes.Movies, CancellationToken.None);

        Assert.That(result.Success, Is.False);
        Assert.That(result.PermanentFailure, Is.True);
    }

    #endregion

    #region Existing ImageUrl

    [Test]
    public async Task LookupAndSaveImageAsync_UsesExistingImageUrl_BeforeStrategyLookup()
    {
        // Arrange
        var book = new Book { Id = "1", Title = "Dune", ISBN = "9780441013593", ImageUrl = "https://existing.com/cover.jpg" };

        _imageServiceMock
            .Setup(s => s.DownloadAndSaveImageAsync("https://existing.com/cover.jpg", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(_savedImage);

        // Act
        var result = await _service.LookupAndSaveImageAsync(book, MediaTypes.Books, CancellationToken.None);

        // Assert
        Assert.That(result.Success, Is.True);
        Assert.That(result.ImageUrl, Is.EqualTo("https://existing.com/cover.jpg"));
        _bookStrategyMock.Verify(s => s.LookupAsync(
            It.IsAny<IdentifierType>(),
            It.IsAny<IReadOnlyDictionary<string, string>>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task LookupAndSaveImageAsync_FallsBackToStrategyLookup_WhenExistingImageUrlFails()
    {
        // Arrange
        var book = new Book { Id = "1", Title = "Dune", ISBN = "9780441013593", ImageUrl = "https://broken.com/cover.jpg" };

        _imageServiceMock
            .Setup(s => s.DownloadAndSaveImageAsync("https://broken.com/cover.jpg", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new System.Exception("Download failed"));
        _bookStrategyMock
            .Setup(s => s.CanHandle(MediaTypes.Books, IdentifierType.Isbn))
            .Returns(true);
        _bookStrategyMock
            .Setup(s => s.LookupAsync(It.IsAny<IdentifierType>(), It.IsAny<IReadOnlyDictionary<string, string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<BookResponse>
            {
                new("Dune", "", [], 0, [], "", [], ImageUrl: "https://example.com/dune.jpg")
            });
        _imageServiceMock
            .Setup(s => s.DownloadAndSaveImageAsync("https://example.com/dune.jpg", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(_savedImage);

        // Act
        var result = await _service.LookupAndSaveImageAsync(book, MediaTypes.Books, CancellationToken.None);

        // Assert
        Assert.That(result.Success, Is.True);
        _bookStrategyMock.Verify(s => s.LookupAsync(
            It.IsAny<IdentifierType>(),
            It.IsAny<IReadOnlyDictionary<string, string>>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region Strategy failure cases

    [Test]
    public async Task LookupAndSaveImageAsync_ReturnsFailure_WhenStrategyReturnsNoResults()
    {
        // Arrange
        var book = new Book { Id = "1", Title = "Dune", ISBN = "9780441013593" };

        _bookStrategyMock
            .Setup(s => s.CanHandle(MediaTypes.Books, IdentifierType.Isbn))
            .Returns(true);
        _bookStrategyMock
            .Setup(s => s.LookupAsync(It.IsAny<IdentifierType>(), It.IsAny<IReadOnlyDictionary<string, string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<BookResponse>());

        // Act
        var result = await _service.LookupAndSaveImageAsync(book, MediaTypes.Books, CancellationToken.None);

        // Assert
        Assert.That(result.Success, Is.False);
        Assert.That(result.PermanentFailure, Is.False);
        Assert.That(result.ErrorMessage, Does.Contain("No image URL"));
    }

    [Test]
    public async Task LookupAndSaveImageAsync_ReturnsPermanentFailure_WhenNoStrategyAvailable()
    {
        // Arrange
        var movie = new Movie { Id = "2", Title = "Inception", Barcode = "883929149575" };

        _movieStrategyMock
            .Setup(s => s.CanHandle(It.IsAny<MediaTypes>(), It.IsAny<IdentifierType>()))
            .Returns(false);

        // Act
        var result = await _service.LookupAndSaveImageAsync(movie, MediaTypes.Movies, CancellationToken.None);

        // Assert
        Assert.That(result.Success, Is.False);
        Assert.That(result.PermanentFailure, Is.True);
    }

    #endregion
}
