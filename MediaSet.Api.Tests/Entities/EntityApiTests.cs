using NUnit.Framework;
using Moq;
using Bogus;
using MediaSet.Api.Services;
using MediaSet.Api.Models;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Threading;
using MongoDB.Driver;

namespace MediaSet.Api.Tests.Entities;

[TestFixture]
public class EntityApiTests : IntegrationTestBase
{
    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;
    private Mock<IEntityService<Book>> _bookServiceMock = null!;
    private Mock<IEntityService<Movie>> _movieServiceMock = null!;
    private Mock<IEntityService<Game>> _gameServiceMock = null!;
    private Faker<Book> _bookFaker = null!;
    private Faker<Movie> _movieFaker = null!;
    private Faker<Game> _gameFaker = null!;
    private JsonSerializerOptions _jsonOptions = null!;

    [SetUp]
    public void Setup()
    {
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            Converters = { new JsonStringEnumConverter() }
        };
        _bookServiceMock = new Mock<IEntityService<Book>>();
        _movieServiceMock = new Mock<IEntityService<Movie>>();
        _gameServiceMock = new Mock<IEntityService<Game>>();

        _factory = CreateWebApplicationFactory()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    // Remove existing service registrations
                    var bookServiceDescriptor = services.SingleOrDefault(
                        d => d.ServiceType == typeof(IEntityService<Book>));
                    if (bookServiceDescriptor != null)
                        services.Remove(bookServiceDescriptor);

                    var movieServiceDescriptor = services.SingleOrDefault(
                        d => d.ServiceType == typeof(IEntityService<Movie>));
                    if (movieServiceDescriptor != null)
                        services.Remove(movieServiceDescriptor);

                    var gameServiceDescriptor = services.SingleOrDefault(
                        d => d.ServiceType == typeof(IEntityService<Game>));
                    if (gameServiceDescriptor != null)
                        services.Remove(gameServiceDescriptor);

