using MediaSet.Api.Features.Lookup.Models;
using MediaSet.Api.Features.Entities.Models;
using MediaSet.Api.Infrastructure.Lookup;
using MediaSet.Api.Features.Entities.Models;
using MediaSet.Api.Infrastructure.Lookup;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MediaSet.Api.Tests.Infrastructure.Lookup;

[TestFixture]
public class MovieLookupStrategyTests
{
    private Mock<IUpcItemDbClient> _upcItemDbClientMock = null!;
    private Mock<ITmdbClient> _tmdbClientMock = null!;
    private Mock<ILogger<MovieLookupStrategy>> _loggerMock = null!;
    private MovieLookupStrategy _strategy = null!;

    [SetUp]
    public void SetUp()
    {
        _upcItemDbClientMock = new Mock<IUpcItemDbClient>();
        _tmdbClientMock = new Mock<ITmdbClient>();
        _loggerMock = new Mock<ILogger<MovieLookupStrategy>>();
        _strategy = new MovieLookupStrategy(
            _upcItemDbClientMock.Object,
            _tmdbClientMock.Object,
            _loggerMock.Object);
    }

    #region CanHandle Tests

    [Test]
    public void CanHandle_WithMoviesAndUpc_ReturnsTrue()
    {
        var result = _strategy.CanHandle(MediaTypes.Movies, IdentifierType.Upc);

        Assert.That(result, Is.True);
    }

    [Test]
    public void CanHandle_WithMoviesAndEan_ReturnsTrue()
    {
        var result = _strategy.CanHandle(MediaTypes.Movies, IdentifierType.Ean);

        Assert.That(result, Is.True);
    }

    [Test]
    public void CanHandle_WithMoviesAndIsbn_ReturnsFalse()
    {
        var result = _strategy.CanHandle(MediaTypes.Movies, IdentifierType.Isbn);

        Assert.That(result, Is.False);
    }

    [Test]
    public void CanHandle_WithBooksAndUpc_ReturnsFalse()
    {
        var result = _strategy.CanHandle(MediaTypes.Books, IdentifierType.Upc);

        Assert.That(result, Is.False);
    }

    #endregion

    #region LookupAsync - Success Tests

    [Test]
    public async Task LookupAsync_WithUpc_WhenMovieFound_ReturnsMovieResponse()
    {
        var upc = "043396471238";
        var movieTitle = "1408 (Two-Disc Collector's Edition) [DVD]";
        var cleanedTitle = "1408";
        var upcResponse = CreateUpcItemResponse(upc, movieTitle);
        var searchResponse = CreateSearchResponse(1408, "1408");
        var movieDetails = CreateMovieDetails(
            id: 1408,
            title: "1408",
            releaseDate: "2007-06-22",
            runtime: 104);

        _upcItemDbClientMock
            .Setup(x => x.GetItemByCodeAsync(upc, It.IsAny<CancellationToken>()))
            .ReturnsAsync(upcResponse);

        _tmdbClientMock
            .Setup(x => x.SearchMovieAsync(cleanedTitle, It.IsAny<CancellationToken>()))
            .ReturnsAsync(searchResponse);

        _tmdbClientMock
            .Setup(x => x.GetMovieDetailsAsync(1408, It.IsAny<CancellationToken>()))
            .ReturnsAsync(movieDetails);

        var result = await _strategy.LookupAsync(IdentifierType.Upc, upc, CancellationToken.None);

        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Title, Is.EqualTo("1408"));
        Assert.That(result.ReleaseDate, Is.EqualTo("2007-06-22"));
        Assert.That(result.Runtime, Is.EqualTo(104));
        // Format extraction finds "DVD" from the brackets
        Assert.That(result.Format, Is.EqualTo("DVD"));
        Assert.That(result.Rating, Is.EqualTo("7.5/10"));

