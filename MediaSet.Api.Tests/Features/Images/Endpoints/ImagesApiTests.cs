using MediaSet.Api.Features.Images.Services;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using NUnit.Framework;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;

namespace MediaSet.Api.Tests.Features.Images.Endpoints;

[TestFixture]
public class ImagesApiTests : IntegrationTestBase
{
    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;
    private Mock<IImageManagementService> _imageManagementServiceMock = null!;

    [SetUp]
    public void Setup()
    {
        _imageManagementServiceMock = new Mock<IImageManagementService>();

        _factory = CreateWebApplicationFactory()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    var descriptor = services.SingleOrDefault(
                        d => d.ServiceType == typeof(IImageManagementService));
                    if (descriptor != null)
                    {
                        services.Remove(descriptor);
                    }

                    services.AddScoped<IImageManagementService>(_ => _imageManagementServiceMock.Object);
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
    public async Task DeleteOrphanedImages_ShouldReturnOk_WithDeletedCount()
    {
        // Arrange
        _imageManagementServiceMock.Setup(s => s.DeleteOrphanedImagesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(3);

        // Act
        var response = await _client.DeleteAsync("/images/orphaned");
        var result = await response.Content.ReadFromJsonAsync<Dictionary<string, int>>();

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(result, Is.Not.Null);
        Assert.That(result!["deleted"], Is.EqualTo(3));
        _imageManagementServiceMock.Verify(s => s.DeleteOrphanedImagesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task DeleteOrphanedImages_ShouldReturnZero_WhenNoOrphanedFiles()
    {
        // Arrange
        _imageManagementServiceMock.Setup(s => s.DeleteOrphanedImagesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        // Act
        var response = await _client.DeleteAsync("/images/orphaned");
        var result = await response.Content.ReadFromJsonAsync<Dictionary<string, int>>();

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(result!["deleted"], Is.EqualTo(0));
        _imageManagementServiceMock.Verify(s => s.DeleteOrphanedImagesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
