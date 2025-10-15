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
using MongoDB.Driver;

namespace MediaSet.Api.Tests.Entities;

[TestFixture]
public class EntityApiTests
{
    private WebApplicationFactory<Program> _factory;
    private HttpClient _client;
    private Mock<IEntityService<Book>> _bookServiceMock;
    private Mock<IEntityService<Movie>> _movieServiceMock;
    private Faker<Book> _bookFaker;
    private Faker<Movie> _movieFaker;
    private JsonSerializerOptions _jsonOptions;

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
        
        _factory = new WebApplicationFactory<Program>()
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

                    // Add mock services
                    services.AddScoped<IEntityService<Book>>(_ => _bookServiceMock.Object);
                    services.AddScoped<IEntityService<Movie>>(_ => _movieServiceMock.Object);
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
        _bookServiceMock.Setup(s => s.GetListAsync()).ReturnsAsync(books);

        // Act
        var response = await _client.GetAsync("/Books");
        var result = await response.Content.ReadFromJsonAsync<List<Book>>(_jsonOptions);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Count, Is.EqualTo(3));
        _bookServiceMock.Verify(s => s.GetListAsync(), Times.Once);
    }

    [Test]
    public async Task GetMovies_ShouldReturnAllMovies()
    {
        // Arrange
        var movies = _movieFaker.Generate(2);
        _movieServiceMock.Setup(s => s.GetListAsync()).ReturnsAsync(movies);

        // Act
        var response = await _client.GetAsync("/Movies");
        var result = await response.Content.ReadFromJsonAsync<List<Movie>>(_jsonOptions);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Count, Is.EqualTo(2));
        _movieServiceMock.Verify(s => s.GetListAsync(), Times.Once);
    }

    [Test]
    public async Task SearchBooks_ShouldReturnMatchingBooks()
    {
        // Arrange
        var searchText = "fantasy";
        var orderBy = "title:asc";
        var books = _bookFaker.Generate(2);
        _bookServiceMock.Setup(s => s.SearchAsync(searchText, orderBy)).ReturnsAsync(books);

        // Act
        var response = await _client.GetAsync($"/Books/search?searchText={searchText}&orderBy={orderBy}");
        var result = await response.Content.ReadFromJsonAsync<List<Book>>(_jsonOptions);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Count, Is.EqualTo(2));
        _bookServiceMock.Verify(s => s.SearchAsync(searchText, orderBy), Times.Once);
    }

    [Test]
    public async Task GetBookById_ShouldReturnBook_WhenBookExists()
    {
        // Arrange
        var bookId = "507f1f77bcf86cd799439011";
        var expectedBook = _bookFaker.Clone().RuleFor(b => b.Id, bookId).Generate();
        _bookServiceMock.Setup(s => s.GetAsync(bookId)).ReturnsAsync(expectedBook);

        // Act
        var response = await _client.GetAsync($"/Books/{bookId}");
        var result = await response.Content.ReadFromJsonAsync<Book>(_jsonOptions);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Id, Is.EqualTo(bookId));
        Assert.That(result.Title, Is.EqualTo(expectedBook.Title));
        _bookServiceMock.Verify(s => s.GetAsync(bookId), Times.Once);
    }

    [Test]
    public async Task GetBookById_ShouldReturnNotFound_WhenBookDoesNotExist()
    {
        // Arrange
        var bookId = "507f1f77bcf86cd799439011";
        _bookServiceMock.Setup(s => s.GetAsync(bookId)).ReturnsAsync((Book)null!);

        // Act
        var response = await _client.GetAsync($"/Books/{bookId}");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        _bookServiceMock.Verify(s => s.GetAsync(bookId), Times.Once);
    }

    [Test]
    public async Task CreateBook_ShouldReturnCreated_WhenBookIsValid()
    {
        // Arrange
        var newBook = _bookFaker.Generate();
        _bookServiceMock.Setup(s => s.CreateAsync(newBook)).Returns(Task.CompletedTask);

        // Act
        var response = await _client.PostAsJsonAsync("/Books", newBook);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        Assert.That(response.Headers.Location, Is.Not.Null);
        _bookServiceMock.Verify(s => s.CreateAsync(It.IsAny<Book>()), Times.Once);
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
        _bookServiceMock.Verify(s => s.CreateAsync(It.IsAny<Book>()), Times.Never);
    }

    [Test]
    public async Task UpdateBook_ShouldReturnOk_WhenIdsMatch()
    {
        // Arrange
        var bookId = "507f1f77bcf86cd799439011";
        var updatedBook = _bookFaker.Clone().RuleFor(b => b.Id, bookId).Generate();
        var updateResult = Mock.Of<ReplaceOneResult>();
        _bookServiceMock.Setup(s => s.UpdateAsync(bookId, It.IsAny<Book>())).ReturnsAsync(updateResult);

        // Act
        var response = await _client.PutAsJsonAsync($"/Books/{bookId}", updatedBook);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        _bookServiceMock.Verify(s => s.UpdateAsync(bookId, It.IsAny<Book>()), Times.Once);
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
        _bookServiceMock.Verify(s => s.UpdateAsync(It.IsAny<string>(), It.IsAny<Book>()), Times.Never);
    }

    [Test]
    public async Task DeleteBook_ShouldReturnOk()
    {
        // Arrange
        var bookId = "507f1f77bcf86cd799439011";
        var deleteResult = Mock.Of<DeleteResult>();
        _bookServiceMock.Setup(s => s.RemoveAsync(bookId)).ReturnsAsync(deleteResult);

        // Act
        var response = await _client.DeleteAsync($"/Books/{bookId}");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        _bookServiceMock.Verify(s => s.RemoveAsync(bookId), Times.Once);
    }

    [Test]
    public async Task GetBooks_ShouldReturnEmptyList_WhenNoBooksExist()
    {
        // Arrange
        var emptyList = new List<Book>();
        _bookServiceMock.Setup(s => s.GetListAsync()).ReturnsAsync(emptyList);

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
        _bookServiceMock.Setup(s => s.SearchAsync(searchText, orderBy)).ReturnsAsync(emptyList);

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
        _movieServiceMock.Setup(s => s.GetAsync(movieId)).ReturnsAsync(expectedMovie);

        // Act
        var response = await _client.GetAsync($"/Movies/{movieId}");
        var result = await response.Content.ReadFromJsonAsync<Movie>(_jsonOptions);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Id, Is.EqualTo(movieId));
        Assert.That(result.Title, Is.EqualTo(expectedMovie.Title));
        _movieServiceMock.Verify(s => s.GetAsync(movieId), Times.Once);
    }

    [Test]
    public async Task CreateMovie_ShouldReturnCreated_WhenMovieIsValid()
    {
        // Arrange
        var newMovie = _movieFaker.Generate();
        _movieServiceMock.Setup(s => s.CreateAsync(newMovie)).Returns(Task.CompletedTask);

        // Act
        var response = await _client.PostAsJsonAsync("/Movies", newMovie);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        Assert.That(response.Headers.Location, Is.Not.Null);
        _movieServiceMock.Verify(s => s.CreateAsync(It.IsAny<Movie>()), Times.Once);
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
            _bookServiceMock.Setup(s => s.SearchAsync(searchText, orderBy)).ReturnsAsync(books);

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