                    // Add mock services
                    services.AddScoped<IEntityService<Book>>(_ => _bookServiceMock.Object);
                    services.AddScoped<IEntityService<Movie>>(_ => _movieServiceMock.Object);
                    services.AddScoped<IEntityService<Game>>(_ => _gameServiceMock.Object);
                });
            });

        _client = _factory.CreateClient();

        _bookFaker = new Faker<Book>()
            .RuleFor(b => b.Id, f => f.Random.AlphaNumeric(24))
            .RuleFor(b => b.Type, _ => MediaTypes.Books)
            .RuleFor(b => b.Title, f => f.Lorem.Sentence())
            .RuleFor(b => b.Authors, f => new List<string> { f.Person.FullName })
            .RuleFor(b => b.Genres, f => new List<string> { f.Lorem.Word() })
            .RuleFor(b => b.ISBN, f => f.Random.Replace("###-#-###-#####-#"))
            .RuleFor(b => b.Format, f => f.PickRandom("Hardcover", "Paperback", "eBook"))
            .RuleFor(b => b.Pages, f => f.Random.Int(100, 1000))
            .RuleFor(b => b.PublicationDate, f => f.Date.Past().ToString("yyyy-MM-dd"))
            .RuleFor(b => b.Publisher, f => f.Company.CompanyName())
            .RuleFor(b => b.Plot, f => f.Lorem.Paragraph())
            .RuleFor(b => b.Subtitle, f => f.Lorem.Sentence());

        _movieFaker = new Faker<Movie>()
            .RuleFor(m => m.Id, f => f.Random.AlphaNumeric(24))
            .RuleFor(m => m.Type, _ => MediaTypes.Movies)
            .RuleFor(m => m.Title, f => f.Lorem.Sentence())
            .RuleFor(m => m.Studios, f => new List<string> { f.Company.CompanyName() })
            .RuleFor(m => m.Genres, f => new List<string> { f.Lorem.Word() })
            .RuleFor(m => m.Format, f => f.PickRandom("Blu-ray", "DVD", "Digital"))
            .RuleFor(m => m.Runtime, f => f.Random.Int(90, 180))
            .RuleFor(m => m.ReleaseDate, f => f.Date.Past().ToString("yyyy-MM-dd"))
            .RuleFor(m => m.Rating, f => f.PickRandom("G", "PG", "PG-13", "R"))
            .RuleFor(m => m.Plot, f => f.Lorem.Paragraph())
            .RuleFor(m => m.IsTvSeries, f => f.Random.Bool());

        _gameFaker = new Faker<Game>()
            .RuleFor(g => g.Id, f => f.Random.AlphaNumeric(24))
            .RuleFor(g => g.Type, _ => MediaTypes.Games)
            .RuleFor(g => g.Title, f => f.Lorem.Sentence())
            .RuleFor(g => g.Format, f => f.PickRandom("Disc", "Cartridge", "Digital"))
            .RuleFor(g => g.Platform, f => f.PickRandom("PC", "Xbox", "PlayStation", "Switch"))
            .RuleFor(g => g.Developers, f => new List<string> { f.Company.CompanyName() })
            .RuleFor(g => g.Publishers, f => new List<string> { f.Company.CompanyName() })
            .RuleFor(g => g.Genres, f => new List<string> { f.Lorem.Word() })
            .RuleFor(g => g.ReleaseDate, f => f.Date.Past().ToString("yyyy-MM-dd"))
            .RuleFor(g => g.Rating, f => f.PickRandom("E", "T", "M"))
            .RuleFor(g => g.Description, f => f.Lorem.Paragraph());
    }

    [Test]
    public async Task GetGames_ShouldReturnAllGames()
    {
        // Arrange
        var games = _gameFaker.Generate(3);
        _gameServiceMock.Setup(s => s.GetListAsync(It.IsAny<CancellationToken>())).ReturnsAsync(games);

        // Act
        var response = await _client.GetAsync("/Games");
        var result = await response.Content.ReadFromJsonAsync<List<Game>>(_jsonOptions);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Count, Is.EqualTo(3));
        _gameServiceMock.Verify(s => s.GetListAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task GetGameById_ShouldReturnGame_WhenGameExists()
    {
        // Arrange
        var gameId = "507f1f77bcf86cd799439011";
        var expectedGame = _gameFaker.Clone().RuleFor(g => g.Id, gameId).Generate();
        _gameServiceMock.Setup(s => s.GetAsync(gameId, It.IsAny<CancellationToken>())).ReturnsAsync(expectedGame);

        // Act
        var response = await _client.GetAsync($"/Games/{gameId}");
        var result = await response.Content.ReadFromJsonAsync<Game>(_jsonOptions);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Id, Is.EqualTo(gameId));
        Assert.That(result.Title, Is.EqualTo(expectedGame.Title));
        _gameServiceMock.Verify(s => s.GetAsync(gameId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task CreateGame_ShouldReturnCreated_WhenGameIsValid()
    {
        // Arrange
        var newGame = _gameFaker.Generate();
        _gameServiceMock.Setup(s => s.CreateAsync(newGame, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        // Act
        var response = await _client.PostAsJsonAsync("/Games", newGame);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        Assert.That(response.Headers.Location, Is.Not.Null);
        _gameServiceMock.Verify(s => s.CreateAsync(It.IsAny<Game>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task CreateGame_ShouldReturnBadRequest_WhenGameIsEmpty()
    {
        // Arrange
        var emptyGame = new Game();

        // Act
        var response = await _client.PostAsJsonAsync("/Games", emptyGame);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        _gameServiceMock.Verify(s => s.CreateAsync(It.IsAny<Game>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task UpdateGame_ShouldReturnOk_WhenIdsMatch()
    {
        // Arrange
        var gameId = "507f1f77bcf86cd799439011";
        var updatedGame = _gameFaker.Clone().RuleFor(g => g.Id, gameId).Generate();
        var updateResult = Mock.Of<ReplaceOneResult>();
        
        // Mock GetAsync to return the existing entity
        _gameServiceMock.Setup(s => s.GetAsync(gameId, It.IsAny<CancellationToken>())).Returns(Task.FromResult<Game?>(updatedGame));
        _gameServiceMock.Setup(s => s.UpdateAsync(gameId, It.IsAny<Game>(), It.IsAny<CancellationToken>())).Returns(Task.FromResult(updateResult));

        // Act
        var response = await _client.PutAsJsonAsync($"/Games/{gameId}", updatedGame);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        _gameServiceMock.Verify(s => s.UpdateAsync(gameId, It.IsAny<Game>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task UpdateGame_ShouldReturnBadRequest_WhenIdsDontMatch()
    {
        // Arrange
        var pathId = "507f1f77bcf86cd799439011";
        var entityId = "507f1f77bcf86cd799439012";
        var updatedGame = _gameFaker.Clone().RuleFor(g => g.Id, entityId).Generate();

        // Act
        var response = await _client.PutAsJsonAsync($"/Games/{pathId}", updatedGame);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        _gameServiceMock.Verify(s => s.UpdateAsync(It.IsAny<string>(), It.IsAny<Game>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task DeleteGame_ShouldReturnOk()
    {
        // Arrange
        var gameId = "507f1f77bcf86cd799439011";
        var deleteResult = Mock.Of<DeleteResult>();
        _gameServiceMock.Setup(s => s.RemoveAsync(gameId, It.IsAny<CancellationToken>())).Returns(Task.FromResult(deleteResult));

        // Act
        var response = await _client.DeleteAsync($"/Games/{gameId}");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        _gameServiceMock.Verify(s => s.RemoveAsync(gameId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task GetGames_ShouldReturnEmptyList_WhenNoGamesExist()
    {
        // Arrange
        var emptyList = new List<Game>();
        _gameServiceMock.Setup(s => s.GetListAsync(It.IsAny<CancellationToken>())).ReturnsAsync(emptyList);

        // Act
        var response = await _client.GetAsync("/Games");
        var result = await response.Content.ReadFromJsonAsync<List<Game>>(_jsonOptions);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Count, Is.EqualTo(0));
    }

    [Test]
    public async Task SearchGames_ShouldReturnMatchingGames()
    {
        // Arrange
        var searchText = "adventure";
        var orderBy = "title:asc";
        var games = _gameFaker.Generate(2);
        _gameServiceMock.Setup(s => s.SearchAsync(searchText, orderBy, It.IsAny<CancellationToken>())).ReturnsAsync(games);

        // Act
        var response = await _client.GetAsync($"/Games/search?searchText={searchText}&orderBy={orderBy}");
        var result = await response.Content.ReadFromJsonAsync<List<Game>>(_jsonOptions);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Count, Is.EqualTo(2));
        _gameServiceMock.Verify(s => s.SearchAsync(searchText, orderBy, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task SearchGames_ShouldReturnEmptyList_WhenNoMatchesFound()
    {
        // Arrange
        var searchText = "nonexistent";
        var orderBy = "title:asc";
        var emptyList = new List<Game>();
        _gameServiceMock.Setup(s => s.SearchAsync(searchText, orderBy, It.IsAny<CancellationToken>())).ReturnsAsync(emptyList);

        // Act
        var response = await _client.GetAsync($"/Games/search?searchText={searchText}&orderBy={orderBy}");
        var result = await response.Content.ReadFromJsonAsync<List<Game>>(_jsonOptions);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Count, Is.EqualTo(0));
    }

    [Test]
    public async Task SearchGames_ShouldSupportDifferentOrderByFormats()
    {
        // Arrange
        var searchText = "game";
        var orderByOptions = new[] { "title:asc", "title:desc" };
        var games = _gameFaker.Generate(2);

        foreach (var orderBy in orderByOptions)
        {
            _gameServiceMock.Setup(s => s.SearchAsync(searchText, orderBy, It.IsAny<CancellationToken>())).ReturnsAsync(games);

            // Act
            var response = await _client.GetAsync($"/Games/search?searchText={searchText}&orderBy={orderBy}");
            var result = await response.Content.ReadFromJsonAsync<List<Game>>(_jsonOptions);

            // Assert
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(result, Is.Not.Null);
            Assert.That(result!.Count, Is.EqualTo(2));
        }
    }

    [TearDown]
    public void TearDown()
    {
        _client?.Dispose();
        _factory?.Dispose();
    }

    [Test]
    public async Task GetBooks_ShouldReturnAllBooks()
    {
        // Arrange
        var books = _bookFaker.Generate(3);
        _bookServiceMock.Setup(s => s.GetListAsync(It.IsAny<CancellationToken>())).ReturnsAsync(books);

        // Act
        var response = await _client.GetAsync("/Books");
        var result = await response.Content.ReadFromJsonAsync<List<Book>>(_jsonOptions);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Count, Is.EqualTo(3));
        _bookServiceMock.Verify(s => s.GetListAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task GetMovies_ShouldReturnAllMovies()
    {
        // Arrange
        var movies = _movieFaker.Generate(2);
        _movieServiceMock.Setup(s => s.GetListAsync(It.IsAny<CancellationToken>())).ReturnsAsync(movies);

        // Act
        var response = await _client.GetAsync("/Movies");
        var result = await response.Content.ReadFromJsonAsync<List<Movie>>(_jsonOptions);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Count, Is.EqualTo(2));
        _movieServiceMock.Verify(s => s.GetListAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task SearchBooks_ShouldReturnMatchingBooks()
    {
        // Arrange
        var searchText = "fantasy";
        var orderBy = "title:asc";
        var books = _bookFaker.Generate(2);
        _bookServiceMock.Setup(s => s.SearchAsync(searchText, orderBy, It.IsAny<CancellationToken>())).ReturnsAsync(books);

        // Act
        var response = await _client.GetAsync($"/Books/search?searchText={searchText}&orderBy={orderBy}");
        var result = await response.Content.ReadFromJsonAsync<List<Book>>(_jsonOptions);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Count, Is.EqualTo(2));
        _bookServiceMock.Verify(s => s.SearchAsync(searchText, orderBy, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task GetBookById_ShouldReturnBook_WhenBookExists()
    {
        // Arrange
        var bookId = "507f1f77bcf86cd799439011";
        var expectedBook = _bookFaker.Clone().RuleFor(b => b.Id, bookId).Generate();
        _bookServiceMock.Setup(s => s.GetAsync(bookId, It.IsAny<CancellationToken>())).ReturnsAsync(expectedBook);

        // Act
        var response = await _client.GetAsync($"/Books/{bookId}");
        var result = await response.Content.ReadFromJsonAsync<Book>(_jsonOptions);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Id, Is.EqualTo(bookId));
        Assert.That(result.Title, Is.EqualTo(expectedBook.Title));
        _bookServiceMock.Verify(s => s.GetAsync(bookId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task GetBookById_ShouldReturnNotFound_WhenBookDoesNotExist()
    {
        // Arrange
        var bookId = "507f1f77bcf86cd799439011";
        _bookServiceMock.Setup(s => s.GetAsync(bookId, It.IsAny<CancellationToken>())).ReturnsAsync((Book)null!);

        // Act
        var response = await _client.GetAsync($"/Books/{bookId}");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        _bookServiceMock.Verify(s => s.GetAsync(bookId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task CreateBook_ShouldReturnCreated_WhenBookIsValid()
    {
        // Arrange
        var newBook = _bookFaker.Generate();
        _bookServiceMock.Setup(s => s.CreateAsync(newBook, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        // Act
        var response = await _client.PostAsJsonAsync("/Books", newBook);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        Assert.That(response.Headers.Location, Is.Not.Null);
        _bookServiceMock.Verify(s => s.CreateAsync(It.IsAny<Book>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task CreateBook_ShouldReturnBadRequest_WhenBookIsEmpty()
    {
        // Arrange
        var emptyBook = new Book();

        // Act
        var response = await _client.PostAsJsonAsync("/Books", emptyBook);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        _bookServiceMock.Verify(s => s.CreateAsync(It.IsAny<Book>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task UpdateBook_ShouldReturnOk_WhenIdsMatch()
    {
        // Arrange
        var bookId = "507f1f77bcf86cd799439011";
        var updatedBook = _bookFaker.Clone().RuleFor(b => b.Id, bookId).Generate();
        var updateResult = Mock.Of<ReplaceOneResult>();
        
        // Mock GetAsync to return the existing entity
        _bookServiceMock.Setup(s => s.GetAsync(bookId, It.IsAny<CancellationToken>())).Returns(Task.FromResult<Book?>(updatedBook));
        _bookServiceMock.Setup(s => s.UpdateAsync(bookId, It.IsAny<Book>(), It.IsAny<CancellationToken>())).Returns(Task.FromResult(updateResult));

        // Act
        var response = await _client.PutAsJsonAsync($"/Books/{bookId}", updatedBook);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        _bookServiceMock.Verify(s => s.UpdateAsync(bookId, It.IsAny<Book>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task UpdateBook_ShouldReturnBadRequest_WhenIdsDontMatch()
    {
        // Arrange
        var pathId = "507f1f77bcf86cd799439011";
        var entityId = "507f1f77bcf86cd799439012";
        var updatedBook = _bookFaker.Clone().RuleFor(b => b.Id, entityId).Generate();

        // Act
        var response = await _client.PutAsJsonAsync($"/Books/{pathId}", updatedBook);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        _bookServiceMock.Verify(s => s.UpdateAsync(It.IsAny<string>(), It.IsAny<Book>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task DeleteBook_ShouldReturnOk()
    {
        // Arrange
        var bookId = "507f1f77bcf86cd799439011";
        var deleteResult = Mock.Of<DeleteResult>();
        _bookServiceMock.Setup(s => s.RemoveAsync(bookId, It.IsAny<CancellationToken>())).Returns(Task.FromResult(deleteResult));

        // Act
        var response = await _client.DeleteAsync($"/Books/{bookId}");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        _bookServiceMock.Verify(s => s.RemoveAsync(bookId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task GetBooks_ShouldReturnEmptyList_WhenNoBooksExist()
    {
        // Arrange
        var emptyList = new List<Book>();
        _bookServiceMock.Setup(s => s.GetListAsync(It.IsAny<CancellationToken>())).ReturnsAsync(emptyList);

        // Act
        var response = await _client.GetAsync("/Books");
        var result = await response.Content.ReadFromJsonAsync<List<Book>>(_jsonOptions);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Count, Is.EqualTo(0));
    }

    [Test]
    public async Task SearchBooks_ShouldReturnEmptyList_WhenNoMatchesFound()
    {
        // Arrange
        var searchText = "nonexistent";
        var orderBy = "title:asc";
        var emptyList = new List<Book>();
        _bookServiceMock.Setup(s => s.SearchAsync(searchText, orderBy, It.IsAny<CancellationToken>())).ReturnsAsync(emptyList);

        // Act
        var response = await _client.GetAsync($"/Books/search?searchText={searchText}&orderBy={orderBy}");
        var result = await response.Content.ReadFromJsonAsync<List<Book>>(_jsonOptions);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Count, Is.EqualTo(0));
    }

    [Test]
    public async Task GetMovieById_ShouldReturnMovie_WhenMovieExists()
    {
        // Arrange
        var movieId = "507f1f77bcf86cd799439011";
        var expectedMovie = _movieFaker.Clone().RuleFor(m => m.Id, movieId).Generate();
        _movieServiceMock.Setup(s => s.GetAsync(movieId, It.IsAny<CancellationToken>())).ReturnsAsync(expectedMovie);

        // Act
        var response = await _client.GetAsync($"/Movies/{movieId}");
        var result = await response.Content.ReadFromJsonAsync<Movie>(_jsonOptions);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Id, Is.EqualTo(movieId));
        Assert.That(result.Title, Is.EqualTo(expectedMovie.Title));
        _movieServiceMock.Verify(s => s.GetAsync(movieId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task CreateMovie_ShouldReturnCreated_WhenMovieIsValid()
    {
        // Arrange
        var newMovie = _movieFaker.Generate();
        _movieServiceMock.Setup(s => s.CreateAsync(newMovie, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        // Act
        var response = await _client.PostAsJsonAsync("/Movies", newMovie);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        Assert.That(response.Headers.Location, Is.Not.Null);
        _movieServiceMock.Verify(s => s.CreateAsync(It.IsAny<Movie>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task SearchBooks_ShouldSupportDifferentOrderByFormats()
    {
        // Arrange
        var searchText = "book";
        var orderByOptions = new[] { "title:asc", "title:desc" };
        var books = _bookFaker.Generate(2);

        foreach (var orderBy in orderByOptions)
        {
            _bookServiceMock.Setup(s => s.SearchAsync(searchText, orderBy, It.IsAny<CancellationToken>())).ReturnsAsync(books);

            // Act
            var response = await _client.GetAsync($"/Books/search?searchText={searchText}&orderBy={orderBy}");
            var result = await response.Content.ReadFromJsonAsync<List<Book>>(_jsonOptions);

            // Assert
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(result, Is.Not.Null);
            Assert.That(result!.Count, Is.EqualTo(2));
        }
    }
}
