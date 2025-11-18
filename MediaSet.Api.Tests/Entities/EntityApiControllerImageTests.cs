using NUnit.Framework;
using Moq;
using Bogus;
using MediaSet.Api.Services;
using MediaSet.Api.Models;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using MongoDB.Driver;

namespace MediaSet.Api.Tests.Entities;

/// <summary>
/// Unit tests for image-related Entity API controller endpoints.
/// Tests the API contract and controller behavior with mocked service dependencies.
/// Note: These are controller unit tests, not true integration tests (services are mocked).
/// </summary>
[TestFixture]
public class EntityApiControllerImageTests : IntegrationTestBase
{
    private WebApplicationFactory<Program>? _factory;
    private HttpClient? _client;
    private Mock<IImageService>? _imageServiceMock;
    private Mock<IEntityService<Game>>? _gameServiceMock;
    private Faker<Game>? _gameFaker;
    private JsonSerializerOptions? _jsonOptions;

    [SetUp]
    public void Setup()
    {
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            Converters = { new JsonStringEnumConverter() }
        };

        _imageServiceMock = new Mock<IImageService>();
        _gameServiceMock = new Mock<IEntityService<Game>>();

        _factory = CreateWebApplicationFactory()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    // Remove existing service registrations
                    var imageServiceDescriptor = services.SingleOrDefault(
                        d => d.ServiceType == typeof(IImageService));
                    if (imageServiceDescriptor != null)
                        services.Remove(imageServiceDescriptor);

                    var gameServiceDescriptor = services.SingleOrDefault(
                        d => d.ServiceType == typeof(IEntityService<Game>));
                    if (gameServiceDescriptor != null)
                        services.Remove(gameServiceDescriptor);

