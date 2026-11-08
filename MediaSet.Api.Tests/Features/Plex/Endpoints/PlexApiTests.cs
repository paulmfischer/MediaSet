using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using MediaSet.Api.Infrastructure.Database;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration.EnvironmentVariables;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using NUnit.Framework;

namespace MediaSet.Api.Tests.Features.Plex.Endpoints;

/// <summary>
/// Boots the API in Production (never loads user-secrets) with and without a Plex section.
/// </summary>
[TestFixture]
public class PlexApiTests
{
    private const string Token = "super-secret-token-value";
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

    private static Dictionary<string, string> PlexSettings(string moviesEnabled = "true", string schedule = "0 3 * * 0") => new()
    {
        ["PlexConfiguration:BaseUrl"] = "http://plex.local:32400",
        ["PlexConfiguration:Token"] = Token,
        ["PlexConfiguration:Entities:Movies:Enabled"] = moviesEnabled,
        ["PlexConfiguration:Entities:Movies:Schedule"] = schedule
    };

    private HttpClient CreateClient(IReadOnlyDictionary<string, string>? settings = null)
    {
        var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Production");
            // Ignore PlexConfiguration__* variables of the machine running the tests
            builder.ConfigureAppConfiguration((_, config) =>
            {
                foreach (var source in config.Sources.OfType<EnvironmentVariablesConfigurationSource>().ToList())
                {
                    config.Sources.Remove(source);
                }
            });
            builder.UseSetting("MediaSetDatabaseSettings:ConnectionString", "mongodb://127.0.0.1:27017");
            builder.UseSetting("ImageConfiguration:StoragePath", TestContext.CurrentContext.WorkDirectory);
            builder.UseSetting("ExternalLogging:Enabled", "false");
            builder.UseSetting("BackgroundImageLookupConfiguration:Enabled", "false");
            foreach (var (key, value) in settings ?? new Dictionary<string, string>())
            {
                builder.UseSetting(key, value);
            }
            builder.ConfigureServices(services =>
            {
                foreach (var d in services.Where(d => d.ServiceType == typeof(IDatabaseService)).ToList())
                {
                    services.Remove(d);
                }
                services.AddSingleton(new Mock<IDatabaseService>().Object);
                InMemoryPlexSyncRunStore.Replace(services);
            });
        });
        _factories.Add(factory);
        return factory.CreateClient();
    }

    private static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response)
    {
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return doc.RootElement.Clone();
    }

    private static JsonElement PlexEntry(JsonElement integrations) =>
        integrations.EnumerateArray().Single(e => e.GetProperty("key").GetString() == "plex");

    [Test]
    public async Task GetPlexStatus_NoPlexSection_ReturnsNotFound()
    {
        using var client = CreateClient();

        var response = await client.GetAsync("/plex/status");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task GetEntityStatus_NoPlexSection_ReturnsNotFound()
    {
        using var client = CreateClient();

        var response = await client.GetAsync("/plex/movies/status");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public void Services_NoPlexSection_NoPlexServicesRegistered()
    {
        CreateClient();
        var factory = _factories.Single();

        var enablement = factory.Services.GetService<MediaSet.Api.Infrastructure.Plex.PlexEnablement>();

        Assert.That(enablement, Is.Null);
    }

    [Test]
    public async Task GetIntegrations_NoPlexSection_PlexEntryDisabled()
    {
        using var client = CreateClient();

        var response = await client.GetAsync("/config/integrations");
        var plex = PlexEntry(await ReadJsonAsync(response));

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(plex.GetProperty("enabled").GetBoolean(), Is.False);
    }

    [Test]
    public async Task GetIntegrations_PlexConfigured_ListsEnabledEntityTypes()
    {
        using var client = CreateClient(PlexSettings());

        var plex = PlexEntry(await ReadJsonAsync(await client.GetAsync("/config/integrations")));

        Assert.That(plex.GetProperty("enabled").GetBoolean(), Is.True);
        Assert.That(plex.GetProperty("entityTypes").EnumerateArray().Select(e => e.GetString()), Is.EqualTo(["movies"]));
    }

    [Test]
    public async Task GetIntegrations_OtherIntegration_EntityTypesIsNull()
    {
        using var client = CreateClient(PlexSettings());

        var integrations = await ReadJsonAsync(await client.GetAsync("/config/integrations"));
        var tmdb = integrations.EnumerateArray().Single(e => e.GetProperty("key").GetString() == "tmdb");

        Assert.That(tmdb.GetProperty("entityTypes").ValueKind, Is.EqualTo(JsonValueKind.Null));
    }

    [Test]
    public async Task GetIntegrations_PlexConfigured_ResponseDoesNotContainToken()
    {
        using var client = CreateClient(PlexSettings());

        var body = await (await client.GetAsync("/config/integrations")).Content.ReadAsStringAsync();

        Assert.That(body, Does.Not.Contain(Token));
    }

    [Test]
    public async Task GetPlexStatus_PlexConfigured_ReturnsEntryPerEntityTypeWithEmptyRuns()
    {
        using var client = CreateClient(PlexSettings());

        var response = await client.GetAsync("/plex/status");
        var body = await response.Content.ReadAsStringAsync();
        var entries = (await ReadJsonAsync(response)).EnumerateArray().ToList();

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(entries, Has.Count.EqualTo(1));
        Assert.That(entries[0].GetProperty("entityType").GetString(), Is.EqualTo("movies"));
        Assert.That(entries[0].GetProperty("enabled").GetBoolean(), Is.True);
        Assert.That(entries[0].GetProperty("lastRun").ValueKind, Is.EqualTo(JsonValueKind.Null));
        Assert.That(entries[0].GetProperty("nextRun").ValueKind, Is.EqualTo(JsonValueKind.Null));
        Assert.That(body, Does.Not.Contain(Token));
    }

    [Test]
    public async Task GetEntityStatus_EnabledEntityType_ReturnsStatus()
    {
        using var client = CreateClient(PlexSettings());

        var response = await client.GetAsync("/plex/Movies/status");
        var status = await ReadJsonAsync(response);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(status.GetProperty("entityType").GetString(), Is.EqualTo("movies"));
        Assert.That(status.GetProperty("enabled").GetBoolean(), Is.True);
    }

    [Test]
    public async Task GetEntityStatus_DisabledEntityType_ReturnsBadRequestProblem()
    {
        using var client = CreateClient(PlexSettings(moviesEnabled: "false"));

        var response = await client.GetAsync("/plex/movies/status");
        var problem = await ReadJsonAsync(response);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That(response.Content.Headers.ContentType?.MediaType, Is.EqualTo("application/problem+json"));
        Assert.That(problem.GetProperty("detail").GetString(), Does.Contain("not enabled"));
    }

    [Test]
    public async Task GetEntityStatus_UnknownEntityType_ReturnsNotFoundProblem()
    {
        using var client = CreateClient(PlexSettings());

        var response = await client.GetAsync("/plex/books/status");
        var problem = await ReadJsonAsync(response);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        Assert.That(response.Content.Headers.ContentType?.MediaType, Is.EqualTo("application/problem+json"));
        Assert.That(problem.GetProperty("detail").GetString(), Does.Contain("not supported"));
    }

    [Test]
    public async Task GetPlexStatus_InvalidCron_ReportsMoviesDisabledButEndpointsRegistered()
    {
        using var client = CreateClient(PlexSettings(schedule: "not a cron"));

        var statusResponse = await client.GetAsync("/plex/status");
        var entry = (await ReadJsonAsync(statusResponse)).EnumerateArray().Single();
        var plex = PlexEntry(await ReadJsonAsync(await client.GetAsync("/config/integrations")));

        Assert.That(statusResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(entry.GetProperty("enabled").GetBoolean(), Is.False);
        Assert.That(plex.GetProperty("enabled").GetBoolean(), Is.True);
        Assert.That(plex.GetProperty("entityTypes").GetArrayLength(), Is.EqualTo(0));
    }

    [TestCase("PlexConfiguration:BaseUrl")]
    [TestCase("PlexConfiguration:Token")]
    public async Task GetPlexStatus_MissingBaseUrlOrToken_IntegrationDisabledAndNotFound(string missingKey)
    {
        var settings = PlexSettings();
        settings[missingKey] = "";
        using var client = CreateClient(settings);

        var statusResponse = await client.GetAsync("/plex/status");
        var plex = PlexEntry(await ReadJsonAsync(await client.GetAsync("/config/integrations")));

        Assert.That(statusResponse.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        Assert.That(plex.GetProperty("enabled").GetBoolean(), Is.False);
    }

    [Test]
    public async Task GetPlexStatus_MalformedSetting_StartsWithPlexDisabled()
    {
        var settings = PlexSettings();
        settings["PlexConfiguration:Timeout"] = "abc";
        using var client = CreateClient(settings);

        var statusResponse = await client.GetAsync("/plex/status");
        var integrations = await client.GetAsync("/config/integrations");
        var plex = PlexEntry(await ReadJsonAsync(integrations));

        Assert.That(statusResponse.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        Assert.That(plex.GetProperty("enabled").GetBoolean(), Is.False);
    }

    [Test]
    public async Task GetPlexStatus_UnsupportedEntityKey_ReportsOnlySupportedTypes()
    {
        var settings = PlexSettings();
        settings["PlexConfiguration:Entities:Books:Enabled"] = "true";
        using var client = CreateClient(settings);

        var statusResponse = await client.GetAsync("/plex/status");
        var entries = (await ReadJsonAsync(statusResponse)).EnumerateArray().ToList();
        var booksResponse = await client.GetAsync("/plex/books/status");
        var plex = PlexEntry(await ReadJsonAsync(await client.GetAsync("/config/integrations")));

        Assert.That(entries.Select(e => e.GetProperty("entityType").GetString()), Is.EqualTo(["movies"]));
        Assert.That(entries[0].GetProperty("enabled").GetBoolean(), Is.True);
        Assert.That(booksResponse.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        Assert.That(plex.GetProperty("entityTypes").EnumerateArray().Select(e => e.GetString()), Is.EqualTo(["movies"]));
    }
}
