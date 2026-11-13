using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using MediaSet.Api.Features.Images.Services;
using MediaSet.Api.Infrastructure.Lookup;
using MediaSet.Api.Infrastructure.Lookup.Clients.GoogleBooks;
using MediaSet.Api.Infrastructure.Lookup.Models;
using MediaSet.Api.Infrastructure.Lookup.Strategies;
using MediaSet.Api.Infrastructure.Storage;
using MediaSet.Api.Shared.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NUnit.Framework;

namespace MediaSet.Api.Tests.Features.Images.Services;

[TestFixture]
public class ImageGoogleBooksFallbackTests
{
    private const string Isbn = "9780441013593";
    private const string OpenLibraryIsbnCover = $"https://covers.openlibrary.org/b/isbn/{Isbn}-L.jpg?default=false";
    private const string GoogleCover = "https://books.google.com/books/content?id=vol1&printsec=frontcover&img=1&zoom=1";

    private Mock<ILookupStrategy<BookResponse>> _strategy = null!;
    private Mock<IImageService> _images = null!;
    private Mock<IGoogleBooksClient> _google = null!;
    private ImageLookupService _service = null!;
    private static readonly Image _saved = new() { FilePath = "books/1.jpg" };

    [SetUp]
    public void Setup()
    {
        _strategy = new Mock<ILookupStrategy<BookResponse>>();
        _strategy.Setup(s => s.CanHandle(MediaTypes.Books, It.IsAny<IdentifierType>())).Returns(true);
        _strategy.Setup(s => s.LookupAsync(It.IsAny<IdentifierType>(), It.IsAny<IReadOnlyDictionary<string, string>>(),
            It.IsAny<CancellationToken>())).ReturnsAsync([]);
        var services = new Mock<IServiceProvider>();
        services.Setup(sp => sp.GetService(typeof(IEnumerable<ILookupStrategy<BookResponse>>)))
            .Returns(new[] { _strategy.Object });
        _images = new Mock<IImageService>();
        _images.Setup(s => s.DownloadAndSaveImageAsync(It.IsAny<string>(), "Books", "1", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Not Found", null, HttpStatusCode.NotFound));
        _google = new Mock<IGoogleBooksClient>();
        _service = new ImageLookupService(new LookupStrategyFactory(services.Object), _images.Object,
            NullLogger<ImageLookupService>.Instance, _google.Object);
    }

    private static Book Book() => new() { Id = "1", Title = "Dune", ISBN = Isbn };

    [Test]
    public async Task LookupAndSaveImageAsync_OpenLibraryCover404_DownloadsGoogleBooksCover()
    {
        _google.Setup(g => g.GetCoverUrlByIsbnAsync(Isbn, It.IsAny<CancellationToken>())).ReturnsAsync(GoogleCover);
        _images.Setup(s => s.DownloadAndSaveImageAsync(GoogleCover, "Books", "1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(_saved);

        var result = await _service.LookupAndSaveImageAsync(Book(), MediaTypes.Books, CancellationToken.None);

        Assert.That(result.Success, Is.True);
        Assert.That(result.Provider, Is.EqualTo("GoogleBooks"));
        Assert.That(result.ImageUrl, Is.EqualTo(GoogleCover));
        Assert.That(result.AttemptedStrategy, Is.EqualTo("Books:Isbn"));
        Assert.That(result.ConfirmedMatch!.Provider, Is.EqualTo("GoogleBooks"));
        Assert.That(result.ConfirmedMatch.ProviderId, Is.EqualTo(Isbn));
        Assert.That(result.ConfirmedMatch.ImageUrl, Is.EqualTo(GoogleCover));
        _images.Verify(s => s.DownloadAndSaveImageAsync(OpenLibraryIsbnCover, "Books", "1", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task LookupAndSaveImageAsync_OpenLibraryCoverExists_DoesNotCallGoogleBooks()
    {
        _images.Setup(s => s.DownloadAndSaveImageAsync(OpenLibraryIsbnCover, "Books", "1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(_saved);

        var result = await _service.LookupAndSaveImageAsync(Book(), MediaTypes.Books, CancellationToken.None);

        Assert.That(result.Success, Is.True);
        Assert.That(result.Provider, Is.EqualTo("OpenLibrary"));
        _google.Verify(g => g.GetCoverUrlByIsbnAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task LookupAndSaveImageAsync_GoogleBooksHasNoCover_KeepsOpenLibraryMissOutcome()
    {
        _google.Setup(g => g.GetCoverUrlByIsbnAsync(Isbn, It.IsAny<CancellationToken>())).ReturnsAsync((string?)null);

        var result = await _service.LookupAndSaveImageAsync(Book(), MediaTypes.Books, CancellationToken.None);

        Assert.That(result.Success, Is.False);
        Assert.That(result.Outcome, Is.EqualTo(ImageLookupOutcome.NoMatch));
        _google.Verify(g => g.GetCoverUrlByIsbnAsync(Isbn, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task LookupAndSaveImageAsync_GoogleBooksCover404_ReturnsFailure()
    {
        _google.Setup(g => g.GetCoverUrlByIsbnAsync(Isbn, It.IsAny<CancellationToken>())).ReturnsAsync(GoogleCover);

        var result = await _service.LookupAndSaveImageAsync(Book(), MediaTypes.Books, CancellationToken.None);

        Assert.That(result.Success, Is.False);
        _images.Verify(s => s.DownloadAndSaveImageAsync(GoogleCover, "Books", "1", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task LookupAndSaveImageAsync_GoogleBooksRateLimited_ReturnsRetryableQuotaOutcome()
    {
        _google.Setup(g => g.GetCoverUrlByIsbnAsync(Isbn, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ProviderLookupException("GoogleBooks", ImageLookupOutcome.QuotaLimited, "rate limited"));

        var result = await _service.LookupAndSaveImageAsync(Book(), MediaTypes.Books, CancellationToken.None);

        Assert.That(result.Success, Is.False);
        Assert.That(result.Outcome, Is.EqualTo(ImageLookupOutcome.QuotaLimited));
        Assert.That(result.Provider, Is.EqualTo("GoogleBooks"));
        Assert.That(result.PermanentFailure, Is.False);
    }

    [Test]
    public async Task LookupAndSaveImageAsync_BookWithoutIsbn_DoesNotCallGoogleBooks()
    {
        await _service.LookupAndSaveImageAsync(new Book { Id = "1", Title = "Dune" }, MediaTypes.Books, CancellationToken.None);

        _google.Verify(g => g.GetCoverUrlByIsbnAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