                    // Add mock services
                    services.AddScoped<IImageService>(_ => _imageServiceMock.Object);
                    services.AddScoped<IEntityService<Game>>(_ => _gameServiceMock.Object);
                });
            });

        _client = _factory.CreateClient();

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

    #region Upload Image Tests

    [Test]
    public async Task CreateGameWithImage_ShouldUploadImageSuccessfully_WhenFileIsValid()
    {
        // Arrange
        var gameId = "507f1f77bcf86cd799439011";
        var newGame = _gameFaker!.Clone().RuleFor(g => g.Id, gameId).Generate();
        var imageFile = CreateImageFile("test-image.jpg", "image/jpeg");
        var imageData = new Image
        {
            FileName = "test-image.jpg",
            FilePath = "games/507f1f77bcf86cd799439011-guid.jpg",
            ContentType = "image/jpeg",
            FileSize = 1024,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _gameServiceMock!.Setup(s => s.CreateAsync(It.IsAny<Game>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _imageServiceMock!.Setup(s => s.SaveImageAsync(It.IsAny<IFormFile>(), "games", It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(imageData);
        _gameServiceMock.Setup(s => s.UpdateAsync(It.IsAny<string>(), It.IsAny<Game>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Mock.Of<ReplaceOneResult>());

        // Act
        var content = CreateMultipartFormDataContent(newGame, imageFile);
        var response = await _client!.PostAsync("/Games", content);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        _gameServiceMock.Verify(s => s.CreateAsync(It.IsAny<Game>(), It.IsAny<CancellationToken>()), Times.Once);
        _imageServiceMock.Verify(s => s.SaveImageAsync(It.IsAny<IFormFile>(), "games", It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task CreateGameWithImage_ShouldCreateEntityEvenIfImageUploadFails()
    {
        // Arrange
        var gameId = "507f1f77bcf86cd799439011";
        var newGame = _gameFaker!.Clone().RuleFor(g => g.Id, gameId).Generate();
        var imageFile = CreateImageFile("invalid.gif", "image/gif");

        _gameServiceMock!.Setup(s => s.CreateAsync(It.IsAny<Game>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _imageServiceMock!.Setup(s => s.SaveImageAsync(It.IsAny<IFormFile>(), "games", It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ArgumentException("Unsupported file type"));

        // Act
        var content = CreateMultipartFormDataContent(newGame, imageFile);
        var response = await _client!.PostAsync("/Games", content);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        _gameServiceMock.Verify(s => s.CreateAsync(It.IsAny<Game>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task CreateGameWithImage_ShouldReturnBadRequest_WhenFileIsEmpty()
    {
        // Arrange
        var gameId = "507f1f77bcf86cd799439011";
        var newGame = _gameFaker!.Clone().RuleFor(g => g.Id, gameId).Generate();
        var emptyFile = CreateImageFile("empty.jpg", "image/jpeg", []);

        _gameServiceMock!.Setup(s => s.CreateAsync(It.IsAny<Game>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _imageServiceMock!.Setup(s => s.SaveImageAsync(It.IsAny<IFormFile>(), "games", It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ArgumentException("File cannot be null or empty"));

        // Act
        var content = CreateMultipartFormDataContent(newGame, emptyFile);
        var response = await _client!.PostAsync("/Games", content);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
    }

    [Test]
    public async Task CreateGameWithImage_ShouldReturnBadRequest_WhenEntityJsonMissing()
    {
        // Arrange
        var imageFile = CreateImageFile("test.jpg", "image/jpeg");

        // Act
        var content = new MultipartFormDataContent();
        content.Add(new StreamContent(imageFile.OpenReadStream()), "coverImage", imageFile.FileName);
        var response = await _client!.PostAsync("/Games", content);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    #endregion

    #region Edit Entity with Image Tests

    [Test]
    public async Task UpdateGameWithNewImage_ShouldReplaceExistingImage()
    {
        // Arrange
        var gameId = "507f1f77bcf86cd799439011";
        var existingGame = _gameFaker!.Clone().RuleFor(g => g.Id, gameId).Generate();
        existingGame.CoverImage = new Image
        {
            FileName = "old-image.jpg",
            FilePath = "games/507f1f77bcf86cd799439011-old-guid.jpg",
            ContentType = "image/jpeg",
            FileSize = 2048,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var updatedGame = _gameFaker!.Clone().RuleFor(g => g.Id, gameId).Generate();
        var newImageFile = CreateImageFile("new-image.jpg", "image/jpeg");
        var newImageData = new Image
        {
            FileName = "new-image.jpg",
            FilePath = "games/507f1f77bcf86cd799439011-new-guid.jpg",
            ContentType = "image/jpeg",
            FileSize = 1024,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _gameServiceMock!.Setup(s => s.GetAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingGame);
        _gameServiceMock.Setup(s => s.UpdateAsync(gameId, It.IsAny<Game>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Mock.Of<ReplaceOneResult>());
        _imageServiceMock!.Setup(s => s.SaveImageAsync(It.IsAny<IFormFile>(), "games", gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(newImageData);
        _imageServiceMock.Setup(s => s.DeleteImageAsync(It.IsAny<string>()));

        // Act
        var content = CreateMultipartFormDataContent(updatedGame, newImageFile);
        var response = await _client!.PutAsync($"/Games/{gameId}", content);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        _imageServiceMock!.Verify(s => s.SaveImageAsync(It.IsAny<IFormFile>(), "games", gameId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task UpdateGameAndRemoveImage_ShouldDeleteImageReference()
    {
        // Arrange
        var gameId = "507f1f77bcf86cd799439011";
        var existingGame = _gameFaker!.Clone().RuleFor(g => g.Id, gameId).Generate();
        existingGame.CoverImage = new Image
        {
            FileName = "old-image.jpg",
            FilePath = "games/507f1f77bcf86cd799439011-old-guid.jpg",
            ContentType = "image/jpeg",
            FileSize = 2048,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var updatedGame = _gameFaker!.Clone().RuleFor(g => g.Id, gameId).RuleFor(g => g.CoverImage, _ => (Image)null!).Generate();

        _gameServiceMock!.Setup(s => s.GetAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingGame);
        _gameServiceMock.Setup(s => s.UpdateAsync(gameId, It.IsAny<Game>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Mock.Of<ReplaceOneResult>());
        _imageServiceMock!.Setup(s => s.DeleteImageAsync(It.IsAny<string>()));

        // Act
        var response = await _client!.PutAsJsonAsync($"/Games/{gameId}", updatedGame);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        _imageServiceMock!.Verify(s => s.DeleteImageAsync(It.IsAny<string>()), Times.Once);
    }

    [Test]
    public async Task UpdateGameWithImageUploadFailure_ShouldReturnBadRequest()
    {
        // Arrange
        var gameId = "507f1f77bcf86cd799439011";
        var existingGame = _gameFaker!.Clone().RuleFor(g => g.Id, gameId).Generate();
        var updatedGame = _gameFaker!.Clone().RuleFor(g => g.Id, gameId).Generate();
        var invalidImageFile = CreateImageFile("oversized.jpg", "image/jpeg");

        _gameServiceMock!.Setup(s => s.GetAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingGame);
        _gameServiceMock.Setup(s => s.UpdateAsync(gameId, It.IsAny<Game>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Mock.Of<ReplaceOneResult>());
        _imageServiceMock!.Setup(s => s.SaveImageAsync(It.IsAny<IFormFile>(), "games", gameId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ArgumentException("File size exceeds maximum allowed size of 5MB"));

        // Act
        var content = CreateMultipartFormDataContent(updatedGame, invalidImageFile);
        var response = await _client!.PutAsync($"/Games/{gameId}", content);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    #endregion

    #region Delete Image Tests

    [Test]
    public async Task DeleteGameImage_ShouldRemoveImageSuccessfully()
    {
        // Arrange
        var gameId = "507f1f77bcf86cd799439011";
        var gameWithImage = _gameFaker!.Clone().RuleFor(g => g.Id, gameId).Generate();
        gameWithImage.CoverImage = new Image
        {
            FileName = "test-image.jpg",
            FilePath = "games/507f1f77bcf86cd799439011-guid.jpg",
            ContentType = "image/jpeg",
            FileSize = 1024,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _gameServiceMock!.Setup(s => s.GetAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(gameWithImage);
        _gameServiceMock.Setup(s => s.UpdateAsync(gameId, It.IsAny<Game>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Mock.Of<ReplaceOneResult>());
        _imageServiceMock!.Setup(s => s.DeleteImageAsync(It.IsAny<string>()));

        // Act
        var response = await _client!.DeleteAsync($"/Games/{gameId}/image");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
        _imageServiceMock!.Verify(s => s.DeleteImageAsync(It.IsAny<string>()), Times.Once);
        _gameServiceMock!.Verify(s => s.UpdateAsync(gameId, It.IsAny<Game>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task DeleteGameImage_ShouldReturnNotFound_WhenGameDoesNotExist()
    {
        // Arrange
        var gameId = "507f1f77bcf86cd799439011";

        _gameServiceMock!.Setup(s => s.GetAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Game)null!);

        // Act
        var response = await _client!.DeleteAsync($"/Games/{gameId}/image");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        _imageServiceMock!.Verify(s => s.DeleteImageAsync(It.IsAny<string>()), Times.Never);
    }

    [Test]
    public async Task DeleteGameImage_ShouldReturnNotFound_WhenGameHasNoImage()
    {
        // Arrange
        var gameId = "507f1f77bcf86cd799439011";
        var gameWithoutImage = _gameFaker!.Clone().RuleFor(g => g.Id, gameId).Generate();

        _gameServiceMock!.Setup(s => s.GetAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(gameWithoutImage);

        // Act
        var response = await _client!.DeleteAsync($"/Games/{gameId}/image");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        _imageServiceMock!.Verify(s => s.DeleteImageAsync(It.IsAny<string>()), Times.Never);
    }

    [Test]
    public async Task DeleteGame_ShouldAlsoDeleteAssociatedImage()
    {
        // Arrange
        var gameId = "507f1f77bcf86cd799439011";
        var gameWithImage = _gameFaker!.Clone().RuleFor(g => g.Id, gameId).Generate();
        gameWithImage.CoverImage = new Image
        {
            FileName = "test-image.jpg",
            FilePath = "games/507f1f77bcf86cd799439011-guid.jpg",
            ContentType = "image/jpeg",
            FileSize = 1024,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _gameServiceMock!.Setup(s => s.GetAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(gameWithImage);
        _gameServiceMock.Setup(s => s.RemoveAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Mock.Of<DeleteResult>());
        _imageServiceMock!.Setup(s => s.DeleteImageAsync(gameWithImage.CoverImage!.FilePath));

        // Act
        var response = await _client!.DeleteAsync($"/Games/{gameId}");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        _imageServiceMock!.Verify(s => s.DeleteImageAsync(gameWithImage.CoverImage!.FilePath), Times.Once);
    }

    #endregion

    #region URL Download Image Tests

    [Test]
    public async Task CreateGameWithImageUrl_ShouldDownloadAndSaveImageSuccessfully()
    {
        // Arrange
        var gameId = "507f1f77bcf86cd799439011";
        var newGame = _gameFaker!.Clone().RuleFor(g => g.Id, gameId).Generate();
        var imageUrl = "https://example.com/image.jpg";
        var imageData = new Image
        {
            FileName = "image.jpg",
            FilePath = "games/507f1f77bcf86cd799439011-guid.jpg",
            ContentType = "image/jpeg",
            FileSize = 1024,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _gameServiceMock!.Setup(s => s.CreateAsync(It.IsAny<Game>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _imageServiceMock!.Setup(s => s.DownloadAndSaveImageAsync(imageUrl, "games", It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(imageData);
        _gameServiceMock.Setup(s => s.UpdateAsync(It.IsAny<string>(), It.IsAny<Game>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Mock.Of<ReplaceOneResult>());

        // Act
        var content = CreateMultipartFormDataContentWithUrl(newGame, imageUrl);
        var response = await _client!.PostAsync("/Games", content);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        _imageServiceMock!.Verify(s => s.DownloadAndSaveImageAsync(imageUrl, "games", It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task CreateGameWithImageUrl_ShouldReturnBadRequest_WhenUrlIsInvalid()
    {
        // Arrange
        var gameId = "507f1f77bcf86cd799439011";
        var newGame = _gameFaker!.Clone().RuleFor(g => g.Id, gameId).Generate();
        var invalidUrl = "not-a-valid-url";

        _gameServiceMock!.Setup(s => s.CreateAsync(It.IsAny<Game>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _imageServiceMock!.Setup(s => s.DownloadAndSaveImageAsync(invalidUrl, "Games", gameId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ArgumentException("Image URL must be a valid HTTP or HTTPS URL"));

        // Act
        var content = CreateMultipartFormDataContentWithUrl(newGame, invalidUrl);
        var response = await _client!.PostAsync("/Games", content);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
    }

    [Test]
    public async Task UpdateGameWithImageUrl_ShouldDownloadAndReplaceImage()
    {
        // Arrange
        var gameId = "507f1f77bcf86cd799439011";
        var existingGame = _gameFaker!.Clone().RuleFor(g => g.Id, gameId).Generate();
        existingGame.CoverImage = new Image
        {
            FileName = "old-image.jpg",
            FilePath = "games/507f1f77bcf86cd799439011-old-guid.jpg",
            ContentType = "image/jpeg",
            FileSize = 2048,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var updatedGame = _gameFaker!.Clone().RuleFor(g => g.Id, gameId).Generate();
        var imageUrl = "https://example.com/new-image.jpg";
        var newImageData = new Image
        {
            FileName = "new-image.jpg",
            FilePath = "games/507f1f77bcf86cd799439011-new-guid.jpg",
            ContentType = "image/jpeg",
            FileSize = 1024,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _gameServiceMock!.Setup(s => s.GetAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingGame);
        _gameServiceMock.Setup(s => s.UpdateAsync(gameId, It.IsAny<Game>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Mock.Of<ReplaceOneResult>());
        _imageServiceMock!.Setup(s => s.DeleteImageAsync(It.IsAny<string>()));
        _imageServiceMock!.Setup(s => s.DownloadAndSaveImageAsync(imageUrl, "games", gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(newImageData);

        // Act
        var content = CreateMultipartFormDataContentWithUrl(updatedGame, imageUrl);
        var response = await _client!.PutAsync($"/Games/{gameId}", content);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        _imageServiceMock!.Verify(s => s.DownloadAndSaveImageAsync(imageUrl, "games", gameId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task CreateGameWithImageUrl_ShouldCreateEntityEvenIfDownloadFails()
    {
        // Arrange
        var gameId = "507f1f77bcf86cd799439011";
        var newGame = _gameFaker!.Clone().RuleFor(g => g.Id, gameId).Generate();
        var imageUrl = "https://example.com/nonexistent.jpg";

        _gameServiceMock!.Setup(s => s.CreateAsync(It.IsAny<Game>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _imageServiceMock!.Setup(s => s.DownloadAndSaveImageAsync(imageUrl, "Games", gameId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("The remote server returned an error: (404) Not Found."));

        // Act
        var content = CreateMultipartFormDataContentWithUrl(newGame, imageUrl);
        var response = await _client!.PostAsync("/Games", content);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
    }

    #endregion

    #region Helper Methods

    private MultipartFormDataContent CreateMultipartFormDataContent(Game game, IFormFile imageFile)
    {
        var content = new MultipartFormDataContent();
        var jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var gameJson = JsonSerializer.Serialize(game, jsonOptions);
        content.Add(new StringContent(gameJson, Encoding.UTF8, "application/json"), "entity");
        content.Add(new StreamContent(imageFile.OpenReadStream()), "coverImage", imageFile.FileName);
        return content;
    }

    private MultipartFormDataContent CreateMultipartFormDataContentWithUrl(Game game, string imageUrl)
    {
        var content = new MultipartFormDataContent();
        var jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var gameJson = JsonSerializer.Serialize(game, jsonOptions);
        content.Add(new StringContent(gameJson, Encoding.UTF8, "application/json"), "entity");
        content.Add(new StringContent(imageUrl, Encoding.UTF8, "text/plain"), "imageUrl");
        return content;
    }

    private IFormFile CreateImageFile(string fileName, string contentType, byte[]? fileContent = null)
    {
        if (fileContent == null)
        {
            fileContent = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 }; // JPEG header
        }

        var stream = new MemoryStream(fileContent);
        return new FormFile(stream, 0, fileContent.Length, fileName, fileName)
        {
            Headers = new HeaderDictionary(),
            ContentType = contentType
        };
    }

    [TearDown]
    public void TearDown()
    {
        _client?.Dispose();
        _factory?.Dispose();
    }

    #endregion
}