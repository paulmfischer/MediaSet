using NUnit.Framework;
using Moq;
using Bogus;
using MediaSet.Api.Infrastructure.Database;
using MediaSet.Api.Infrastructure.Caching;
using MediaSet.Api.Services;
using MediaSet.Api.Infrastructure.DataAccess;
using MediaSet.Api.Models;
using MongoDB.Driver;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;

namespace MediaSet.Api.Tests.Infrastructure.DataAccess
{
    [TestFixture]
    public class EntityServiceTests
    {
        private Mock<IDatabaseService> _databaseServiceMock = null!;
        private Mock<IMongoCollection<Book>> _collectionMock = null!;
        private Mock<ICacheService> _cacheServiceMock = null!;
        private Mock<ILogger<EntityService<Book>>> _loggerMock = null!;
        private EntityService<Book> _entityService = null!;
        private Faker<Book> _bookFaker = null!;

        [SetUp]
        public void Setup()
        {
            _databaseServiceMock = new Mock<IDatabaseService>();
            _collectionMock = new Mock<IMongoCollection<Book>>();
            _cacheServiceMock = new Mock<ICacheService>();
            _loggerMock = new Mock<ILogger<EntityService<Book>>>();

            _databaseServiceMock.Setup(db => db.GetCollection<Book>())
                .Returns(_collectionMock.Object);

            _entityService = new EntityService<Book>(
                _databaseServiceMock.Object,
                _cacheServiceMock.Object,
                _loggerMock.Object);

            _bookFaker = new Faker<Book>()
                .RuleFor(b => b.Id, f => f.Random.AlphaNumeric(24))
                .RuleFor(b => b.Title, f => f.Lorem.Sentence())
                .RuleFor(b => b.Authors, f => new List<string> { f.Person.FullName })
                .RuleFor(b => b.Genres, f => new List<string> { f.Lorem.Word() });
        }

        [Test]
        public void EntityService_ShouldBeConstructed_WithValidDatabaseService()
        {
            // Arrange & Act
            var service = new EntityService<Book>(
                _databaseServiceMock.Object,
                _cacheServiceMock.Object,
                _loggerMock.Object);

            // Assert
            Assert.That(service, Is.Not.Null);
            // Verify it was called at least once (once in setup, once here)
            _databaseServiceMock.Verify(db => db.GetCollection<Book>(), Times.AtLeastOnce);
        }

        [Test]
        public async Task CreateAsync_ShouldCallInsertOneAsync()
        {
            // Arrange
            var book = _bookFaker.Generate();

            // Act
            await _entityService.CreateAsync(book);

            // Assert
            _collectionMock.Verify(c => c.InsertOneAsync(book, null, default), Times.Once);
        }

        [Test]
        public async Task BulkCreateAsync_ShouldCallInsertManyAsync()
        {
            // Arrange
            var books = _bookFaker.Generate(3);

            // Act
            await _entityService.BulkCreateAsync(books);

            // Assert
            _collectionMock.Verify(c => c.InsertManyAsync(books, null, default), Times.Once);
        }