        _upcItemDbClientMock.Verify(
            x => x.GetItemByCodeAsync(upc, It.IsAny<CancellationToken>()),
            Times.Once);
        _tmdbClientMock.Verify(
            x => x.SearchMovieAsync(cleanedTitle, It.IsAny<CancellationToken>()),
            Times.Once);
        _tmdbClientMock.Verify(
            x => x.GetMovieDetailsAsync(1408, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Test]
    public async Task LookupAsync_WithEan_WhenMovieFound_ReturnsMovieResponse()
    {
        var ean = "043396471238";
        var movieTitle = "The Matrix (Blu-ray)";
        var cleanedTitle = "The Matrix";
        var upcResponse = CreateUpcItemResponse(ean, movieTitle);
        var searchResponse = CreateSearchResponse(603, "The Matrix");
        var movieDetails = CreateMovieDetails(
            id: 603,
            title: "The Matrix",
            releaseDate: "1999-03-31",
            runtime: 136);

        _upcItemDbClientMock
            .Setup(x => x.GetItemByCodeAsync(ean, It.IsAny<CancellationToken>()))
            .ReturnsAsync(upcResponse);

        _tmdbClientMock
            .Setup(x => x.SearchMovieAsync(cleanedTitle, It.IsAny<CancellationToken>()))
            .ReturnsAsync(searchResponse);

        _tmdbClientMock
            .Setup(x => x.GetMovieDetailsAsync(603, It.IsAny<CancellationToken>()))
            .ReturnsAsync(movieDetails);

        var result = await _strategy.LookupAsync(IdentifierType.Ean, ean, CancellationToken.None);

        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Title, Is.EqualTo("The Matrix"));
        Assert.That(result.Format, Is.EqualTo("Blu-ray"));
    }

    #endregion

    #region LookupAsync - Error Cases

