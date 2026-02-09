using MediaSet.Api.Infrastructure.Lookup.Models;
using NUnit.Framework;
using Moq;
using MediaSet.Api.Infrastructure.Lookup.Clients.OpenLibrary;
using MediaSet.Api.Infrastructure.Lookup.Clients.UpcItemDb;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;

namespace MediaSet.Api.Tests.Features.Lookup.Endpoints;

[TestFixture]
public class LookupApiTests : IntegrationTestBase
{
    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;
    private Mock<IOpenLibraryClient> _openLibraryClientMock = null!;
    private Mock<IUpcItemDbClient> _upcItemDbClientMock = null!;

    [SetUp]
    public void Setup()
    {
        _openLibraryClientMock = new Mock<IOpenLibraryClient>();
        _upcItemDbClientMock = new Mock<IUpcItemDbClient>();

        _factory = CreateWebApplicationFactory()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    // Remove existing client registrations and replace with mocks
                    var openLibraryDescriptor = services.SingleOrDefault(
                        d => d.ServiceType == typeof(IOpenLibraryClient));
                    if (openLibraryDescriptor != null)
                    {
                        services.Remove(openLibraryDescriptor);
                    }

                    var upcItemDbDescriptor = services.SingleOrDefault(
                        d => d.ServiceType == typeof(IUpcItemDbClient));
                    if (upcItemDbDescriptor != null)
                    {
                        services.Remove(upcItemDbDescriptor);
                    }

                    // Add mock clients FIRST
                    services.AddScoped<IOpenLibraryClient>(_ => _openLibraryClientMock.Object);
                    services.AddScoped<IUpcItemDbClient>(_ => _upcItemDbClientMock.Object);
                    
                    // Ensure the BookLookupStrategy is registered with our mocks
                    // Remove existing strategy if it exists
                    var strategyDescriptor = services.SingleOrDefault(
                        d => d.ServiceType == typeof(MediaSet.Api.Infrastructure.Lookup.Strategies.ILookupStrategy<BookResponse>));
                    if (strategyDescriptor != null)
                    {
                        services.Remove(strategyDescriptor);
                    }

                    // Re-register with our mocked clients
                    services.AddScoped<MediaSet.Api.Infrastructure.Lookup.Strategies.ILookupStrategy<BookResponse>, MediaSet.Api.Infrastructure.Lookup.Strategies.BookLookupStrategy>();
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
    public async Task GetBookByIsbn_ShouldReturnBook_WhenValidIsbn()
    {
        // Arrange
        var entityType = "books";
        var identifierType = "isbn";
        var identifierValue = "9780134685991";
        var expectedResponse = new BookResponse(
            "Effective Java",
            "Best Practices for the Java Platform",
            new List<Author> { new Author("Joshua Bloch", "/authors/OL1234A") },
            416,
            new List<Publisher> { new Publisher("Addison-Wesley") },
            "2018-01-06",
            new List<Subject> { new Subject("Programming", "/subjects/programming") },
            "Hardcover"
        );

        _openLibraryClientMock
            .Setup(c  => c.GetReadableBookByIsbnAsync(identifierValue, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResponse);

        // Act
        var response = await _client.GetAsync($"/lookup/{entityType}/{identifierType}/{identifierValue}");
        var result = await response.Content.ReadFromJsonAsync<BookResponse>();

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Title, Is.EqualTo("Effective Java"));
        Assert.That(result.Authors.Count, Is.EqualTo(1));
        Assert.That(result.Authors[0].Name, Is.EqualTo("Joshua Bloch"));
        Assert.That(result.NumberOfPages, Is.EqualTo(416));
        _openLibraryClientMock.Verify(c => c.GetReadableBookByIsbnAsync(identifierValue, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task GetBookByIdentifier_ShouldReturnNotFound_WhenBookNotFound()
    {
        // Arrange
        var entityType = "books";
        var identifierType = "isbn";
        var identifierValue = "0000000000000";

        _openLibraryClientMock
            .Setup(c  => c.GetReadableBookByIsbnAsync(identifierValue, It.IsAny<CancellationToken>()))
            .ReturnsAsync((BookResponse)null!);

        // Act
        var response = await _client.GetAsync($"/lookup/{entityType}/{identifierType}/{identifierValue}");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        _openLibraryClientMock.Verify(c => c.GetReadableBookByIsbnAsync(identifierValue, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task GetBookByLccn_ShouldReturnBook_WhenValidLccn()
    {
        // Arrange
        var entityType = "books";
        var identifierType = "lccn";
        var identifierValue = "2017046873";
        var expectedResponse = new BookResponse(
            "Test Book",
            "A Test",
            new List<Author> { new Author("Test Author", "/authors/OL5678A") },
            300,
            new List<Publisher> { new Publisher("Test Publisher") },
            "2018",
            new List<Subject>(),
            "Paperback"
        );

        _openLibraryClientMock
            .Setup(c  => c.GetReadableBookByLccnAsync(identifierValue, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResponse);

        // Act
        var response = await _client.GetAsync($"/lookup/{entityType}/{identifierType}/{identifierValue}");
        var result = await response.Content.ReadFromJsonAsync<BookResponse>();

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Title, Is.EqualTo("Test Book"));
        _openLibraryClientMock.Verify(c => c.GetReadableBookByLccnAsync(identifierValue, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task GetBookByOclc_ShouldReturnBook_WhenValidOclc()
    {
        // Arrange
        var entityType = "books";
        var identifierType = "oclc";
        var identifierValue = "1004392074";
        var expectedResponse = new BookResponse(
            "OCLC Book",
            "",
            new List<Author> { new Author("OCLC Author", "/authors/OL9012A") },
            250,
            new List<Publisher> { new Publisher("OCLC Publisher") },
            "2019",
            new List<Subject>(),
            null
        );

        _openLibraryClientMock
            .Setup(c  => c.GetReadableBookByOclcAsync(identifierValue, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResponse);

        // Act
        var response = await _client.GetAsync($"/lookup/{entityType}/{identifierType}/{identifierValue}");
        var result = await response.Content.ReadFromJsonAsync<BookResponse>();

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Title, Is.EqualTo("OCLC Book"));
        _openLibraryClientMock.Verify(c => c.GetReadableBookByOclcAsync(identifierValue, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task GetBookByOlid_ShouldReturnBook_WhenValidOlid()
    {
        // Arrange
        var entityType = "books";
        var identifierType = "olid";
        var identifierValue = "OL7353617M";
        var expectedResponse = new BookResponse(
            "OpenLibrary Book",
            "An ID Test",
            new List<Author> { new Author("OL Author", "/authors/OL3456A") },
            200,
            new List<Publisher> { new Publisher("OL Publisher") },
            "2020",
            new List<Subject>(),
            "eBook"
        );

        _openLibraryClientMock
            .Setup(c  => c.GetReadableBookByOlidAsync(identifierValue, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResponse);

        // Act
        var response = await _client.GetAsync($"/lookup/{entityType}/{identifierType}/{identifierValue}");
        var result = await response.Content.ReadFromJsonAsync<BookResponse>();

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Title, Is.EqualTo("OpenLibrary Book"));
        _openLibraryClientMock.Verify(c => c.GetReadableBookByOlidAsync(identifierValue, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task GetBookByIdentifier_ShouldReturnBadRequest_WhenInvalidIdentifierType()
    {
        // Arrange
        var entityType = "books";
        var identifierType = "invalid";
        var identifierValue = "123456";

        // Act
        var response = await _client.GetAsync($"/lookup/{entityType}/{identifierType}/{identifierValue}");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        var content = await response.Content.ReadAsStringAsync();
        Assert.That(content, Does.Contain("Invalid identifier type"));
        _openLibraryClientMock.Verify(c => c.GetReadableBookByIsbnAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _openLibraryClientMock.Verify(c => c.GetReadableBookByLccnAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _openLibraryClientMock.Verify(c => c.GetReadableBookByOclcAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _openLibraryClientMock.Verify(c => c.GetReadableBookByOlidAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task GetBookByIdentifier_ShouldBeCaseInsensitive_ForIdentifierType()
    {
        // Arrange
        var entityType = "books";
        var identifierTypes = new[] { "ISBN", "Isbn", "iSbN", "isbn" };
        var identifierValue = "9780134685991";
        var expectedResponse = new BookResponse(
            "Test Book",
            "",
            new List<Author> { new Author("Author", "/authors/OL1A") },
            300,
            new List<Publisher> { new Publisher("Publisher") },
            "2020",
            new List<Subject>(),
            null
        );

        foreach (var identifierType in identifierTypes)
        {
            _openLibraryClientMock
                .Setup(c  => c.GetReadableBookByIsbnAsync(identifierValue, It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedResponse);

            // Act
            var response = await _client.GetAsync($"/lookup/{entityType}/{identifierType}/{identifierValue}");

            // Assert
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK), $"Failed for identifier type: {identifierType}");
        }
    }

    [Test]
    public async Task GetBookByIdentifier_ShouldHandleBookWithMultipleAuthors()
    {
        // Arrange
        var entityType = "books";
        var identifierType = "isbn";
        var identifierValue = "9780132350884";
        var expectedResponse = new BookResponse(
            "Clean Code",
            "A Handbook of Agile Software Craftsmanship",
            new List<Author>
            {
                new Author("Robert C. Martin", "/authors/OL1A"),
                new Author("Contributor One", "/authors/OL2A"),
                new Author("Contributor Two", "/authors/OL3A")
            },
            464,
            new List<Publisher> { new Publisher("Prentice Hall") },
            "2008",
            new List<Subject> { new Subject("Software Engineering", "/subjects/software") },
            "Hardcover"
        );

        _openLibraryClientMock
            .Setup(c  => c.GetReadableBookByIsbnAsync(identifierValue, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResponse);

        // Act
        var response = await _client.GetAsync($"/lookup/{entityType}/{identifierType}/{identifierValue}");
        var result = await response.Content.ReadFromJsonAsync<BookResponse>();

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Authors.Count, Is.EqualTo(3));
        Assert.That(result.Authors[0].Name, Is.EqualTo("Robert C. Martin"));
        Assert.That(result.Authors[1].Name, Is.EqualTo("Contributor One"));
        Assert.That(result.Authors[2].Name, Is.EqualTo("Contributor Two"));
    }

    [Test]
    public async Task GetBookByIdentifier_ShouldHandleBookWithMultiplePublishers()
    {
        // Arrange
        var entityType = "books";
        var identifierType = "isbn";
        var identifierValue = "9780201616224";
        var expectedResponse = new BookResponse(
            "The Pragmatic Programmer",
            "From Journeyman to Master",
            new List<Author> { new Author("Andrew Hunt", "/authors/OL4A") },
            352,
            new List<Publisher>
            {
                new Publisher("Addison-Wesley"),
                new Publisher("Pearson Education")
            },
            "1999",
            new List<Subject>(),
            "Paperback"
        );

        _openLibraryClientMock
            .Setup(c  => c.GetReadableBookByIsbnAsync(identifierValue, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResponse);

        // Act
        var response = await _client.GetAsync($"/lookup/{entityType}/{identifierType}/{identifierValue}");
        var result = await response.Content.ReadFromJsonAsync<BookResponse>();

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Publishers.Count, Is.EqualTo(2));
        Assert.That(result.Publishers[0].Name, Is.EqualTo("Addison-Wesley"));
        Assert.That(result.Publishers[1].Name, Is.EqualTo("Pearson Education"));
    }

    [Test]
    public async Task GetBookByIdentifier_ShouldHandleBookWithMultipleSubjects()
    {
        // Arrange
        var entityType = "books";
        var identifierType = "isbn";
        var identifierValue = "9780596007126";
        var expectedResponse = new BookResponse(
            "Head First Design Patterns",
            "",
            new List<Author> { new Author("Eric Freeman", "/authors/OL5A") },
            694,
            new List<Publisher> { new Publisher("O'Reilly Media") },
            "2004",
            new List<Subject>
            {
                new Subject("Software Engineering", "/subjects/software_engineering"),
                new Subject("Design Patterns", "/subjects/design_patterns"),
                new Subject("Object-Oriented Programming", "/subjects/oop")
            },
            "Paperback"
        );

        _openLibraryClientMock
            .Setup(c  => c.GetReadableBookByIsbnAsync(identifierValue, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResponse);

        // Act
        var response = await _client.GetAsync($"/lookup/{entityType}/{identifierType}/{identifierValue}");
        var result = await response.Content.ReadFromJsonAsync<BookResponse>();

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Subjects.Count, Is.EqualTo(3));
        Assert.That(result.Subjects[0].Name, Is.EqualTo("Software Engineering"));
        Assert.That(result.Subjects[1].Name, Is.EqualTo("Design Patterns"));
        Assert.That(result.Subjects[2].Name, Is.EqualTo("Object-Oriented Programming"));
    }

    [Test]
    public async Task GetBookByIdentifier_ShouldHandleBookWithNoSubtitle()
    {
        // Arrange
        var entityType = "books";
        var identifierType = "isbn";
        var identifierValue = "9780451524935";
        var expectedResponse = new BookResponse(
            "1984",
            "",
            new List<Author> { new Author("George Orwell", "/authors/OL6A") },
            328,
            new List<Publisher> { new Publisher("Signet Classics") },
            "1961",
            new List<Subject> { new Subject("Fiction", "/subjects/fiction") },
            "Paperback"
        );

        _openLibraryClientMock
            .Setup(c  => c.GetReadableBookByIsbnAsync(identifierValue, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResponse);

        // Act
        var response = await _client.GetAsync($"/lookup/{entityType}/{identifierType}/{identifierValue}");
        var result = await response.Content.ReadFromJsonAsync<BookResponse>();

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Subtitle, Is.Empty);
    }

    [Test]
    public async Task GetBookByIdentifier_ShouldHandleBookWithNoFormat()
    {
        // Arrange
        var entityType = "books";
        var identifierType = "isbn";
        var identifierValue = "9780141439518";
        var expectedResponse = new BookResponse(
            "Pride and Prejudice",
            "",
            new List<Author> { new Author("Jane Austen", "/authors/OL7A") },
            279,
            new List<Publisher> { new Publisher("Penguin Classics") },
            "2002",
            new List<Subject>(),
            null
        );

        _openLibraryClientMock
            .Setup(c  => c.GetReadableBookByIsbnAsync(identifierValue, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResponse);

        // Act
        var response = await _client.GetAsync($"/lookup/{entityType}/{identifierType}/{identifierValue}");
        var result = await response.Content.ReadFromJsonAsync<BookResponse>();

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Format, Is.Null);
    }
}
