using NUnit.Framework;
using Moq;
using MediaSet.Api.Services;
using MediaSet.Api.Models;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Hosting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using System.Threading;

namespace MediaSet.Api.Tests.Metadata;

[TestFixture]
public class MetadataApiNewTests : IntegrationTestBase
{
    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;
    private Mock<IMetadataService> _metadataServiceMock = null!;

    [SetUp]
    public void Setup()
    {
        _metadataServiceMock = new Mock<IMetadataService>();

        _factory = CreateWebApplicationFactory()
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
    public async Task GetMetadata_WithFormats_ForBooks_ShouldReturnFormats()
    {
        // Arrange
        var expectedFormats = new List<string> { "Hardcover", "Paperback", "eBook" };
        _metadataServiceMock.Setup(s => s.GetMetadata(MediaTypes.Books, "formats", It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedFormats);

        // Act
        var response = await _client.GetAsync("/metadata/books/formats");
        var result = await response.Content.ReadFromJsonAsync<List<string>>();

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Count, Is.EqualTo(3));
        _metadataServiceMock.Verify(s => s.GetMetadata(MediaTypes.Books, "formats", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task GetMetadata_WithGenres_ForMovies_ShouldReturnGenres()
    {
        // Arrange
        var expectedGenres = new List<string> { "Action", "Comedy", "Drama" };
        _metadataServiceMock.Setup(s => s.GetMetadata(MediaTypes.Movies, "genres", It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedGenres);

        // Act
        var response = await _client.GetAsync("/metadata/movies/genres");
        var result = await response.Content.ReadFromJsonAsync<List<string>>();

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Count, Is.EqualTo(3));
        _metadataServiceMock.Verify(s => s.GetMetadata(MediaTypes.Movies, "genres", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task GetMetadata_ForAuthors_ShouldReturnAuthors()
    {
        // Arrange
        var expectedAuthors = new List<string> { "J.K. Rowling", "Stephen King" };
        _metadataServiceMock.Setup(s => s.GetMetadata(MediaTypes.Books, "Authors", It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedAuthors);

        // Act
        var response = await _client.GetAsync("/metadata/books/Authors");
        var result = await response.Content.ReadFromJsonAsync<List<string>>();

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Count, Is.EqualTo(2));
        _metadataServiceMock.Verify(s => s.GetMetadata(MediaTypes.Books, "Authors", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task GetMetadata_ForStudios_ShouldReturnStudios()
    {
        // Arrange
        var expectedStudios = new List<string> { "Warner Bros", "Universal" };
        _metadataServiceMock.Setup(s => s.GetMetadata(MediaTypes.Movies, "Studios", It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedStudios);

        // Act
        var response = await _client.GetAsync("/metadata/movies/Studios");
        var result = await response.Content.ReadFromJsonAsync<List<string>>();

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Count, Is.EqualTo(2));
        _metadataServiceMock.Verify(s => s.GetMetadata(MediaTypes.Movies, "Studios", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task GetMetadata_ForArtists_ShouldReturnArtists()
    {
        // Arrange
        var expectedArtists = new List<string> { "The Beatles", "Queen" };
        _metadataServiceMock.Setup(s => s.GetMetadata(MediaTypes.Musics, "Artist", It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedArtists);

        // Act
        var response = await _client.GetAsync("/metadata/musics/Artist");
        var result = await response.Content.ReadFromJsonAsync<List<string>>();

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Count, Is.EqualTo(2));
        _metadataServiceMock.Verify(s => s.GetMetadata(MediaTypes.Musics, "Artist", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task GetMetadata_WithFormats_ShouldBeCaseInsensitive()
    {
        // Arrange
        var expectedFormats = new List<string> { "CD", "Vinyl" };
        _metadataServiceMock.Setup(s => s.GetMetadata(MediaTypes.Musics, "formats", It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedFormats);

        var mediaTypes = new[] { "Musics", "musics", "MUSICS", "mUsIcS" };

        foreach (var mediaType in mediaTypes)
        {
            // Act
            var response = await _client.GetAsync($"/metadata/{mediaType}/formats");

            // Assert
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK), 
                $"Failed for media type: {mediaType}");
        }
    }

    [Test]
    public async Task GetMetadata_WithGenres_ShouldBeCaseInsensitive()
    {
        // Arrange
        var expectedGenres = new List<string> { "RPG", "Action" };
        _metadataServiceMock.Setup(s => s.GetMetadata(MediaTypes.Games, "genres", It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedGenres);

        var mediaTypes = new[] { "Games", "games", "GAMES", "gAmEs" };

        foreach (var mediaType in mediaTypes)
        {
            // Act
            var response = await _client.GetAsync($"/metadata/{mediaType}/genres");

            // Assert
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK), 
                $"Failed for media type: {mediaType}");
        }
    }

    [Test]
    public async Task GetMetadata_WithFormats_ShouldReturnEmptyList_WhenNoFormatsExist()
    {
        // Arrange
        _metadataServiceMock.Setup(s => s.GetMetadata(MediaTypes.Books, "formats", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<string>());

        // Act
        var response = await _client.GetAsync("/metadata/books/formats");
        var result = await response.Content.ReadFromJsonAsync<List<string>>();

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Count, Is.EqualTo(0));
    }
}