    [Test]
    public async Task LookupAsync_WithUpc_WhenUpcNotFound_ReturnsNull()
    {
        var upc = "0000000000000";

        _upcItemDbClientMock
            .Setup(x => x.GetItemByCodeAsync(upc, It.IsAny<CancellationToken>()))
            .ReturnsAsync((UpcItemResponse?)null);

        var result = await _strategy.LookupAsync(IdentifierType.Upc, upc, CancellationToken.None);

        Assert.That(result, Is.Null);
        _tmdbClientMock.Verify(
            x => x.SearchMovieAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
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
        _tmdbClientMock.Verify(
            x => x.SearchMovieAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Test]
    public async Task LookupAsync_WithUpc_WhenNoTitle_ReturnsNull()
    {
        var upc = "0000000000000";
        var upcItem = new UpcItem(
            Ean: upc,
            Title: string.Empty,
            Description: "Some movie",
            Category: "Movies",
            Brand: "Studio",
            Model: null,
            Isbn: null);
        var upcResponse = new UpcItemResponse(upc, 1, [upcItem]);

        _upcItemDbClientMock
            .Setup(x => x.GetItemByCodeAsync(upc, It.IsAny<CancellationToken>()))
            .ReturnsAsync(upcResponse);

        var result = await _strategy.LookupAsync(IdentifierType.Upc, upc, CancellationToken.None);

        Assert.That(result, Is.Null);
        _tmdbClientMock.Verify(
            x => x.SearchMovieAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Test]
    public async Task LookupAsync_WithUpc_WhenTmdbSearchReturnsNull_ReturnsNull()
    {
        var upc = "043396471238";
        var movieTitle = "Unknown Movie Title";
        var upcResponse = CreateUpcItemResponse(upc, movieTitle);

        _upcItemDbClientMock
            .Setup(x => x.GetItemByCodeAsync(upc, It.IsAny<CancellationToken>()))
            .ReturnsAsync(upcResponse);

        _tmdbClientMock
            .Setup(x => x.SearchMovieAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((TmdbSearchResponse?)null);

        var result = await _strategy.LookupAsync(IdentifierType.Upc, upc, CancellationToken.None);

        Assert.That(result, Is.Null);
        _tmdbClientMock.Verify(
            x => x.GetMovieDetailsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Test]
    public async Task LookupAsync_WithUpc_WhenTmdbSearchReturnsNoResults_ReturnsNull()
    {
        var upc = "043396471238";
        var movieTitle = "Unknown Movie Title";
        var upcResponse = CreateUpcItemResponse(upc, movieTitle);
        var emptySearchResponse = new TmdbSearchResponse(1, [], 0, 0);

        _upcItemDbClientMock
            .Setup(x => x.GetItemByCodeAsync(upc, It.IsAny<CancellationToken>()))
            .ReturnsAsync(upcResponse);

        _tmdbClientMock
            .Setup(x => x.SearchMovieAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(emptySearchResponse);

        var result = await _strategy.LookupAsync(IdentifierType.Upc, upc, CancellationToken.None);

        Assert.That(result, Is.Null);
        _tmdbClientMock.Verify(
            x => x.GetMovieDetailsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Test]
    public async Task LookupAsync_WithUpc_WhenGetMovieDetailsReturnsNull_ReturnsNull()
    {
        var upc = "043396471238";
        var movieTitle = "1408";
        var upcResponse = CreateUpcItemResponse(upc, movieTitle);
        var searchResponse = CreateSearchResponse(1408, "1408");

        _upcItemDbClientMock
            .Setup(x => x.GetItemByCodeAsync(upc, It.IsAny<CancellationToken>()))
            .ReturnsAsync(upcResponse);

        _tmdbClientMock
            .Setup(x => x.SearchMovieAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(searchResponse);

        _tmdbClientMock
            .Setup(x => x.GetMovieDetailsAsync(1408, It.IsAny<CancellationToken>()))
            .ReturnsAsync((TmdbMovieResponse?)null);

        var result = await _strategy.LookupAsync(IdentifierType.Upc, upc, CancellationToken.None);

        Assert.That(result, Is.Null);
    }

    #endregion

    #region Title Cleaning Tests

    [Test]
    public async Task LookupAsync_CleansMovieTitleCorrectly_RemovesParentheses()
    {
        var upc = "043396471238";
        var rawTitle = "1408 (Two-Disc Collector's Edition)";
        var expectedCleanTitle = "1408";
        await TestTitleCleaning(upc, rawTitle, expectedCleanTitle);
    }

    [Test]
    public async Task LookupAsync_CleansMovieTitleCorrectly_RemovesBrackets()
    {
        var upc = "043396471238";
        var rawTitle = "1408 [2 Discs] [Collector's Edition]";
        var expectedCleanTitle = "1408";
        await TestTitleCleaning(upc, rawTitle, expectedCleanTitle);
    }

    [Test]
    public async Task LookupAsync_CleansMovieTitleCorrectly_RemovesDashSuffix()
    {
        var upc = "043396471238";
        var rawTitle = "1408 - DVD";
        var expectedCleanTitle = "1408";
        await TestTitleCleaning(upc, rawTitle, expectedCleanTitle);
    }

    [Test]
    public async Task LookupAsync_CleansMovieTitleCorrectly_RemovesTrailingNew()
    {
        var upc = "043396471238";
        var rawTitle = "Akira (Widescreen) [DVD] NEW";
        var expectedCleanTitle = "Akira";
        await TestTitleCleaning(upc, rawTitle, expectedCleanTitle);
    }

    [Test]
    public async Task LookupAsync_CleansMovieTitleCorrectly_ComplexTitle()
    {
        var upc = "043396471238";
        var rawTitle = "The Matrix (Widescreen) [2-Disc Special Edition] - Blu-ray NEW";
        var expectedCleanTitle = "The Matrix";
        await TestTitleCleaning(upc, rawTitle, expectedCleanTitle);
    }

    private async Task TestTitleCleaning(string upc, string rawTitle, string expectedCleanTitle)
    {
        var upcResponse = CreateUpcItemResponse(upc, rawTitle);
        var searchResponse = CreateSearchResponse(1, "Test Movie");
        var movieDetails = CreateMovieDetails(1, "Test Movie", "2020-01-01", 120);

        _upcItemDbClientMock
            .Setup(x => x.GetItemByCodeAsync(upc, It.IsAny<CancellationToken>()))
            .ReturnsAsync(upcResponse);

        _tmdbClientMock
            .Setup(x => x.SearchMovieAsync(expectedCleanTitle, It.IsAny<CancellationToken>()))
            .ReturnsAsync(searchResponse);

        _tmdbClientMock
            .Setup(x => x.GetMovieDetailsAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(movieDetails);

        await _strategy.LookupAsync(IdentifierType.Upc, upc, CancellationToken.None);

        _tmdbClientMock.Verify(
            x => x.SearchMovieAsync(expectedCleanTitle, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    #endregion

    #region Format Extraction Tests

    [Test]
    public async Task LookupAsync_ExtractsFormat_FromParentheses()
    {
        var format = await TestFormatExtraction("1408 (DVD)", "DVD");
        Assert.That(format, Is.EqualTo("DVD"));
    }

    [Test]
    public async Task LookupAsync_ExtractsFormat_FromBrackets()
    {
        var format = await TestFormatExtraction("The Matrix [Blu-ray]", "Blu-ray");
        Assert.That(format, Is.EqualTo("Blu-ray"));
    }

    [Test]
    public async Task LookupAsync_ExtractsFormat_FromDashSuffix()
    {
        var format = await TestFormatExtraction("Akira - 4K UHD", "4K 4K UHD");
        Assert.That(format, Is.EqualTo("4K 4K UHD"));
    }

    [Test]
    public async Task LookupAsync_ExtractsFormat_NormalizesBlueray()
    {
        var format = await TestFormatExtraction("Movie [BD]", "Blu-ray");
        Assert.That(format, Is.EqualTo("Blu-ray"));
    }

    [Test]
    public async Task LookupAsync_ExtractsFormat_NormalizesUHD()
    {
        var format = await TestFormatExtraction("Movie [UHD]", "4K UHD");
        Assert.That(format, Is.EqualTo("4K UHD"));
    }

    [Test]
    public async Task LookupAsync_ExtractsFormat_ComplexFormat()
    {
        // The strategy extracts the first format found, which is "DVD" from brackets
        var format = await TestFormatExtraction("Movie (Two-Disc Collector's Edition) [DVD]", "DVD");
        Assert.That(format, Is.EqualTo("DVD"));
    }

    [Test]
    public async Task LookupAsync_ExtractsFormat_NoFormatFound()
    {
        var format = await TestFormatExtraction("Plain Movie Title", string.Empty);
        Assert.That(format, Is.EqualTo(string.Empty));
    }

    [Test]
    public async Task LookupAsync_ExtractsFormat_FromEndOfTitle()
    {
        // Format at end: 'DVD'
        var format = await TestFormatExtraction("30 Days of Night DVD", "DVD");
        Assert.That(format, Is.EqualTo("DVD"));
    }

    [Test]
    public async Task LookupAsync_ExtractsFormat_FromEndOfTitle_Bluray()
    {
        // Format at end: 'Blu-ray'
        var format = await TestFormatExtraction("Some Movie Blu-ray", "Blu-ray");
        Assert.That(format, Is.EqualTo("Blu-ray"));
    }

    [Test]
    public async Task LookupAsync_ExtractsFormat_FromEndOfTitle_4K()
    {
        // Format at end: '4K'
        var format = await TestFormatExtraction("Epic Movie 4K", "4K");
        Assert.That(format, Is.EqualTo("4K"));
    }

    [Test]
    public async Task LookupAsync_ExtractsDvDFormat_FromEndOfTitle()
    {
        // Format at end: 'DVD'
        var format = await TestFormatExtraction("30 Days of Night DVD", "DVD");
        Assert.That(format, Is.EqualTo("DVD"));
    }

    [Test]
    public async Task LookupAsync_ExtractsBluRayFormat_FromEndOfTitle()
    {
        // Format at end: 'Blu-ray'
        var format = await TestFormatExtraction("Some Movie Blu-ray", "Blu-ray");
        Assert.That(format, Is.EqualTo("Blu-ray"));
    }

    [Test]
    public async Task LookupAsync_Extracts4kFormat_FromEndOfTitle()
    {
        // Format at end: '4K'
        var format = await TestFormatExtraction("Epic Movie 4K", "4K");
        Assert.That(format, Is.EqualTo("4K"));
    }

    private async Task<string> TestFormatExtraction(string title, string expectedFormat)
    {
        var upc = "043396471238";
        var upcResponse = CreateUpcItemResponse(upc, title);
        var searchResponse = CreateSearchResponse(1, "Test Movie");
        var movieDetails = CreateMovieDetails(1, "Test Movie", "2020-01-01", 120);

        _upcItemDbClientMock
            .Setup(x => x.GetItemByCodeAsync(upc, It.IsAny<CancellationToken>()))
            .ReturnsAsync(upcResponse);

        _tmdbClientMock
            .Setup(x => x.SearchMovieAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(searchResponse);

        _tmdbClientMock
            .Setup(x => x.GetMovieDetailsAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(movieDetails);

        var result = await _strategy.LookupAsync(IdentifierType.Upc, upc, CancellationToken.None);

        return result?.Format ?? string.Empty;
    }

    #endregion

    #region Response Mapping Tests

    [Test]
    public async Task LookupAsync_MapsResponseCorrectly_WithAllFields()
    {
        var upc = "043396471238";
        var upcResponse = CreateUpcItemResponse(upc, "Test Movie");
        var searchResponse = CreateSearchResponse(1, "Test Movie");
        var movieDetails = CreateMovieDetails(
            id: 1,
            title: "Test Movie",
            releaseDate: "2020-01-01",
            runtime: 120,
            overview: "Test overview",
            voteAverage: 8.5);

        _upcItemDbClientMock
            .Setup(x => x.GetItemByCodeAsync(upc, It.IsAny<CancellationToken>()))
            .ReturnsAsync(upcResponse);

        _tmdbClientMock
            .Setup(x => x.SearchMovieAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(searchResponse);

        _tmdbClientMock
            .Setup(x => x.GetMovieDetailsAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(movieDetails);

        var result = await _strategy.LookupAsync(IdentifierType.Upc, upc, CancellationToken.None);

        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Title, Is.EqualTo("Test Movie"));
        Assert.That(result.ReleaseDate, Is.EqualTo("2020-01-01"));
        Assert.That(result.Runtime, Is.EqualTo(120));
        Assert.That(result.Plot, Is.EqualTo("Test overview"));
        Assert.That(result.Rating, Is.EqualTo("8.5/10"));
        Assert.That(result.Genres, Has.Count.EqualTo(2));
        Assert.That(result.Genres, Contains.Item("Action"));
        Assert.That(result.Genres, Contains.Item("Thriller"));
        Assert.That(result.Studios, Has.Count.EqualTo(2));
        Assert.That(result.Studios, Contains.Item("Warner Bros."));
        Assert.That(result.Studios, Contains.Item("Village Roadshow"));
    }

    [Test]
    public async Task LookupAsync_MapsResponseCorrectly_WithEmptyOptionalFields()
    {
        var upc = "043396471238";
        var upcResponse = CreateUpcItemResponse(upc, "Test Movie");
        var searchResponse = CreateSearchResponse(1, "Test Movie");
        var movieDetails = new TmdbMovieResponse(
            Id: 1,
            Title: "Test Movie",
            Overview: null,
            ReleaseDate: null,
            Runtime: null,
            Genres: [],
            ProductionCompanies: [],
            VoteAverage: 0);

        _upcItemDbClientMock
            .Setup(x => x.GetItemByCodeAsync(upc, It.IsAny<CancellationToken>()))
            .ReturnsAsync(upcResponse);

        _tmdbClientMock
            .Setup(x => x.SearchMovieAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(searchResponse);

        _tmdbClientMock
            .Setup(x => x.GetMovieDetailsAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(movieDetails);

        var result = await _strategy.LookupAsync(IdentifierType.Upc, upc, CancellationToken.None);

        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Title, Is.EqualTo("Test Movie"));
        Assert.That(result.ReleaseDate, Is.EqualTo(string.Empty));
        Assert.That(result.Runtime, Is.Null);
        Assert.That(result.Plot, Is.EqualTo(string.Empty));
        Assert.That(result.Rating, Is.EqualTo(string.Empty));
        Assert.That(result.Genres, Is.Empty);
        Assert.That(result.Studios, Is.Empty);
    }

    #endregion

    #region Helper Methods

    private static UpcItemResponse CreateUpcItemResponse(string code, string title)
    {
        var item = new UpcItem(
            Ean: code,
            Title: title,
            Description: $"Description for {title}",
            Category: "Movies",
            Brand: "Studio",
            Model: null,
            Isbn: null);

        return new UpcItemResponse(code, 1, [item]);
    }

    private static TmdbSearchResponse CreateSearchResponse(int id, string title)
    {
        var result = new TmdbMovieSearchResult(
            Id: id,
            Title: title,
            ReleaseDate: "2020-01-01",
            Overview: "Test overview");

        return new TmdbSearchResponse(1, [result], 1, 1);
    }

    private static TmdbMovieResponse CreateMovieDetails(
        int id,
        string title,
        string releaseDate,
        int runtime,
        string overview = "Test overview",
        double voteAverage = 7.5)
    {
        var genres = new List<TmdbGenre>
        {
            new(1, "Action"),
            new(2, "Thriller")
        };

        var companies = new List<TmdbProductionCompany>
        {
            new(1, "Warner Bros."),
            new(2, "Village Roadshow")
        };

        return new TmdbMovieResponse(
            Id: id,
            Title: title,
            Overview: overview,
            ReleaseDate: releaseDate,
            Runtime: runtime,
            Genres: genres,
            ProductionCompanies: companies,
            VoteAverage: voteAverage);
    }

    #endregion
}
