// Enable nullable annotations for matching API signatures
#nullable enable
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MediaSet.Api.Models;
using MediaSet.Api.Clients;
using MediaSet.Api.Services.Lookup;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;

namespace MediaSet.Api.Tests.Services;

[TestFixture]
public class LookupServiceTests
{
    private LookupService _service = null!;

    [SetUp]
    public void Setup()
    {
        var logger = Mock.Of<ILogger<LookupService>>();
        var strategies = new List<ILookupStrategy>
        {
            new FakeBookStrategy(),
            new FakeMovieStrategy()
        };
        _service = new LookupService(strategies, logger);
    }

    [Test]
    public async Task LookupAsync_ReturnsBookResponse_ForBooksIsbn()
    {
        var result = await _service.LookupAsync("books", "isbn", "9780134685991", CancellationToken.None);
        Assert.That(result, Is.InstanceOf<BookResponse>());
        var book = (BookResponse)result!;
        Assert.That(book.Title, Is.EqualTo("Test Book"));
    }

    [Test]
    public async Task LookupAsync_ReturnsNull_WhenNoStrategyMatches()
    {
        var result = await _service.LookupAsync("unknown", "isbn", "123", CancellationToken.None);
        Assert.That(result, Is.Null);
    }

    private sealed class FakeBookStrategy : ILookupStrategy<BookResponse>
    {
        public string EntityType => "books";
        public bool SupportsIdentifierType(string identifierType) => identifierType.Equals("isbn", System.StringComparison.OrdinalIgnoreCase);
        public Task<BookResponse?> LookupAsync(string identifierType, string identifierValue, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<BookResponse?>(new BookResponse(
                "Test Book",
                string.Empty,
                new List<Author>(),
                0,
                new List<Publisher>(),
                string.Empty,
                new List<Subject>(),
                null
            ));
        }
    }

    private sealed class FakeMovieStrategy : ILookupStrategy<MovieLookupResponse>
    {
        public string EntityType => "movies";
        public bool SupportsIdentifierType(string identifierType) => identifierType.Equals("upc", System.StringComparison.OrdinalIgnoreCase);
        public Task<MovieLookupResponse?> LookupAsync(string identifierType, string identifierValue, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<MovieLookupResponse?>(new MovieLookupResponse(
                "Movie",
                new List<string>(),
                new List<string>(),
                string.Empty,
                string.Empty,
                null,
                string.Empty,
                null,
                false
            ));
        }
    }
}
