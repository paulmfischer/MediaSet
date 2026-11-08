using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using MediaSet.Api.Infrastructure.Database;
using MediaSet.Api.Infrastructure.Plex;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration.EnvironmentVariables;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using NUnit.Framework;

namespace MediaSet.Api.Tests.Features.Plex.Endpoints;

/// <summary>Search endpoint over HTTP, with the Plex server replaced by fixtures served from a fake handler.</summary>
[TestFixture]
public class PlexSearchApiTests
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

    private HttpClient CreateClient(FakePlexHandler handler, bool moviesEnabled = true, string? sectionIds = null)
    {
        var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Production");
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
            builder.UseSetting("PlexConfiguration:BaseUrl", "http://plex.local:32400");
            builder.UseSetting("PlexConfiguration:Token", Token);
            builder.UseSetting("PlexConfiguration:Entities:Movies:Enabled", moviesEnabled.ToString());
            if (sectionIds != null)
            {
                builder.UseSetting("PlexConfiguration:Entities:Movies:LibrarySectionIds:0", sectionIds);
            }
            builder.ConfigureServices(services =>
            {
                foreach (var d in services.Where(d => d.ServiceType == typeof(IDatabaseService)).ToList())
                {
                    services.Remove(d);
                }
                services.AddSingleton(new Mock<IDatabaseService>().Object);
                services.AddHttpClient(nameof(IPlexClient)).ConfigurePrimaryHttpMessageHandler(() => handler);
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

    private static FakePlexHandler ServerWithMovieSections() => FakePlexHandler.WithServer()
        .Route("/library/sections/1/all", "movie.json")
        .Route("/library/sections/3/all", "subtitles_audio.json")
        .Route("/library/sections/2/all", "show.json");

    [Test]
    public async Task Search_MatchingTitle_ReturnsTitleIdYearLibraryAndGuids()
    {
        using var client = CreateClient(ServerWithMovieSections());

        var response = await client.GetAsync("/plex/movies/search?query=matrix");
        var first = (await ReadJsonAsync(response)).EnumerateArray().First();

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(first.GetProperty("title").GetString(), Is.EqualTo("The Matrix"));
        Assert.That(first.GetProperty("plexItemId").GetString(), Is.EqualTo("1001"));
        Assert.That(first.GetProperty("year").GetInt32(), Is.EqualTo(1999));
        Assert.That(first.GetProperty("library").GetString(), Is.EqualTo("Movies"));
        Assert.That(first.GetProperty("guids").GetProperty("tmdb").GetString(), Is.EqualTo("603"));
        Assert.That(first.GetProperty("guids").GetProperty("imdb").GetString(), Is.EqualTo("tt0133093"));
        Assert.That(first.GetProperty("guids").GetProperty("tvdb").GetString(), Is.EqualTo("169"));
    }

    [Test]
    public async Task Search_ForMovies_NeverQueriesShowLibrariesAndReturnsNoShows()
    {
        var handler = ServerWithMovieSections();
        using var client = CreateClient(handler);

        var results = (await ReadJsonAsync(await client.GetAsync("/plex/movies/search?query=matrix"))).EnumerateArray().ToList();

        Assert.That(handler.Requests.Select(r => r.Uri.AbsolutePath), Does.Not.Contain("/library/sections/2/all"));
        Assert.That(results.Select(r => r.GetProperty("title").GetString()), Does.Not.Contain("The Matrix Show"));
    }

    [Test]
    public async Task Search_MovieLibraryContainsShowItem_ShowIsExcluded()
    {
        var handler = FakePlexHandler.WithServer().Route("/library/sections/1/all", "show.json").Route("/library/sections/3/all", "movie.json");
        using var client = CreateClient(handler);

        var results = (await ReadJsonAsync(await client.GetAsync("/plex/movies/search?query=matrix"))).EnumerateArray().ToList();

        Assert.That(results.Select(r => r.GetProperty("title").GetString()), Is.EqualTo(["The Matrix"]));
    }

    [Test]
    public async Task Search_SectionIdsConfigured_OnlyConfiguredLibrariesAreSearched()
    {
        var handler = ServerWithMovieSections();
        using var client = CreateClient(handler, sectionIds: "3");

        var results = (await ReadJsonAsync(await client.GetAsync("/plex/movies/search?query=amelie"))).EnumerateArray().ToList();

        Assert.That(handler.Requests.Select(r => r.Uri.AbsolutePath), Does.Contain("/library/sections/3/all"));
        Assert.That(handler.Requests.Select(r => r.Uri.AbsolutePath), Does.Not.Contain("/library/sections/1/all"));
        Assert.That(results.Select(r => r.GetProperty("library").GetString()), Is.All.EqualTo("Kids Movies"));
    }

    [Test]
    public async Task Search_Query_IsPassedToPlexAsTitleFilter()
    {
        var handler = ServerWithMovieSections();
        using var client = CreateClient(handler);

        await client.GetAsync("/plex/movies/search?query=%20matrix%20");

        var request = handler.Requests.First(r => r.Uri.AbsolutePath == "/library/sections/1/all");
        Assert.That(request.Uri.Query, Is.EqualTo("?includeGuids=1&title=matrix"));
    }

    [Test]
    public async Task Search_PlexEntityTypeDisabled_ReturnsBadRequestProblemWithoutCallingPlex()
    {
        var handler = ServerWithMovieSections();
        using var client = CreateClient(handler, moviesEnabled: false);

        var response = await client.GetAsync("/plex/movies/search?query=matrix");
        var problem = await ReadJsonAsync(response);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That(problem.GetProperty("detail").GetString(), Does.Contain("Plex is not enabled for entity type"));
        Assert.That(handler.Requests, Is.Empty);
    }

    [TestCase("")]
    [TestCase("   ")]
    [TestCase("a")]
    [TestCase(" a ")]
    [TestCase(null)]
    public async Task Search_BlankOrTooShortQuery_ReturnsBadRequestProblem(string? query)
    {
        var handler = ServerWithMovieSections();
        using var client = CreateClient(handler);

        var response = await client.GetAsync(query == null ? "/plex/movies/search" : $"/plex/movies/search?query={query}");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That(response.Content.Headers.ContentType?.MediaType, Is.EqualTo("application/problem+json"));
        Assert.That(handler.Requests, Is.Empty);
    }

    [Test]
    public async Task Search_PlexReturnsError_ReturnsBadGatewayProblemWithoutTokenOrUrl()
    {
        var handler = new FakePlexHandler().Route("/library/sections",
            _ => new HttpResponseMessage(HttpStatusCode.Unauthorized));
        using var client = CreateClient(handler);

        var response = await client.GetAsync("/plex/movies/search?query=matrix");
        var body = await response.Content.ReadAsStringAsync();

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadGateway));
        Assert.That(response.Content.Headers.ContentType?.MediaType, Is.EqualTo("application/problem+json"));
        Assert.That(body, Does.Not.Contain(Token).And.Not.Contain("plex.local"));
    }

    [Test]
    public async Task Search_PlexUnreachable_ReturnsBadGatewayProblemWithoutTokenOrUrl()
    {
        var handler = new FakePlexHandler().Route("/library/sections",
            _ => throw new HttpRequestException($"refused http://plex.local:32400/?X-Plex-Token={Token}"));
        using var client = CreateClient(handler);

        var response = await client.GetAsync("/plex/movies/search?query=matrix");
        var body = await response.Content.ReadAsStringAsync();

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadGateway));
        Assert.That(body, Does.Not.Contain(Token).And.Not.Contain("plex.local"));
    }

    [Test]
    public async Task Search_AnyRequest_OnlySendsGetRequestsToPlex()
    {
        var handler = ServerWithMovieSections();
        using var client = CreateClient(handler);

        await client.GetAsync("/plex/movies/search?query=matrix");

        Assert.That(handler.Requests, Is.Not.Empty);
        Assert.That(handler.Requests.Select(r => r.Method), Is.All.EqualTo(HttpMethod.Get));
    }

    [Test]
    public async Task Search_AnyRequest_SendsTokenOnlyInHeaderNeverInUrl()
    {
        var handler = ServerWithMovieSections();
        using var client = CreateClient(handler);

        await client.GetAsync("/plex/movies/search?query=matrix");

        Assert.That(handler.Requests.Select(r => r.Headers["X-Plex-Token"]), Is.All.EqualTo(Token));
        Assert.That(handler.Requests.Select(r => r.Uri.ToString()), Is.All.Not.Contain(Token));
    }

    [Test]
    public async Task Search_IdentityEndpointFails_StillReturnsResults()
    {
        var handler = ServerWithMovieSections().Route("/identity", _ => new HttpResponseMessage(HttpStatusCode.InternalServerError));
        using var client = CreateClient(handler);

        var response = await client.GetAsync("/plex/movies/search?query=matrix");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(handler.Requests.Select(r => r.Uri.AbsolutePath), Does.Not.Contain("/identity"));
    }

    [Test]
    public async Task Search_MoreMatchesThanCap_ReturnsOnlyCapResults()
    {
        var items = string.Join(",", Enumerable.Range(1, 80).Select(i => $$"""{"ratingKey":"{{i}}","type":"movie","title":"Film {{i}}"}"""));
        var handler = FakePlexHandler.WithServer().Route("/library/sections/1/all",
            _ => FakePlexHandler.Json("{\"MediaContainer\":{\"size\":80,\"totalSize\":80,\"Metadata\":[" + items + "]}}"));
        using var client = CreateClient(handler, sectionIds: "1");

        var results = (await ReadJsonAsync(await client.GetAsync("/plex/movies/search?query=film"))).EnumerateArray().ToList();

        Assert.That(results, Has.Count.EqualTo(MediaSet.Api.Features.Plex.Services.PlexSearchService.MaxResults));
    }

    [Test]
    public async Task Search_AnyRequest_NeverFetchesPerItemMetadata()
    {
        var handler = ServerWithMovieSections();
        using var client = CreateClient(handler);

        await client.GetAsync("/plex/movies/search?query=matrix");

        Assert.That(handler.Requests.Select(r => r.Uri.AbsolutePath), Has.None.StartsWith("/library/metadata"));
    }
}