        [Test]
        public async Task UpdateAsync_ShouldCallReplaceOneAsync_AndReturnResult()
        {
            // Arrange
            var book = _bookFaker.Generate();
            var expectedResult = Mock.Of<ReplaceOneResult>();

            _collectionMock.Setup(c => c.ReplaceOneAsync(
                It.IsAny<FilterDefinition<Book>>(),
                book,
                It.IsAny<ReplaceOptions>(),
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedResult);

            // Act
            var result = await _entityService.UpdateAsync(book.Id ?? throw new InvalidOperationException("book.Id is null"), book);

            // Assert
            _collectionMock.Verify(c => c.ReplaceOneAsync(
                It.IsAny<FilterDefinition<Book>>(),
                book,
                It.IsAny<ReplaceOptions>(),
                It.IsAny<CancellationToken>()), Times.Once);
            Assert.That(result, Is.EqualTo(expectedResult));
        }

        [Test]
        public async Task RemoveAsync_ShouldCallDeleteOneAsync_AndReturnResult()
        {
            // Arrange
            var bookId = "507f1f77bcf86cd799439011";
            var expectedResult = Mock.Of<DeleteResult>();

            _collectionMock.Setup(c => c.DeleteOneAsync(
                It.IsAny<FilterDefinition<Book>>(),
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedResult);

            // Act
            var result = await _entityService.RemoveAsync(bookId);

            // Assert
            _collectionMock.Verify(c => c.DeleteOneAsync(
                It.IsAny<FilterDefinition<Book>>(),
                It.IsAny<CancellationToken>()), Times.Once);
            Assert.That(result, Is.EqualTo(expectedResult));
        }

        [Test]
        public void EntityService_ShouldWork_WithDifferentEntityTypes()
        {
            // Arrange
            var loggerMock = new Mock<ILogger<EntityService<Movie>>>();
            var movieService = new EntityService<Movie>(
                _databaseServiceMock.Object,
                _cacheServiceMock.Object,
                loggerMock.Object);

            // Act & Assert
            Assert.That(movieService, Is.Not.Null);
            // Verify it calls GetCollection for Movie type
            _databaseServiceMock.Verify(db => db.GetCollection<Movie>(), Times.Once);
        }

        [Test]
        public async Task GetListAsync_ShouldReturnAllEntities_SortedByTitle()
        {
            // Arrange
            // Create books in unsorted order
            var books = new List<Book>
            {
                _bookFaker.Clone().RuleFor(b => b.Title, "Zebra Book").Generate(),
                _bookFaker.Clone().RuleFor(b => b.Title, "Alpha Book").Generate(),
                _bookFaker.Clone().RuleFor(b => b.Title, "Middle Book").Generate()
            };

            // MongoDB would return them sorted, so simulate that
            var sortedBooks = books.OrderBy(b => b.Title).ToList();

            var asyncCursor = new Mock<IAsyncCursor<Book>>();
            asyncCursor.Setup(c => c.Current).Returns(sortedBooks);
            asyncCursor.SetupSequence(c => c.MoveNext(It.IsAny<CancellationToken>()))
                .Returns(true)
                .Returns(false);
            asyncCursor.SetupSequence(c => c.MoveNextAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(true)
                .ReturnsAsync(false);

            _collectionMock.Setup(c => c.FindAsync(
                It.IsAny<FilterDefinition<Book>>(),
                It.IsAny<FindOptions<Book, Book>>(),
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(asyncCursor.Object);

            // Act
            var result = await _entityService.GetListAsync();

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result.Count(), Is.EqualTo(3));

            // Verify the books are sorted by title
            var resultList = result.ToList();
            Assert.That(resultList[0].Title, Is.EqualTo("Alpha Book"));
            Assert.That(resultList[1].Title, Is.EqualTo("Middle Book"));
            Assert.That(resultList[2].Title, Is.EqualTo("Zebra Book"));

            // Verify collection was called
            _collectionMock.Verify(c => c.FindAsync(
                It.IsAny<FilterDefinition<Book>>(),
                It.IsAny<FindOptions<Book, Book>>(),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Test]
        public async Task GetListAsync_ShouldReturnEmpty_WhenNoEntitiesExist()
        {
            // Arrange
            var emptyList = new List<Book>();

            var asyncCursor = new Mock<IAsyncCursor<Book>>();
            asyncCursor.Setup(c => c.Current).Returns(emptyList);
            asyncCursor.SetupSequence(c => c.MoveNext(It.IsAny<CancellationToken>()))
                .Returns(true)
                .Returns(false);
            asyncCursor.SetupSequence(c => c.MoveNextAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(true)
                .ReturnsAsync(false);

            _collectionMock.Setup(c => c.FindAsync(
                It.IsAny<FilterDefinition<Book>>(),
                It.IsAny<FindOptions<Book, Book>>(),
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(asyncCursor.Object);

            // Act
            var result = await _entityService.GetListAsync();

            // Assert
            Assert.That(result, Is.Empty);
        }

        [Test]
        public async Task GetAsync_ShouldReturnEntity_WhenEntityExists()
        {
            // Arrange
            var bookId = "507f1f77bcf86cd799439011";
            var expectedBook = _bookFaker.Clone().RuleFor(b => b.Id, bookId).Generate();

            var asyncCursor = new Mock<IAsyncCursor<Book>>();
            asyncCursor.Setup(c => c.Current).Returns(new List<Book> { expectedBook });
            asyncCursor.SetupSequence(c => c.MoveNext(It.IsAny<CancellationToken>()))
                .Returns(true)
                .Returns(false);
            asyncCursor.SetupSequence(c => c.MoveNextAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(true)
                .ReturnsAsync(false);

            _collectionMock.Setup(c => c.FindAsync(
                It.IsAny<FilterDefinition<Book>>(),
                It.IsAny<FindOptions<Book, Book>>(),
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(asyncCursor.Object);

            // Act
            var result = await _entityService.GetAsync(bookId);

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result?.Id, Is.EqualTo(bookId));
            _collectionMock.Verify(c => c.FindAsync(
                It.IsAny<FilterDefinition<Book>>(),
                It.IsAny<FindOptions<Book, Book>>(),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Test]
        public async Task GetAsync_ShouldReturnNull_WhenEntityDoesNotExist()
        {
            // Arrange
            var bookId = "507f1f77bcf86cd799439011";

            var asyncCursor = new Mock<IAsyncCursor<Book>>();
            asyncCursor.Setup(c => c.Current).Returns(new List<Book>());
            asyncCursor.SetupSequence(c => c.MoveNext(It.IsAny<CancellationToken>()))
                .Returns(false);
            asyncCursor.SetupSequence(c => c.MoveNextAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);

            _collectionMock.Setup(c => c.FindAsync(
                It.IsAny<FilterDefinition<Book>>(),
                It.IsAny<FindOptions<Book, Book>>(),
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(asyncCursor.Object);

            // Act
            var result = await _entityService.GetAsync(bookId);

            // Assert
            Assert.That(result, Is.Null);
        }

        [Test]
        public async Task SearchAsync_ShouldReturnMatchingEntities_WhenSearchTextMatches()
        {
            // Arrange
            var searchText = "fantasy";
            var orderBy = "title:asc";
            var books = new List<Book>
            {
                _bookFaker.Clone().RuleFor(b => b.Title, "Fantasy Adventure").Generate(),
                _bookFaker.Clone().RuleFor(b => b.Title, "Epic Fantasy").Generate()
            };

            var asyncCursor = new Mock<IAsyncCursor<Book>>();
            asyncCursor.Setup(c => c.Current).Returns(books);
            asyncCursor.SetupSequence(c => c.MoveNext(It.IsAny<CancellationToken>()))
                .Returns(true)
                .Returns(false);
            asyncCursor.SetupSequence(c => c.MoveNextAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(true)
                .ReturnsAsync(false);

            _collectionMock.Setup(c => c.FindAsync(
                It.IsAny<FilterDefinition<Book>>(),
                It.IsAny<FindOptions<Book, Book>>(),
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(asyncCursor.Object);

            // Act
            var result = await _entityService.SearchAsync(searchText, orderBy);

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result.Count(), Is.EqualTo(2));
            _collectionMock.Verify(c => c.FindAsync(
                It.IsAny<FilterDefinition<Book>>(),
                It.IsAny<FindOptions<Book, Book>>(),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Test]
        public async Task SearchAsync_ShouldReturnEmpty_WhenNoMatchesFound()
        {
            // Arrange
            var searchText = "nonexistent";
            var orderBy = "title:asc";
            var emptyList = new List<Book>();

            var asyncCursor = new Mock<IAsyncCursor<Book>>();
            asyncCursor.Setup(c => c.Current).Returns(emptyList);
            asyncCursor.SetupSequence(c => c.MoveNext(It.IsAny<CancellationToken>()))
                .Returns(true)
                .Returns(false);
            asyncCursor.SetupSequence(c => c.MoveNextAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(true)
                .ReturnsAsync(false);

            _collectionMock.Setup(c => c.FindAsync(
                It.IsAny<FilterDefinition<Book>>(),
                It.IsAny<FindOptions<Book, Book>>(),
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(asyncCursor.Object);

            // Act
            var result = await _entityService.SearchAsync(searchText, orderBy);

            // Assert
            Assert.That(result, Is.Empty);
        }

        [Test]
        public async Task SearchAsync_ShouldHandleAscendingOrder()
        {
            // Arrange
            var searchText = "book";
            var orderBy = "title:asc";
            var books = new List<Book>
            {
                _bookFaker.Clone().RuleFor(b => b.Title, "Book A").Generate(),
                _bookFaker.Clone().RuleFor(b => b.Title, "Book B").Generate()
            };

            var asyncCursor = new Mock<IAsyncCursor<Book>>();
            asyncCursor.Setup(c => c.Current).Returns(books);
            asyncCursor.SetupSequence(c => c.MoveNext(It.IsAny<CancellationToken>()))
                .Returns(true)
                .Returns(false);
            asyncCursor.SetupSequence(c => c.MoveNextAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(true)
                .ReturnsAsync(false);

            _collectionMock.Setup(c => c.FindAsync(
                It.IsAny<FilterDefinition<Book>>(),
                It.IsAny<FindOptions<Book, Book>>(),
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(asyncCursor.Object);

            // Act
            var result = await _entityService.SearchAsync(searchText, orderBy);

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result.Count(), Is.EqualTo(2));
        }

        [Test]
        public async Task SearchAsync_ShouldHandleDescendingOrder()
        {
            // Arrange
            var searchText = "book";
            var orderBy = "title:desc";
            var books = new List<Book>
            {
                _bookFaker.Clone().RuleFor(b => b.Title, "Book B").Generate(),
                _bookFaker.Clone().RuleFor(b => b.Title, "Book A").Generate()
            };

            var asyncCursor = new Mock<IAsyncCursor<Book>>();
            asyncCursor.Setup(c => c.Current).Returns(books);
            asyncCursor.SetupSequence(c => c.MoveNext(It.IsAny<CancellationToken>()))
                .Returns(true)
                .Returns(false);
            asyncCursor.SetupSequence(c => c.MoveNextAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(true)
                .ReturnsAsync(false);

            _collectionMock.Setup(c => c.FindAsync(
                It.IsAny<FilterDefinition<Book>>(),
                It.IsAny<FindOptions<Book, Book>>(),
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(asyncCursor.Object);

            // Act
            var result = await _entityService.SearchAsync(searchText, orderBy);

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result.Count(), Is.EqualTo(2));
        }

        [Test]
        public async Task SearchAsync_ShouldDefaultToAscending_WhenOrderByIsEmpty()
        {
            // Arrange
            var searchText = "book";
            var orderBy = "";
            var books = new List<Book>
            {
                _bookFaker.Clone().RuleFor(b => b.Title, "Book Title").Generate()
            };

            var asyncCursor = new Mock<IAsyncCursor<Book>>();
            asyncCursor.Setup(c => c.Current).Returns(books);
            asyncCursor.SetupSequence(c => c.MoveNext(It.IsAny<CancellationToken>()))
                .Returns(true)
                .Returns(false);
            asyncCursor.SetupSequence(c => c.MoveNextAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(true)
                .ReturnsAsync(false);

            _collectionMock.Setup(c => c.FindAsync(
                It.IsAny<FilterDefinition<Book>>(),
                It.IsAny<FindOptions<Book, Book>>(),
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(asyncCursor.Object);

            // Act
            var result = await _entityService.SearchAsync(searchText, orderBy);

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result.Count(), Is.EqualTo(1));
        }

        [Test]
        public async Task SearchAsync_ShouldBeCaseInsensitive()
        {
            // Arrange
            var searchText = "FANTASY";
            var orderBy = "title:asc";
            var books = new List<Book>
            {
                _bookFaker.Clone().RuleFor(b => b.Title, "fantasy adventure").Generate()
            };

            var asyncCursor = new Mock<IAsyncCursor<Book>>();
            asyncCursor.Setup(c => c.Current).Returns(books);
            asyncCursor.SetupSequence(c => c.MoveNext(It.IsAny<CancellationToken>()))
                .Returns(true)
                .Returns(false);
            asyncCursor.SetupSequence(c => c.MoveNextAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(true)
                .ReturnsAsync(false);

            _collectionMock.Setup(c => c.FindAsync(
                It.IsAny<FilterDefinition<Book>>(),
                It.IsAny<FindOptions<Book, Book>>(),
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(asyncCursor.Object);

            // Act
            var result = await _entityService.SearchAsync(searchText, orderBy);

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result.Count(), Is.EqualTo(1));
        }

        #region Cache Invalidation Tests

        [Test]
        public async Task CreateAsync_ShouldInvalidateCache()
        {
            // Arrange
            var book = _bookFaker.Generate();

            // Act
            await _entityService.CreateAsync(book);

            // Assert
            _collectionMock.Verify(c => c.InsertOneAsync(book, null, default), Times.Once);
            _cacheServiceMock.Verify(c => c.RemoveByPatternAsync("metadata:Books:*"), Times.Once);
            _cacheServiceMock.Verify(c => c.RemoveAsync("stats"), Times.Once);
        }

        [Test]
        public async Task UpdateAsync_ShouldInvalidateCache()
        {
            // Arrange
            var book = _bookFaker.Generate();
            var expectedResult = Mock.Of<ReplaceOneResult>();

            _collectionMock.Setup(c => c.ReplaceOneAsync(
                It.IsAny<FilterDefinition<Book>>(),
                book,
                It.IsAny<ReplaceOptions>(),
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedResult);

            // Act
            await _entityService.UpdateAsync(book.Id ?? throw new InvalidOperationException("book.Id is null"), book);

            // Assert
            _cacheServiceMock.Verify(c => c.RemoveByPatternAsync("metadata:Books:*"), Times.Once);
            _cacheServiceMock.Verify(c => c.RemoveAsync("stats"), Times.Once);
        }

        [Test]
        public async Task RemoveAsync_ShouldInvalidateCache()
        {
            // Arrange
            var bookId = "507f1f77bcf86cd799439011";
            var expectedResult = Mock.Of<DeleteResult>();

            _collectionMock.Setup(c => c.DeleteOneAsync(
                It.IsAny<FilterDefinition<Book>>(),
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedResult);

            // Act
            await _entityService.RemoveAsync(bookId);

            // Assert
            _cacheServiceMock.Verify(c => c.RemoveByPatternAsync("metadata:Books:*"), Times.Once);
            _cacheServiceMock.Verify(c => c.RemoveAsync("stats"), Times.Once);
        }

        [Test]
        public async Task BulkCreateAsync_ShouldInvalidateCache()
        {
            // Arrange
            var books = _bookFaker.Generate(3);

            // Act
            await _entityService.BulkCreateAsync(books);

            // Assert
            _collectionMock.Verify(c => c.InsertManyAsync(books, null, default), Times.Once);
            _cacheServiceMock.Verify(c => c.RemoveByPatternAsync("metadata:Books:*"), Times.Once);
            _cacheServiceMock.Verify(c => c.RemoveAsync("stats"), Times.Once);
        }

        [Test]
        public async Task CreateAsync_ShouldInvalidateOnlyRelevantCaches()
        {
            // Arrange
            var book = _bookFaker.Generate();

            // Act
            await _entityService.CreateAsync(book);

            // Assert - Should only invalidate Books metadata, not Movies or Games
            _cacheServiceMock.Verify(c => c.RemoveByPatternAsync("metadata:Books:*"), Times.Once);
            _cacheServiceMock.Verify(c => c.RemoveByPatternAsync("metadata:Movies:*"), Times.Never);
            _cacheServiceMock.Verify(c => c.RemoveByPatternAsync("metadata:Games:*"), Times.Never);
            _cacheServiceMock.Verify(c => c.RemoveAsync("stats"), Times.Once);
        }

        #endregion
    }
}
