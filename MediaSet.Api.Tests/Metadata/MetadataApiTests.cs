using NUnit.Framework;
using Moq;
using MediaSet.Api.Services;
using MediaSet.Api.Models;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace MediaSet.Api.Tests.Metadata;

[TestFixture]
public class MetadataApiTests
{
    private WebApplicationFactory<Program> _factory;
    private HttpClient _client;
    private Mock<IMetadataService> _metadataServiceMock;

    [SetUp]
    public void Setup()
    {
        _metadataServiceMock = new Mock<IMetadataService>();
        
        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    // Remove existing MetadataService registration
                    var metadataServiceDescriptor = services.SingleOrDefault(
                        d => d.ServiceType == typeof(IMetadataService));
                    if (metadataServiceDescriptor != null)
                        services.Remove(metadataServiceDescriptor);

                    // Add mock service
                    services.AddScoped<IMetadataService>(_ => _metadataServiceMock.Object);
                });
            });

        _client = _factory.CreateClient();
    }

    [TearDown]
    public void TearDown()
    {
        _client?.Dispose();
        _factory?.Dispose();
    }

    [Test]
    public async Task GetBookFormats_ShouldReturnFormats()
    {
        // Arrange
        var expectedFormats = new List<string> { "Hardcover", "Paperback", "eBook", "Audiobook" };
        _metadataServiceMock.Setup(s => s.GetBookFormats()).ReturnsAsync(expectedFormats);

        // Act
        var response = await _client.GetAsync("/metadata/formats/books");
        var result = await response.Content.ReadFromJsonAsync<List<string>>();

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Count, Is.EqualTo(4));
        Assert.That(result, Does.Contain("Hardcover"));
        Assert.That(result, Does.Contain("Paperback"));
        Assert.That(result, Does.Contain("eBook"));
        Assert.That(result, Does.Contain("Audiobook"));
        _metadataServiceMock.Verify(s => s.GetBookFormats(), Times.Once);
    }

    [Test]
    public async Task GetMovieFormats_ShouldReturnFormats()
    {
        // Arrange
        var expectedFormats = new List<string> { "Blu-ray", "DVD", "Digital", "4K UHD" };
        _metadataServiceMock.Setup(s => s.GetMovieFormats()).ReturnsAsync(expectedFormats);

        // Act
        var response = await _client.GetAsync("/metadata/formats/movies");
        var result = await response.Content.ReadFromJsonAsync<List<string>>();

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Count, Is.EqualTo(4));
        Assert.That(result, Does.Contain("Blu-ray"));
        Assert.That(result, Does.Contain("DVD"));
        Assert.That(result, Does.Contain("Digital"));
        Assert.That(result, Does.Contain("4K UHD"));
        _metadataServiceMock.Verify(s => s.GetMovieFormats(), Times.Once);
    }

    [Test]
    public async Task GetBookGenres_ShouldReturnGenres()
    {
        // Arrange
        var expectedGenres = new List<string> { "Fiction", "Non-Fiction", "Science Fiction", "Fantasy", "Mystery" };
        _metadataServiceMock.Setup(s => s.GetBookGenres()).ReturnsAsync(expectedGenres);

        // Act
        var response = await _client.GetAsync("/metadata/genres/books");
        var result = await response.Content.ReadFromJsonAsync<List<string>>();

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Count, Is.EqualTo(5));
        Assert.That(result, Does.Contain("Fiction"));
        Assert.That(result, Does.Contain("Fantasy"));
        _metadataServiceMock.Verify(s => s.GetBookGenres(), Times.Once);
    }

    [Test]
    public async Task GetMovieGenres_ShouldReturnGenres()
    {
        // Arrange
        var expectedGenres = new List<string> { "Action", "Comedy", "Drama", "Horror", "Sci-Fi", "Thriller" };
        _metadataServiceMock.Setup(s => s.GetMovieGenres()).ReturnsAsync(expectedGenres);

        // Act
        var response = await _client.GetAsync("/metadata/genres/movies");
        var result = await response.Content.ReadFromJsonAsync<List<string>>();

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Count, Is.EqualTo(6));
        Assert.That(result, Does.Contain("Action"));
        Assert.That(result, Does.Contain("Horror"));
        _metadataServiceMock.Verify(s => s.GetMovieGenres(), Times.Once);
    }

    [Test]
    public async Task GetStudios_ShouldReturnStudios()
    {
        // Arrange
        var expectedStudios = new List<string> { "Warner Bros.", "Universal Pictures", "Disney", "Paramount" };
        _metadataServiceMock.Setup(s => s.GetMovieStudios()).ReturnsAsync(expectedStudios);

        // Act
        var response = await _client.GetAsync("/metadata/studios");
        var result = await response.Content.ReadFromJsonAsync<List<string>>();

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Count, Is.EqualTo(4));
        Assert.That(result, Does.Contain("Warner Bros."));
        Assert.That(result, Does.Contain("Disney"));
        _metadataServiceMock.Verify(s => s.GetMovieStudios(), Times.Once);
    }

    [Test]
    public async Task GetAuthors_ShouldReturnAuthors()
    {
        // Arrange
        var expectedAuthors = new List<string> { "J.K. Rowling", "Stephen King", "Agatha Christie", "J.R.R. Tolkien" };
        _metadataServiceMock.Setup(s => s.GetBookAuthors()).ReturnsAsync(expectedAuthors);

        // Act
        var response = await _client.GetAsync("/metadata/authors");
        var result = await response.Content.ReadFromJsonAsync<List<string>>();

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Count, Is.EqualTo(4));
        Assert.That(result, Does.Contain("J.K. Rowling"));
        Assert.That(result, Does.Contain("Stephen King"));
        _metadataServiceMock.Verify(s => s.GetBookAuthors(), Times.Once);
    }

    [Test]
    public async Task GetPublishers_ShouldReturnPublishers()
    {
        // Arrange
        var expectedPublishers = new List<string> { "Penguin Random House", "HarperCollins", "Simon & Schuster", "Hachette" };
        _metadataServiceMock.Setup(s => s.GetBookPublishers()).ReturnsAsync(expectedPublishers);

        // Act
        var response = await _client.GetAsync("/metadata/publishers");
        var result = await response.Content.ReadFromJsonAsync<List<string>>();

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Count, Is.EqualTo(4));
        Assert.That(result, Does.Contain("Penguin Random House"));
        Assert.That(result, Does.Contain("Simon & Schuster"));
        _metadataServiceMock.Verify(s => s.GetBookPublishers(), Times.Once);
    }

    [Test]
    public async Task GetFormats_ShouldReturnEmptyList_WhenNoFormatsExist()
    {
        // Arrange
        var emptyList = new List<string>();
        _metadataServiceMock.Setup(s => s.GetBookFormats()).ReturnsAsync(emptyList);

        // Act
        var response = await _client.GetAsync("/metadata/formats/books");
        var result = await response.Content.ReadFromJsonAsync<List<string>>();

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Count, Is.EqualTo(0));
        _metadataServiceMock.Verify(s => s.GetBookFormats(), Times.Once);
    }

    [Test]
    public async Task GetGenres_ShouldReturnEmptyList_WhenNoGenresExist()
    {
        // Arrange
        var emptyList = new List<string>();
        _metadataServiceMock.Setup(s => s.GetMovieGenres()).ReturnsAsync(emptyList);

        // Act
        var response = await _client.GetAsync("/metadata/genres/movies");
        var result = await response.Content.ReadFromJsonAsync<List<string>>();

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Count, Is.EqualTo(0));
        _metadataServiceMock.Verify(s => s.GetMovieGenres(), Times.Once);
    }

    [Test]
    public async Task GetStudios_ShouldReturnEmptyList_WhenNoStudiosExist()
    {
        // Arrange
        var emptyList = new List<string>();
        _metadataServiceMock.Setup(s => s.GetMovieStudios()).ReturnsAsync(emptyList);

        // Act
        var response = await _client.GetAsync("/metadata/studios");
        var result = await response.Content.ReadFromJsonAsync<List<string>>();

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Count, Is.EqualTo(0));
        _metadataServiceMock.Verify(s => s.GetMovieStudios(), Times.Once);
    }

    [Test]
    public async Task GetAuthors_ShouldReturnEmptyList_WhenNoAuthorsExist()
    {
        // Arrange
        var emptyList = new List<string>();
        _metadataServiceMock.Setup(s => s.GetBookAuthors()).ReturnsAsync(emptyList);

        // Act
        var response = await _client.GetAsync("/metadata/authors");
        var result = await response.Content.ReadFromJsonAsync<List<string>>();

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Count, Is.EqualTo(0));
        _metadataServiceMock.Verify(s => s.GetBookAuthors(), Times.Once);
    }

    [Test]
    public async Task GetPublishers_ShouldReturnEmptyList_WhenNoPublishersExist()
    {
        // Arrange
        var emptyList = new List<string>();
        _metadataServiceMock.Setup(s => s.GetBookPublishers()).ReturnsAsync(emptyList);

        // Act
        var response = await _client.GetAsync("/metadata/publishers");
        var result = await response.Content.ReadFromJsonAsync<List<string>>();

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Count, Is.EqualTo(0));
        _metadataServiceMock.Verify(s => s.GetBookPublishers(), Times.Once);
    }

    [Test]
    public async Task GetFormats_ShouldBeCaseInsensitive_ForMediaType()
    {
        // Arrange
        var expectedFormats = new List<string> { "Hardcover", "Paperback" };
        _metadataServiceMock.Setup(s => s.GetBookFormats()).ReturnsAsync(expectedFormats);

        var mediaTypes = new[] { "Books", "books", "BOOKS", "bOoKs" };

        foreach (var mediaType in mediaTypes)
        {
            // Act
            var response = await _client.GetAsync($"/metadata/formats/{mediaType}");

            // Assert
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK), $"Failed for media type: {mediaType}");
        }
    }

    [Test]
    public async Task GetGenres_ShouldBeCaseInsensitive_ForMediaType()
    {
        // Arrange
        var expectedGenres = new List<string> { "Action", "Comedy" };
        _metadataServiceMock.Setup(s => s.GetMovieGenres()).ReturnsAsync(expectedGenres);

        var mediaTypes = new[] { "Movies", "movies", "MOVIES", "mOvIeS" };

        foreach (var mediaType in mediaTypes)
        {
            // Act
            var response = await _client.GetAsync($"/metadata/genres/{mediaType}");

            // Assert
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK), $"Failed for media type: {mediaType}");
        }
    }
}
