using MediaSet.Api.Features.Lookup.Models;
using MediaSet.Api.Features.Entities.Models;
using MediaSet.Api.Infrastructure.Lookup;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;
using System.Threading;
using System.Threading.Tasks;

namespace MediaSet.Api.Tests.Infrastructure.Lookup;

[TestFixture]
public class BookLookupStrategyTests
{
    private Mock<IOpenLibraryClient> _openLibraryClientMock = null!;
    private Mock<IUpcItemDbClient> _upcItemDbClientMock = null!;
    private Mock<ILogger<BookLookupStrategy>> _loggerMock = null!;
    private BookLookupStrategy _strategy = null!;

    [SetUp]
    public void SetUp()
    {
        _openLibraryClientMock = new Mock<IOpenLibraryClient>();
        _upcItemDbClientMock = new Mock<IUpcItemDbClient>();
        _loggerMock = new Mock<ILogger<BookLookupStrategy>>();
        _strategy = new BookLookupStrategy(
            _openLibraryClientMock.Object,
            _upcItemDbClientMock.Object,
            _loggerMock.Object);
    }

    #region CanHandle Tests

    [Test]
    public void CanHandle_WithBooksAndIsbn_ReturnsTrue()
    {
        var result = _strategy.CanHandle(MediaTypes.Books, IdentifierType.Isbn);

        Assert.That(result, Is.True);
    }

    [Test]
    public void CanHandle_WithBooksAndLccn_ReturnsTrue()
    {
        var result = _strategy.CanHandle(MediaTypes.Books, IdentifierType.Lccn);

        Assert.That(result, Is.True);
    }

    [Test]
    public void CanHandle_WithBooksAndOclc_ReturnsTrue()
    {
        var result = _strategy.CanHandle(MediaTypes.Books, IdentifierType.Oclc);

        Assert.That(result, Is.True);
    }

    [Test]
    public void CanHandle_WithBooksAndOlid_ReturnsTrue()
    {
        var result = _strategy.CanHandle(MediaTypes.Books, IdentifierType.Olid);

        Assert.That(result, Is.True);
    }

    [Test]
    public void CanHandle_WithBooksAndUpc_ReturnsTrue()
    {
        var result = _strategy.CanHandle(MediaTypes.Books, IdentifierType.Upc);

        Assert.That(result, Is.True);
    }

    [Test]
    public void CanHandle_WithBooksAndEan_ReturnsTrue()
    {
        var result = _strategy.CanHandle(MediaTypes.Books, IdentifierType.Ean);

        Assert.That(result, Is.True);
    }

    [Test]
    public void CanHandle_WithMoviesAndIsbn_ReturnsFalse()
    {
        var result = _strategy.CanHandle(MediaTypes.Movies, IdentifierType.Isbn);

        Assert.That(result, Is.False);
    }

    [Test]
    public void CanHandle_WithMoviesAndUpc_ReturnsFalse()
    {
        var result = _strategy.CanHandle(MediaTypes.Movies, IdentifierType.Upc);

        Assert.That(result, Is.False);
    }

    #endregion

    #region LookupAsync - ISBN Tests

