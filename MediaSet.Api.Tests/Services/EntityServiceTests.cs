using NUnit.Framework;
using Moq;
using Bogus;
using MediaSet.Api.Services;
using MediaSet.Api.Models;
using MongoDB.Driver;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading;

namespace MediaSet.Api.Tests.Services
{
    [TestFixture]
    public class EntityServiceTests
    {
        private Mock<DatabaseService> _databaseServiceMock;
        private Mock<IMongoCollection<Book>> _collectionMock;
        private EntityService<Book> _entityService;
        private Faker<Book> _bookFaker;

        [SetUp]
        public void Setup()
        {
            // Create mock database service with proper constructor
            var dbSettingsMock = new Mock<IOptions<MediaSetDatabaseSettings>>();
            dbSettingsMock.Setup(x => x.Value).Returns(new MediaSetDatabaseSettings
            {
                ConnectionString = "mongodb://localhost:27017",
                DatabaseName = "TestDatabase"
            });

            _databaseServiceMock = new Mock<DatabaseService>(dbSettingsMock.Object);
            _collectionMock = new Mock<IMongoCollection<Book>>();
            
            _databaseServiceMock.Setup(db => db.GetCollection<Book>())
                .Returns(_collectionMock.Object);

            _entityService = new EntityService<Book>(_databaseServiceMock.Object);

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
            var service = new EntityService<Book>(_databaseServiceMock.Object);

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
            var result = await _entityService.UpdateAsync(book.Id, book);

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
            var movieService = new EntityService<Movie>(_databaseServiceMock.Object);

            // Act & Assert
            Assert.That(movieService, Is.Not.Null);
            // Verify it calls GetCollection for Movie type
            _databaseServiceMock.Verify(db => db.GetCollection<Movie>(), Times.Once);
        }
    }
}
