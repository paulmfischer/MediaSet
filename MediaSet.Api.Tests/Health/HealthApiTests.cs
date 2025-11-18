using NUnit.Framework;
using Moq;
using MediaSet.Api.Services;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Hosting;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Text.Json;
using MongoDB.Driver;
using MongoDB.Bson;
using System.Threading;

namespace MediaSet.Api.Tests.Health;

[TestFixture]
public class HealthApiTests : IntegrationTestBase
{
    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;
    private Mock<IDatabaseService> _databaseServiceMock = null!;

    [SetUp]
    public void Setup()
    {
        _databaseServiceMock = new Mock<IDatabaseService>();
        
        // Setup a mock MongoDB database for the ping command
        var mockDatabase = new Mock<IMongoDatabase>();
        var mockCollection = new Mock<IMongoCollection<BsonDocument>>();
        
        mockCollection.Setup(c => c.Database).Returns(mockDatabase.Object);
        mockDatabase.Setup(d => d.RunCommandAsync<BsonDocument>(
            It.IsAny<Command<BsonDocument>>(),
            It.IsAny<ReadPreference>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BsonDocument { { "ok", 1 } });
            
        _databaseServiceMock.Setup(s => s.GetCollection<BsonDocument>())
            .Returns(mockCollection.Object);

        _factory = CreateWebApplicationFactory()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    // Remove existing DatabaseService registration
                    var databaseServiceDescriptor = services.SingleOrDefault(
                        d => d.ServiceType == typeof(IDatabaseService));
                    if (databaseServiceDescriptor != null)
                        services.Remove(databaseServiceDescriptor);

                    // Add mock service
                    services.AddSingleton<IDatabaseService>(_ => _databaseServiceMock.Object);
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
    public async Task GetHealth_ShouldReturnOk()
    {
        // Act
        var response = await _client.GetAsync("/health");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    [Test]
    public async Task GetHealth_ShouldIncludeVersionInformation()
    {
        // Act
        var response = await _client.GetAsync("/health");
        var content = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(content);
        var root = json.RootElement;

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        
        // Check that version field exists
        Assert.That(root.TryGetProperty("version", out var versionElement), Is.True, "Response should contain 'version' field");
        var version = versionElement.GetString();
        Assert.That(version, Is.Not.Null);
        Assert.That(version, Is.Not.Empty);
        
        // Check that commit field exists
        Assert.That(root.TryGetProperty("commit", out var commitElement), Is.True, "Response should contain 'commit' field");
        var commit = commitElement.GetString();
        Assert.That(commit, Is.Not.Null);
        Assert.That(commit, Is.Not.Empty);
        
        // Check that buildTime field exists
        Assert.That(root.TryGetProperty("buildTime", out var buildTimeElement), Is.True, "Response should contain 'buildTime' field");
        var buildTime = buildTimeElement.GetString();
        Assert.That(buildTime, Is.Not.Null);
        Assert.That(buildTime, Is.Not.Empty);
    }

    [Test]
    public async Task GetHealth_ShouldIncludeStatusField()
    {
        // Act
        var response = await _client.GetAsync("/health");
        var content = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(content);
        var root = json.RootElement;

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(root.TryGetProperty("status", out var statusElement), Is.True, "Response should contain 'status' field");
        var status = statusElement.GetString();
        Assert.That(status, Is.Not.Null);
        Assert.That(status, Is.Not.Empty);
    }

    [Test]
    public async Task GetHealth_ShouldIncludeTimestampField()
    {
        // Act
        var response = await _client.GetAsync("/health");
        var content = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(content);
        var root = json.RootElement;

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(root.TryGetProperty("timestamp", out var timestampElement), Is.True, "Response should contain 'timestamp' field");
    }

    [Test]
    public async Task GetHealth_ShouldIncludeDatabaseInformation()
    {
        // Act
        var response = await _client.GetAsync("/health");
        var content = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(content);
        var root = json.RootElement;

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(root.TryGetProperty("database", out var databaseElement), Is.True, "Response should contain 'database' field");
        
        // Check database status field
        Assert.That(databaseElement.TryGetProperty("status", out var statusElement), Is.True, "Database should contain 'status' field");
        var status = statusElement.GetString();
        Assert.That(status, Is.Not.Null);
        Assert.That(status, Is.Not.Empty);
    }
}