    [Test]
    public async Task LookupAsync_WithIsbn_CallsGetReadableBookByIsbnAsync()
    {
        var isbn = "9780134685991";
        var expectedResponse = CreateBookResponse("Clean Code");

        _openLibraryClientMock
            .Setup(x => x.GetReadableBookByIsbnAsync(isbn, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResponse);

        var result = await _strategy.LookupAsync(IdentifierType.Isbn, isbn, CancellationToken.None);

        Assert.That(result, Is.EqualTo(expectedResponse));
        _openLibraryClientMock.Verify(
            x => x.GetReadableBookByIsbnAsync(isbn, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Test]
    public async Task LookupAsync_WithIsbn_WhenNotFound_ReturnsNull()
    {
        var isbn = "0000000000000";

        _openLibraryClientMock
            .Setup(x => x.GetReadableBookByIsbnAsync(isbn, It.IsAny<CancellationToken>()))
            .ReturnsAsync((BookResponse?)null);

        var result = await _strategy.LookupAsync(IdentifierType.Isbn, isbn, CancellationToken.None);

        Assert.That(result, Is.Null);
    }

    #endregion

    #region LookupAsync - LCCN Tests

    [Test]
    public async Task LookupAsync_WithLccn_CallsGetReadableBookByLccnAsync()
    {
        var lccn = "2017046873";
        var expectedResponse = CreateBookResponse("Clean Architecture");

        _openLibraryClientMock
            .Setup(x => x.GetReadableBookByLccnAsync(lccn, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResponse);

        var result = await _strategy.LookupAsync(IdentifierType.Lccn, lccn, CancellationToken.None);

        Assert.That(result, Is.EqualTo(expectedResponse));
        _openLibraryClientMock.Verify(
            x => x.GetReadableBookByLccnAsync(lccn, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Test]
    public async Task LookupAsync_WithLccn_WhenNotFound_ReturnsNull()
    {
        var lccn = "0000000000";

        _openLibraryClientMock
            .Setup(x => x.GetReadableBookByLccnAsync(lccn, It.IsAny<CancellationToken>()))
            .ReturnsAsync((BookResponse?)null);

        var result = await _strategy.LookupAsync(IdentifierType.Lccn, lccn, CancellationToken.None);

        Assert.That(result, Is.Null);
    }

    #endregion

    #region LookupAsync - OCLC Tests

    [Test]
    public async Task LookupAsync_WithOclc_CallsGetReadableBookByOclcAsync()
    {
        var oclc = "1004392074";
        var expectedResponse = CreateBookResponse("The Pragmatic Programmer");

        _openLibraryClientMock
            .Setup(x => x.GetReadableBookByOclcAsync(oclc, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResponse);

        var result = await _strategy.LookupAsync(IdentifierType.Oclc, oclc, CancellationToken.None);

        Assert.That(result, Is.EqualTo(expectedResponse));
        _openLibraryClientMock.Verify(
            x => x.GetReadableBookByOclcAsync(oclc, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Test]
    public async Task LookupAsync_WithOclc_WhenNotFound_ReturnsNull()
    {
        var oclc = "0000000000";

        _openLibraryClientMock
            .Setup(x => x.GetReadableBookByOclcAsync(oclc, It.IsAny<CancellationToken>()))
            .ReturnsAsync((BookResponse?)null);

        var result = await _strategy.LookupAsync(IdentifierType.Oclc, oclc, CancellationToken.None);

        Assert.That(result, Is.Null);
    }

    #endregion

    #region LookupAsync - OLID Tests

    [Test]
    public async Task LookupAsync_WithOlid_CallsGetReadableBookByOlidAsync()
    {
        var olid = "OL7353617M";
        var expectedResponse = CreateBookResponse("Head First Design Patterns");

        _openLibraryClientMock
            .Setup(x => x.GetReadableBookByOlidAsync(olid, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResponse);

        var result = await _strategy.LookupAsync(IdentifierType.Olid, olid, CancellationToken.None);

        Assert.That(result, Is.EqualTo(expectedResponse));
        _openLibraryClientMock.Verify(
            x => x.GetReadableBookByOlidAsync(olid, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Test]
    public async Task LookupAsync_WithOlid_WhenNotFound_ReturnsNull()
    {
        var olid = "OL0000000M";

        _openLibraryClientMock
            .Setup(x => x.GetReadableBookByOlidAsync(olid, It.IsAny<CancellationToken>()))
            .ReturnsAsync((BookResponse?)null);

        var result = await _strategy.LookupAsync(IdentifierType.Olid, olid, CancellationToken.None);

        Assert.That(result, Is.Null);
    }

    #endregion

    #region LookupAsync - UPC Tests

    [Test]
    public async Task LookupAsync_WithUpc_WhenIsbnFound_ReturnsBookResponse()
    {
        var upc = "9780134685991";
        var isbn = "9780134685991";
        var upcResponse = CreateUpcItemResponse(upc, "Clean Code", isbn);
        var expectedBookResponse = CreateBookResponse("Clean Code");

        _upcItemDbClientMock
            .Setup(x => x.GetItemByCodeAsync(upc, It.IsAny<CancellationToken>()))
            .ReturnsAsync(upcResponse);

        _openLibraryClientMock
            .Setup(x => x.GetReadableBookByIsbnAsync(isbn, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedBookResponse);

        var result = await _strategy.LookupAsync(IdentifierType.Upc, upc, CancellationToken.None);

        Assert.That(result, Is.EqualTo(expectedBookResponse));
        _upcItemDbClientMock.Verify(
            x => x.GetItemByCodeAsync(upc, It.IsAny<CancellationToken>()),
            Times.Once);
        _openLibraryClientMock.Verify(
            x => x.GetReadableBookByIsbnAsync(isbn, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Test]
    public async Task LookupAsync_WithUpc_WhenUpcNotFound_ReturnsNull()
    {
        var upc = "0000000000000";

        _upcItemDbClientMock
            .Setup(x => x.GetItemByCodeAsync(upc, It.IsAny<CancellationToken>()))
            .ReturnsAsync((UpcItemResponse?)null);

        var result = await _strategy.LookupAsync(IdentifierType.Upc, upc, CancellationToken.None);

        Assert.That(result, Is.Null);
        _openLibraryClientMock.Verify(
            x => x.GetReadableBookByIsbnAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Test]
    public async Task LookupAsync_WithUpc_WhenNoItems_ReturnsNull()
    {
        var upc = "0000000000000";
        var upcResponse = new UpcItemResponse(upc, 0, []);

        _upcItemDbClientMock
            .Setup(x => x.GetItemByCodeAsync(upc, It.IsAny<CancellationToken>()))
            .ReturnsAsync(upcResponse);

        var result = await _strategy.LookupAsync(IdentifierType.Upc, upc, CancellationToken.None);

        Assert.That(result, Is.Null);
        _openLibraryClientMock.Verify(
            x => x.GetReadableBookByIsbnAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Test]
    public async Task LookupAsync_WithUpc_WhenNoIsbn_ReturnsNull()
    {
        var upc = "0000000000000";
        var upcItem = new UpcItem(
            Ean: upc,
            Title: "Some Book",
            Description: "Description",
            Category: "Books",
            Brand: "Publisher",
            Model: null,
            Isbn: null);
        var upcResponse = new UpcItemResponse(upc, 1, [upcItem]);

        _upcItemDbClientMock
            .Setup(x => x.GetItemByCodeAsync(upc, It.IsAny<CancellationToken>()))
            .ReturnsAsync(upcResponse);

        var result = await _strategy.LookupAsync(IdentifierType.Upc, upc, CancellationToken.None);

        Assert.That(result, Is.Null);
        _openLibraryClientMock.Verify(
            x => x.GetReadableBookByIsbnAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Test]
    public async Task LookupAsync_WithUpc_WhenEmptyIsbn_ReturnsNull()
    {
        var upc = "0000000000000";
        var upcItem = new UpcItem(
            Ean: upc,
            Title: "Some Book",
            Description: "Description",
            Category: "Books",
            Brand: "Publisher",
            Model: null,
            Isbn: string.Empty);
        var upcResponse = new UpcItemResponse(upc, 1, [upcItem]);

        _upcItemDbClientMock
            .Setup(x => x.GetItemByCodeAsync(upc, It.IsAny<CancellationToken>()))
            .ReturnsAsync(upcResponse);

        var result = await _strategy.LookupAsync(IdentifierType.Upc, upc, CancellationToken.None);

        Assert.That(result, Is.Null);
        _openLibraryClientMock.Verify(
            x => x.GetReadableBookByIsbnAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    #endregion

    #region LookupAsync - EAN Tests

    [Test]
    public async Task LookupAsync_WithEan_WhenIsbnFound_ReturnsBookResponse()
    {
        var ean = "9780134685991";
        var isbn = "9780134685991";
        var upcResponse = CreateUpcItemResponse(ean, "Clean Code", isbn);
        var expectedBookResponse = CreateBookResponse("Clean Code");

        _upcItemDbClientMock
            .Setup(x => x.GetItemByCodeAsync(ean, It.IsAny<CancellationToken>()))
            .ReturnsAsync(upcResponse);

        _openLibraryClientMock
            .Setup(x => x.GetReadableBookByIsbnAsync(isbn, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedBookResponse);

        var result = await _strategy.LookupAsync(IdentifierType.Ean, ean, CancellationToken.None);

        Assert.That(result, Is.EqualTo(expectedBookResponse));
        _upcItemDbClientMock.Verify(
            x => x.GetItemByCodeAsync(ean, It.IsAny<CancellationToken>()),
            Times.Once);
        _openLibraryClientMock.Verify(
            x => x.GetReadableBookByIsbnAsync(isbn, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    #endregion

    #region Helper Methods

    private static BookResponse CreateBookResponse(string title)
    {
        return new BookResponse(
            Title: title,
            Subtitle: string.Empty,
            Authors: [new Author("Robert C. Martin", "http://example.com")],
            NumberOfPages: 464,
            Publishers: [new Publisher("Prentice Hall")],
            PublishDate: "2008",
            Subjects: [new Subject("Software Engineering", "http://example.com")],
            Format: null);
    }

    private static UpcItemResponse CreateUpcItemResponse(string code, string title, string? isbn)
    {
        var item = new UpcItem(
            Ean: code,
            Title: title,
            Description: $"Description for {title}",
            Category: "Books",
            Brand: "Publisher",
            Model: null,
            Isbn: isbn);

        return new UpcItemResponse(code, 1, [item]);
    }

    #endregion
}
