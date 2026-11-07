using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MediaSet.Api.Infrastructure.Database;
using MediaSet.Api.Infrastructure.DataAccess;
using MediaSet.Api.Infrastructure.Lookup.Strategies;
using MediaSet.Api.Shared.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using MongoDB.Driver;
using Moq;
using NUnit.Framework;

namespace MediaSet.Api.Tests;

/// <summary>
/// Boots the API with no lookup provider section (OpenLibrary, TMDB, MusicBrainz) configured.
/// The Production environment is used because it never loads personal user-secrets, and the
/// default appsettings.json contains none of those sections.
/// </summary>
[TestFixture]
public class NoLookupProviderTests
{
    private readonly List<WebApplicationFactory<Program>> _factories = [];

    [TearDown]
    public void TearDown()
    {
        foreach (var factory in _factories)
        {
            factory.Dispose();
        }
        _factories.Clear();
    }

    private HttpClient CreateClient(string environment = "Production", bool removeStrategies = false,
        IReadOnlyDictionary<string, string>? extraSettings = null)
    {
        var databaseMock = new Mock<IDatabaseService>();
        var collectionMock = new Mock<IMongoCollection<BsonDocument>>();
        var mongoDatabaseMock = new Mock<IMongoDatabase>();
        collectionMock.Setup(c => c.Database).Returns(mongoDatabaseMock.Object);
        mongoDatabaseMock.Setup(d => d.RunCommandAsync<BsonDocument>(
                It.IsAny<Command<BsonDocument>>(), It.IsAny<ReadPreference>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BsonDocument { { "ok", 1 } });
        databaseMock.Setup(s => s.GetCollection<BsonDocument>()).Returns(collectionMock.Object);

        var bookServiceMock = new Mock<IEntityService<Book>>();
        bookServiceMock.Setup(s => s.GetListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Book>());

        var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment(environment);
            builder.UseSetting("MediaSetDatabaseSettings:ConnectionString", "mongodb://127.0.0.1:27017");
            builder.UseSetting("ImageConfiguration:StoragePath", TestContext.CurrentContext.WorkDirectory);
            builder.UseSetting("ExternalLogging:Enabled", "false");
            builder.UseSetting("BackgroundImageLookupConfiguration:Enabled", "false");
            foreach (var (key, value) in extraSettings ?? new Dictionary<string, string>())
            {
                builder.UseSetting(key, value);
            }
            builder.ConfigureServices(services =>
            {
                Replace(services, typeof(IDatabaseService), databaseMock.Object);
                Replace(services, typeof(IEntityService<Book>), bookServiceMock.Object);
                if (removeStrategies)
                {
                    // Independent of any provider secrets the developer machine may load in Development
                    foreach (var d in services.Where(d => d.ServiceType.IsGenericType
                        && d.ServiceType.GetGenericTypeDefinition() == typeof(ILookupStrategy<>)).ToList())
                    {
                        services.Remove(d);
                    }
                    var bookServiceMock = new Mock<IEntityService<Book>>();
                    bookServiceMock.Setup(x => x.GetAsync("abc", It.IsAny<CancellationToken>()))
                        .ReturnsAsync(new Book { Id = "abc", Title = "Dune", ISBN = "9780441172719" });
                    Replace(services, typeof(IEntityService<Book>), bookServiceMock.Object);
                }
            });
        });
        _factories.Add(factory);
        return factory.CreateClient();
    }

    private static void Replace(IServiceCollection services, System.Type serviceType, object instance)
    {
        foreach (var d in services.Where(d => d.ServiceType == serviceType).ToList())
        {
            services.Remove(d);
        }
        services.AddSingleton(serviceType, instance);
    }

    private static async Task AssertLookupNotConfiguredProblemAsync(HttpResponseMessage response)
    {
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.ServiceUnavailable));
        Assert.That(response.Content.Headers.ContentType?.MediaType, Is.EqualTo("application/problem+json"));
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.That(doc.RootElement.GetProperty("status").GetInt32(), Is.EqualTo(503));
        var detail = doc.RootElement.GetProperty("detail").GetString();
        Assert.That(detail, Does.Contain("OpenLibraryConfiguration"));
        Assert.That(detail, Does.Contain("TmdbConfiguration"));
        Assert.That(detail, Does.Contain("MusicBrainzConfiguration"));
    }

    [Test]
    public async Task Health_NoLookupProviderConfigured_ReturnsOk()
    {
        // Arrange
        using var client = CreateClient();

        // Act
        var response = await client.GetAsync("/health");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    [Test]
    public async Task GetBooks_NoLookupProviderConfigured_ReturnsOk()
    {
        // Arrange
        using var client = CreateClient();

        // Act
        var response = await client.GetAsync("/books");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    [Test]
    public async Task Lookup_NoLookupProviderConfigured_ReturnsProblemDetails503()
    {
        // Arrange
        using var client = CreateClient();

        // Act
        var response = await client.GetAsync("/lookup/Books/isbn/9780140328721");

        // Assert
        await AssertLookupNotConfiguredProblemAsync(response);
    }

    [Test]
    public async Task EntityLookup_NoLookupProviderConfigured_ReturnsProblemDetails503()
    {
        // Arrange
        using var client = CreateClient();

        // Act
        var response = await client.GetAsync("/lookup/Books/entity?title=Dune");

        // Assert
        await AssertLookupNotConfiguredProblemAsync(response);
    }

    [Test]
    public async Task DevImageLookup_ServiceRegisteredButNoStrategies_ReturnsProblemDetails503()
    {
        // Arrange
        using var client = CreateClient("Development", removeStrategies: true);

        // Act
        var response = await client.PostAsync("/images/dev/lookup/Books/abc", content: null);

        // Assert
        await AssertLookupNotConfiguredProblemAsync(response);
    }

    [Test]
    public async Task Lookup_MusicBrainzConfigured_DoesNotReturn503()
    {
        // Arrange
        using var client = CreateClient(extraSettings: MusicBrainzSettings);

        // Act
        var response = await client.GetAsync("/lookup/Books/isbn/9780140328721");

        // Assert: no Books strategy exists, so the request is rejected as unsupported, not as unconfigured
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task Lookup_IgdbWithUpcItemDbConfigured_DoesNotReturn503()
    {
        // Arrange
        using var client = CreateClient(extraSettings: new Dictionary<string, string>
        {
            ["IgdbConfiguration:ClientId"] = "id",
            ["IgdbConfiguration:ClientSecret"] = "secret",
            ["UpcItemDbConfiguration:BaseUrl"] = "https://api.upcitemdb.com",
            ["UpcItemDbConfiguration:Timeout"] = "30",
            ["UpcItemDbConfiguration:MaxRequestsPerMinute"] = "100",
            ["UpcItemDbConfiguration:MaxRequestsPerDay"] = "1000",
            ["UpcItemDbConfiguration:MinDelayBetweenRequestsMs"] = "0",
            ["UpcItemDbConfiguration:MaxRetryPauseSeconds"] = "65"
        });

        // Act: an unsupported search property is rejected after the Game strategy is resolved
        var response = await client.GetAsync("/lookup/Games/entity?bogus=x");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task Lookup_IgdbCredentialsWithoutUpcItemDb_ReturnsProblemDetails503()
    {
        // Arrange: a Game strategy needs both IGDB credentials and UpcItemDb, so none is registered
        using var client = CreateClient(extraSettings: new Dictionary<string, string>
        {
            ["IgdbConfiguration:ClientId"] = "id",
            ["IgdbConfiguration:ClientSecret"] = "secret"
        });

        // Act
        var response = await client.GetAsync("/lookup/Games/entity?title=Halo");

        // Assert
        await AssertLookupNotConfiguredProblemAsync(response);
    }

    private static readonly Dictionary<string, string> MusicBrainzSettings = new()
    {
        ["MusicBrainzConfiguration:BaseUrl"] = "https://musicbrainz.org/ws/2",
        ["MusicBrainzConfiguration:Timeout"] = "30",
        ["MusicBrainzConfiguration:ContactEmail"] = "test@example.com"
    };

    [Test]
    public async Task Lookup_ProviderConfigured_StillResolvesStrategies()
    {
        // Arrange
        using var factory = CreateWebApplicationFactoryWithProviders();
        using var scope = factory.Services.CreateScope();
        var strategyFactory = scope.ServiceProvider.GetRequiredService<LookupStrategyFactory>();

        // Act
        var strategy = strategyFactory.GetStrategyBase(MediaTypes.Musics);

        // Assert
        Assert.That(strategy, Is.Not.Null);
    }

    private static WebApplicationFactory<Program> CreateWebApplicationFactoryWithProviders()
    {
        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Production");
            builder.UseSetting("MediaSetDatabaseSettings:ConnectionString", "mongodb://127.0.0.1:27017");
            builder.UseSetting("ImageConfiguration:StoragePath", TestContext.CurrentContext.WorkDirectory);
            builder.UseSetting("ExternalLogging:Enabled", "false");
            builder.UseSetting("BackgroundImageLookupConfiguration:Enabled", "false");
            builder.UseSetting("MusicBrainzConfiguration:BaseUrl", "https://musicbrainz.org/ws/2");
            builder.UseSetting("MusicBrainzConfiguration:Timeout", "30");
            builder.UseSetting("MusicBrainzConfiguration:ContactEmail", "test@example.com");
        });
    }
}
