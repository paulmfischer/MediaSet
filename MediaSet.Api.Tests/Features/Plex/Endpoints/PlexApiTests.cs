using System;
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
    public async Task GetPlexStatus_PlexConfigured_ReturnsEntryPerEntityTypeWithNextRunAndNoRuns()
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
        Assert.That(entries[0].GetProperty("nextRun").ValueKind, Is.EqualTo(JsonValueKind.String));
        Assert.That(entries[0].GetProperty("schedulePolicy").GetString(), Is.EqualTo("queue"));
        Assert.That(body, Does.Not.Contain(Token));
    }

    [Test]
    public async Task GetPlexStatus_DefaultSchedule_NextRunIsSundayThreeLocalInTheFuture()
    {
        using var client = CreateClient(PlexSettings());

        var entry = (await ReadJsonAsync(await client.GetAsync("/plex/status"))).EnumerateArray().Single();
        var next = entry.GetProperty("nextRun").GetDateTimeOffset();

        Assert.That(next, Is.GreaterThan(DateTimeOffset.UtcNow));
        Assert.That(next.DayOfWeek, Is.EqualTo(DayOfWeek.Sunday));
        Assert.That(next.Hour, Is.EqualTo(3));
        Assert.That(next.Offset, Is.EqualTo(TimeZoneInfo.Local.GetUtcOffset(next)));
    }

    [Test]
    public async Task GetEntityStatus_CustomSchedule_ReportsNextRunAndPolicy()
    {
        using var client = CreateClient(PlexSettings(schedule: "30 5 * * *"));

        var status = await ReadJsonAsync(await client.GetAsync("/plex/movies/status"));
        var next = status.GetProperty("nextRun").GetDateTimeOffset();

        Assert.That(next.Hour, Is.EqualTo(5));
        Assert.That(next.Minute, Is.EqualTo(30));
        Assert.That(next, Is.LessThanOrEqualTo(DateTimeOffset.UtcNow.AddDays(1).AddHours(1)));
        Assert.That(status.GetProperty("schedulePolicy").GetString(), Is.EqualTo("queue"));
    }

    [Test]
    public async Task GetPlexStatus_InvalidSchedule_DisablesTypeWithNoNextRunAndAppStillServes()
    {
        using var client = CreateClient(PlexSettings(schedule: "not a cron"));

        var response = await client.GetAsync("/plex/status");
        var entry = (await ReadJsonAsync(response)).EnumerateArray().Single();

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(entry.GetProperty("enabled").GetBoolean(), Is.False);
        Assert.That(entry.GetProperty("nextRun").ValueKind, Is.EqualTo(JsonValueKind.Null));
    }

    [Test]
    public async Task GetPlexStatus_DisabledEntityType_HasNoNextRun()
    {
        using var client = CreateClient(PlexSettings(moviesEnabled: "false"));

        var entry = (await ReadJsonAsync(await client.GetAsync("/plex/status"))).EnumerateArray().Single();

        Assert.That(entry.GetProperty("nextRun").ValueKind, Is.EqualTo(JsonValueKind.Null));
    }

    [Test]
    public void Services_NoPlexSection_NoSchedulerRegistered()
    {
        CreateClient();
        var factory = _factories.Single();

        var hosted = factory.Services.GetServices<Microsoft.Extensions.Hosting.IHostedService>();

        Assert.That(hosted.OfType<MediaSet.Api.Features.Plex.Services.PlexSyncScheduler>(), Is.Empty);
    }

    [Test]
    public void Services_PlexConfigured_SchedulerRegistered()
    {
        CreateClient(PlexSettings());
        var factory = _factories.Single();

        var hosted = factory.Services.GetServices<Microsoft.Extensions.Hosting.IHostedService>();

        Assert.That(hosted.OfType<MediaSet.Api.Features.Plex.Services.PlexSyncScheduler>(), Has.Exactly(1).Items);
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
    public async Task GetEntityStatus_DisabledEntityType_ReportsDisabledWithNoNextRun()
    {
        using var client = CreateClient(PlexSettings(moviesEnabled: "false"));

        var response = await client.GetAsync("/plex/movies/status");
        var status = await ReadJsonAsync(response);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(status.GetProperty("entityType").GetString(), Is.EqualTo("movies"));
        Assert.That(status.GetProperty("enabled").GetBoolean(), Is.False);
        Assert.That(status.GetProperty("nextRun").ValueKind, Is.EqualTo(JsonValueKind.Null));
    }

    [Test]
    public async Task PostEntitySync_DisabledEntityType_ReturnsBadRequestProblem()
    {
        using var client = CreateClient(PlexSettings(moviesEnabled: "false"));

        var response = await client.PostAsync("/plex/movies/sync", null);
        var problem = await ReadJsonAsync(response);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That(response.Content.Headers.ContentType?.MediaType, Is.EqualTo("application/problem+json"));
        Assert.That(problem.GetProperty("detail").GetString(), Does.Contain("not enabled"));
    }

    [Test]
    public async Task OpenApi_EntityScopedEndpoints_DeclareRequiredEntityTypePathParameter()
    {
        using var client = CreateClient(new Dictionary<string, string>(PlexSettings()) { ["EnableSwagger"] = "true" });

        var doc = await ReadJsonAsync(await client.GetAsync("/swagger/v1/swagger.json"));
        var paths = doc.GetProperty("paths").EnumerateObject()
            .Where(p => p.Name.StartsWith("/plex/{entityType}/", StringComparison.Ordinal))
            .ToList();

        Assert.That(paths, Is.Not.Empty);
        foreach (var path in paths)
        {
            foreach (var operation in path.Value.EnumerateObject())
            {
                var parameters = operation.Value.GetProperty("parameters").EnumerateArray();
                var entityType = parameters.SingleOrDefault(p => p.GetProperty("name").GetString() == "entityType");
                Assert.That(entityType.ValueKind, Is.EqualTo(JsonValueKind.Object), $"{operation.Name} {path.Name}");
                Assert.That(entityType.GetProperty("in").GetString(), Is.EqualTo("path"), $"{operation.Name} {path.Name}");
                Assert.That(entityType.GetProperty("required").GetBoolean(), Is.True, $"{operation.Name} {path.Name}");
            }
        }
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
