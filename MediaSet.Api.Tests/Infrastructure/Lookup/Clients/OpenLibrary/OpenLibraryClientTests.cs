using MediaSet.Api.Features.Lookup.Models;
using MediaSet.Api.Features.Entities.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Moq;
using Moq.Protected;
using Microsoft.Extensions.Logging;
using MediaSet.Api.Infrastructure.Lookup;

namespace MediaSet.Api.Tests.Infrastructure.Lookup;

[TestFixture]
public class OpenLibraryClientTests
{
    private Mock<ILogger<OpenLibraryClient>> _loggerMock = null!;
    private Mock<HttpMessageHandler> _httpMessageHandlerMock = null!;
    private HttpClient _httpClient = null!;

    [SetUp]
    public void Setup()
    {
        _loggerMock = new Mock<ILogger<OpenLibraryClient>>();
        _httpMessageHandlerMock = new Mock<HttpMessageHandler>();
        _httpClient = new HttpClient(_httpMessageHandlerMock.Object)
        {
            BaseAddress = new Uri("https://openlibrary.org/")
        };
    }

    [Test]
    public async Task GetReadableBookAsync_NormalizesSubjects_IgnoresPunctuationWhitespaceAndCase()
    {
        // Arrange
        var isbn = "9780140328721";
        var responseJson = """
        {
            "items": [],
            "records": {
                "/books/OL7353617M": {
                    "isbns": ["9780140328721"],
                    "lccns": [],
                    "oclcs": [],
                    "olids": ["OL7353617M"],
                    "publish_dates": ["1988"],
                    "record_url": "https://openlibrary.org/books/OL7353617M/Book",
                    "data": {
                        "title": "Test Book",
                        "subtitle": "",
                        "authors": [],
                        "number_of_pages": 100,
                        "publishers": ["Test Publisher"],
                        "publish_date": "1988",
                        "subjects": [
                            {
                                "name": "Prefect, ford (Fictitious character), fiction",
                                "url": "/subjects/prefect,_ford_(fictitious_character),_fiction"
                            },
                            {
                                "name": "Prefect, Ford (Fictitious character) Fiction",
                                "url": "/subjects/prefect,_ford_(fictitious_character)_fiction"
                            }
                        ]
                    }
                }
            }
        }
        """;

        SetupHttpResponse(HttpStatusCode.OK, responseJson);

        var client = new OpenLibraryClient(_httpClient, _loggerMock.Object);

        // Act
        var result = await client.GetReadableBookByIsbnAsync(isbn);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Subjects, Has.Count.EqualTo(1));
        // Title-cased after normalization and commas replaced with semicolons
        Assert.That(result.Subjects[0].Name, Is.EqualTo("Prefect; Ford (Fictitious Character); Fiction"));
    }

    [TearDown]
    public void TearDown()
    {
        _httpClient?.Dispose();
    }

    [Test]
    public async Task GetBookByIsbnAsync_WithValidIsbn_ReturnsBookResponse()
    {
        // Arrange
        var isbn = "9780140328721";
        var responseJson = $$"""
        {
            "ISBN:{{isbn}}": {
                "title": "Fantastic Mr. Fox",
                "subtitle": "A Children's Story",
                "authors": [
                    {
                        "name": "Roald Dahl",
                        "url": "/authors/OL34184A/Roald_Dahl"
                    }
                ],
                "number_of_pages": 96,
                "publishers": [
                    {
                        "name": "Puffin Books"
                    }
                ],
                "publish_date": "1988",
                "subjects": [
                    {
                        "name": "Fiction",
                        "url": "/subjects/fiction"
                    },
                    {
                        "name": "FICTION",
                        "url": "/subjects/fiction"
                    },
                    {
                        "name": "Prefect, Ford (Fictitious character), fiction",
                        "url": "/subjects/prefect,_ford_(fictitious_character),_fiction"
                    }
                ]
            }
        }
        """;

        SetupHttpResponse(HttpStatusCode.OK, responseJson);

        var client = new OpenLibraryClient(_httpClient, _loggerMock.Object);

        // Act
        var result = await client.GetBookByIsbnAsync(isbn);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Title, Is.EqualTo("Fantastic Mr. Fox"));
        Assert.That(result.Subtitle, Is.EqualTo("A Children's Story"));
        Assert.That(result.Authors, Has.Count.EqualTo(1));
        Assert.That(result.Authors[0].Name, Is.EqualTo("Roald Dahl"));
        Assert.That(result.NumberOfPages, Is.EqualTo(96));
        Assert.That(result.Publishers, Has.Count.EqualTo(1));
        Assert.That(result.Publishers[0].Name, Is.EqualTo("Puffin Books"));
        // For direct ISBN lookup, subjects are returned as provided by OpenLibrary (no normalization here)
        Assert.That(result.Subjects, Has.Count.EqualTo(3));
        Assert.That(result.Subjects[0].Name, Is.EqualTo("Fiction"));
        Assert.That(result.Subjects[1].Name, Is.EqualTo("FICTION"));
        Assert.That(result.Subjects[2].Name, Is.EqualTo("Prefect, Ford (Fictitious character), fiction"));
    }

    [Test]
    public async Task GetBookByIsbnAsync_WithNonExistentIsbn_ReturnsNull()
    {
        // Arrange
        var isbn = "0000000000000";
        var responseJson = "{}";

        SetupHttpResponse(HttpStatusCode.OK, responseJson);

        var client = new OpenLibraryClient(_httpClient, _loggerMock.Object);

        // Act
        var result = await client.GetBookByIsbnAsync(isbn);

        // Assert
        Assert.That(result, Is.Null);
    }

    [Test]
    public async Task GetReadableBookByIsbnAsync_WithValidIsbn_ReturnsBookResponse()
    {
        // Arrange
        var isbn = "9780140328721";
        var responseJson = """
        {
            "items": [],
            "records": {
                "/books/OL7353617M": {
                    "isbns": ["9780140328721"],
                    "lccns": [],
                    "oclcs": [],
                    "olids": ["OL7353617M"],
                    "publish_dates": ["1988"],
                    "record_url": "https://openlibrary.org/books/OL7353617M/Fantastic_Mr_Fox",
                    "data": {
                        "title": "Fantastic Mr. Fox",
                        "subtitle": "A Children's Story",
                        "authors": [
                            {
                                "name": "Roald Dahl",
                                "url": "/authors/OL34184A/Roald_Dahl"
                            }
                        ],
                        "number_of_pages": 96,
                        "publishers": ["Puffin Books"],
                        "publish_date": "1988",
                        "subjects": [
                            {
                                "name": "Fiction",
                                "url": "/subjects/fiction"
                            }
                        ]
                    },
                    "details": {
                        "details": {
                            "physical_format": "paperback"
                        }
                    }
                }
            }
        }
        """;

        SetupHttpResponse(HttpStatusCode.OK, responseJson);

        var client = new OpenLibraryClient(_httpClient, _loggerMock.Object);

        // Act
        var result = await client.GetReadableBookByIsbnAsync(isbn);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Title, Is.EqualTo("Fantastic Mr. Fox"));
        Assert.That(result.Subtitle, Is.EqualTo("A Children's Story"));
        Assert.That(result.Authors, Has.Count.EqualTo(1));
        Assert.That(result.Authors[0].Name, Is.EqualTo("Roald Dahl"));
        Assert.That(result.NumberOfPages, Is.EqualTo(96));
        Assert.That(result.Format, Is.EqualTo("Paperback"));
    }

    [Test]
    public async Task GetReadableBookByIsbnAsync_WithHttpRequestException_ReturnsNull()
    {
        // Arrange
        var isbn = "9780140328721";

        _httpMessageHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Network error"));

        var client = new OpenLibraryClient(_httpClient, _loggerMock.Object);

        // Act
        var result = await client.GetReadableBookByIsbnAsync(isbn);

        // Assert
        Assert.That(result, Is.Null);
    }

    [Test]
    public async Task GetReadableBookByLccnAsync_WithValidLccn_ReturnsBookResponse()
    {
        // Arrange
        var lccn = "2004558237";
        var responseJson = """
        {
            "items": [],
            "records": {
                "/books/OL3703609M": {
                    "isbns": [],
                    "lccns": ["2004558237"],
                    "oclcs": [],
                    "olids": ["OL3703609M"],
                    "publish_dates": ["2005"],
                    "record_url": "https://openlibrary.org/books/OL3703609M/Book_Title",
                    "data": {
                        "title": "Sample Book",
                        "subtitle": "",
                        "authors": [
                            {
                                "name": "John Doe",
                                "url": "/authors/OL123456A/John_Doe"
                            }
                        ],
                        "number_of_pages": 250,
                        "publishers": ["Sample Publisher"],
                        "publish_date": "2005",
                        "subjects": []
                    }
                }
            }
        }
        """;

        SetupHttpResponse(HttpStatusCode.OK, responseJson);

        var client = new OpenLibraryClient(_httpClient, _loggerMock.Object);

        // Act
        var result = await client.GetReadableBookByLccnAsync(lccn);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Title, Is.EqualTo("Sample Book"));
    }

    [Test]
    public async Task GetReadableBookByOclcAsync_WithValidOclc_ReturnsBookResponse()
    {
        // Arrange
        var oclc = "123456789";
        var responseJson = """
        {
            "items": [],
            "records": {
                "/books/OL1234567M": {
                    "isbns": [],
                    "lccns": [],
                    "oclcs": ["123456789"],
                    "olids": ["OL1234567M"],
                    "publish_dates": ["2020"],
                    "record_url": "https://openlibrary.org/books/OL1234567M/Book_Title",
                    "data": {
                        "title": "Test Book",
                        "subtitle": "",
                        "authors": [],
                        "number_of_pages": 100,
                        "publishers": ["Test Publisher"],
                        "publish_date": "2020",
                        "subjects": []
                    }
                }
            }
        }
        """;

        SetupHttpResponse(HttpStatusCode.OK, responseJson);

        var client = new OpenLibraryClient(_httpClient, _loggerMock.Object);

        // Act
        var result = await client.GetReadableBookByOclcAsync(oclc);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Title, Is.EqualTo("Test Book"));
    }

    [Test]
    public async Task GetReadableBookByOlidAsync_WithValidOlid_ReturnsBookResponse()
    {
        // Arrange
        var olid = "OL7353617M";
        var responseJson = """
        {
            "items": [],
            "records": {
                "/books/OL7353617M": {
                    "isbns": [],
                    "lccns": [],
                    "oclcs": [],
                    "olids": ["OL7353617M"],
                    "publish_dates": ["2000"],
                    "record_url": "https://openlibrary.org/books/OL7353617M/Book_Title",
                    "data": {
                        "title": "Another Book",
                        "subtitle": "",
                        "authors": [],
                        "number_of_pages": 150,
                        "publishers": ["Another Publisher"],
                        "publish_date": "2000",
                        "subjects": []
                    }
                }
            }
        }
        """;

        SetupHttpResponse(HttpStatusCode.OK, responseJson);

        var client = new OpenLibraryClient(_httpClient, _loggerMock.Object);

        // Act
        var result = await client.GetReadableBookByOlidAsync(olid);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Title, Is.EqualTo("Another Book"));
    }

    [Test]
    public async Task GetReadableBookAsync_WithIsbnIdentifierType_CallsIsbnMethod()
    {
        // Arrange
        var isbn = "9780140328721";
        var responseJson = """
        {
            "items": [],
            "records": {
                "/books/OL7353617M": {
                    "isbns": ["9780140328721"],
                    "lccns": [],
                    "oclcs": [],
                    "olids": ["OL7353617M"],
                    "publish_dates": ["1988"],
                    "record_url": "https://openlibrary.org/books/OL7353617M/Book",
                    "data": {
                        "title": "Test Book",
                        "subtitle": "",
                        "authors": [],
                        "number_of_pages": 100,
                        "publishers": ["Test Publisher"],
                        "publish_date": "1988",
                        "subjects": []
                    }
                }
            }
        }
        """;

        SetupHttpResponse(HttpStatusCode.OK, responseJson);

        var client = new OpenLibraryClient(_httpClient, _loggerMock.Object);

        // Act
        var result = await client.GetReadableBookAsync(IdentifierType.Isbn, isbn);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Title, Is.EqualTo("Test Book"));
    }

    [Test]
    public async Task GetReadableBookAsync_WithLccnIdentifierType_CallsLccnMethod()
    {
        // Arrange
        var lccn = "2004558237";
        var responseJson = """
        {
            "items": [],
            "records": {
                "/books/OL3703609M": {
                    "isbns": [],
                    "lccns": ["2004558237"],
                    "oclcs": [],
                    "olids": ["OL3703609M"],
                    "publish_dates": ["2005"],
                    "record_url": "https://openlibrary.org/books/OL3703609M/Book",
                    "data": {
                        "title": "LCCN Test Book",
                        "subtitle": "",
                        "authors": [],
                        "number_of_pages": 100,
                        "publishers": ["Test Publisher"],
                        "publish_date": "2005",
                        "subjects": []
                    }
                }
            }
        }
        """;

        SetupHttpResponse(HttpStatusCode.OK, responseJson);

        var client = new OpenLibraryClient(_httpClient, _loggerMock.Object);

        // Act
        var result = await client.GetReadableBookAsync(IdentifierType.Lccn, lccn);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Title, Is.EqualTo("LCCN Test Book"));
    }

    [Test]
    public async Task GetReadableBookAsync_WithOclcIdentifierType_CallsOclcMethod()
    {
        // Arrange
        var oclc = "123456789";
        var responseJson = """
        {
            "items": [],
            "records": {
                "/books/OL1234567M": {
                    "isbns": [],
                    "lccns": [],
                    "oclcs": ["123456789"],
                    "olids": ["OL1234567M"],
                    "publish_dates": ["2020"],
                    "record_url": "https://openlibrary.org/books/OL1234567M/Book",
                    "data": {
                        "title": "OCLC Test Book",
                        "subtitle": "",
                        "authors": [],
                        "number_of_pages": 100,
                        "publishers": ["Test Publisher"],
                        "publish_date": "2020",
                        "subjects": []
                    }
                }
            }
        }
        """;

        SetupHttpResponse(HttpStatusCode.OK, responseJson);

        var client = new OpenLibraryClient(_httpClient, _loggerMock.Object);

        // Act
        var result = await client.GetReadableBookAsync(IdentifierType.Oclc, oclc);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Title, Is.EqualTo("OCLC Test Book"));
    }

    [Test]
    public async Task GetReadableBookAsync_WithOlidIdentifierType_CallsOlidMethod()
    {
        // Arrange
        var olid = "OL7353617M";
        var responseJson = """
        {
            "items": [],
            "records": {
                "/books/OL7353617M": {
                    "isbns": [],
                    "lccns": [],
                    "oclcs": [],
                    "olids": ["OL7353617M"],
                    "publish_dates": ["2000"],
                    "record_url": "https://openlibrary.org/books/OL7353617M/Book",
                    "data": {
                        "title": "OLID Test Book",
                        "subtitle": "",
                        "authors": [],
                        "number_of_pages": 100,
                        "publishers": ["Test Publisher"],
                        "publish_date": "2000",
                        "subjects": []
                    }
                }
            }
        }
        """;

        SetupHttpResponse(HttpStatusCode.OK, responseJson);

        var client = new OpenLibraryClient(_httpClient, _loggerMock.Object);

        // Act
        var result = await client.GetReadableBookAsync(IdentifierType.Olid, olid);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Title, Is.EqualTo("OLID Test Book"));
    }

    [Test]
    public void GetReadableBookAsync_WithInvalidIdentifierType_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var client = new OpenLibraryClient(_httpClient, _loggerMock.Object);

        // Act & Assert
        Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () =>
            await client.GetReadableBookAsync((IdentifierType)999, "value"));
    }

    [Test]
    public async Task GetReadableBookAsync_WithEmptyRecords_ReturnsNull()
    {
        // Arrange
        var isbn = "9780140328721";
        var responseJson = """
        {
            "items": [],
            "records": {}
        }
        """;

        SetupHttpResponse(HttpStatusCode.OK, responseJson);

        var client = new OpenLibraryClient(_httpClient, _loggerMock.Object);

        // Act
        var result = await client.GetReadableBookByIsbnAsync(isbn);

        // Assert
        Assert.That(result, Is.Null);
    }

    [Test]
    public async Task GetReadableBookAsync_WithNullData_ReturnsNull()
    {
        // Arrange
        var isbn = "9780140328721";
        var responseJson = """
        {
            "items": [],
            "records": {
                "/books/OL7353617M": {
                    "isbns": ["9780140328721"],
                    "lccns": [],
                    "oclcs": [],
                    "olids": ["OL7353617M"],
                    "publish_dates": ["1988"],
                    "record_url": "https://openlibrary.org/books/OL7353617M/Book",
                    "data": null
                }
            }
        }
        """;

        SetupHttpResponse(HttpStatusCode.OK, responseJson);

        var client = new OpenLibraryClient(_httpClient, _loggerMock.Object);

        // Act
        var result = await client.GetReadableBookByIsbnAsync(isbn);

        // Assert
        Assert.That(result, Is.Null);
    }

    [Test]
    public async Task GetReadableBookAsync_WithDuplicateSubjects_RemovesDuplicates()
    {
        // Arrange
        var isbn = "9780140328721";
        var responseJson = """
        {
            "items": [],
            "records": {
                "/books/OL7353617M": {
                    "isbns": ["9780140328721"],
                    "lccns": [],
                    "oclcs": [],
                    "olids": ["OL7353617M"],
                    "publish_dates": ["1988"],
                    "record_url": "https://openlibrary.org/books/OL7353617M/Book",
                    "data": {
                        "title": "Test Book",
                        "subtitle": "",
                        "authors": [],
                        "number_of_pages": 100,
                        "publishers": ["Test Publisher"],
                        "publish_date": "1988",
                        "subjects": [
                            {
                                "name": "Fiction",
                                "url": "/subjects/fiction"
                            },
                            {
                                "name": "fiction",
                                "url": "/subjects/fiction"
                            },
                            {
                                "name": "FICTION",
                                "url": "/subjects/fiction"
                            }
                        ]
                    }
                }
            }
        }
        """;

        SetupHttpResponse(HttpStatusCode.OK, responseJson);

        var client = new OpenLibraryClient(_httpClient, _loggerMock.Object);

        // Act
        var result = await client.GetReadableBookByIsbnAsync(isbn);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Subjects, Has.Count.EqualTo(1));
        Assert.That(result.Subjects[0].Name, Is.EqualTo("Fiction"));
    }

    [Test]
    public async Task GetReadableBookAsync_WithPublishDateFromData_UsesDataPublishDate()
    {
        // Arrange
        var isbn = "9780140328721";
        var responseJson = """
        {
            "items": [],
            "records": {
                "/books/OL7353617M": {
                    "isbns": ["9780140328721"],
                    "lccns": [],
                    "oclcs": [],
                    "olids": ["OL7353617M"],
                    "publish_dates": [],
                    "record_url": "https://openlibrary.org/books/OL7353617M/Book",
                    "data": {
                        "title": "Test Book",
                        "subtitle": "",
                        "authors": [],
                        "number_of_pages": 100,
                        "publishers": ["Test Publisher"],
                        "publish_date": "2010",
                        "subjects": []
                    }
                }
            }
        }
        """;

        SetupHttpResponse(HttpStatusCode.OK, responseJson);

        var client = new OpenLibraryClient(_httpClient, _loggerMock.Object);

        // Act
        var result = await client.GetReadableBookByIsbnAsync(isbn);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.PublishDate, Is.EqualTo("2010"));
    }

    [Test]
    public async Task GetReadableBookAsync_PrefersRecordPublishDatesOverDataPublishDate()
    {
        // Arrange
        var isbn = "9780140328721";
        var responseJson = """
        {
            "items": [],
            "records": {
                "/books/OL7353617M": {
                    "isbns": ["9780140328721"],
                    "lccns": [],
                    "oclcs": [],
                    "olids": ["OL7353617M"],
                    "publish_dates": ["2015"],
                    "record_url": "https://openlibrary.org/books/OL7353617M/Book",
                    "data": {
                        "title": "Test Book",
                        "subtitle": "",
                        "authors": [],
                        "number_of_pages": 100,
                        "publishers": ["Test Publisher"],
                        "publish_date": "2010",
                        "subjects": []
                    }
                }
            }
        }
        """;

        SetupHttpResponse(HttpStatusCode.OK, responseJson);

        var client = new OpenLibraryClient(_httpClient, _loggerMock.Object);

        // Act
        var result = await client.GetReadableBookByIsbnAsync(isbn);

        // Assert
        Assert.That(result, Is.Not.Null);
        // Record publish_dates takes priority over data publish_date
        Assert.That(result!.PublishDate, Is.EqualTo("2015"));
    }

    private void SetupHttpResponse(HttpStatusCode statusCode, string content)
    {
        var response = new HttpResponseMessage
        {
            StatusCode = statusCode,
            Content = new StringContent(content, System.Text.Encoding.UTF8, "application/json")
        };

        _httpMessageHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(response);
    }
}
