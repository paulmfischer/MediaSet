using System.Text.Json;
using MediaSet.Api.Features.Images.Models;
using MediaSet.Api.Features.Images.Services;
using MediaSet.Api.Infrastructure.Database;
using MediaSet.Api.Infrastructure.Lookup.Clients.Igdb;
using MediaSet.Api.Infrastructure.Lookup.Clients.UpcItemDb;
using MediaSet.Api.Infrastructure.Lookup.Models;
using MediaSet.Api.Infrastructure.Lookup.Strategies;
using MediaSet.Api.Infrastructure.Storage;
using MediaSet.Api.Shared.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using NUnit.Framework;
using SkiaSharp;

namespace MediaSet.ImageLookup.Regressions;

public sealed partial class RegressionSuite
{
    private const string GameId = "000000000000000000000030";
    private const string GameTitle = "Fixture Game";
    private const string GameArt = "https://images.igdb.com/igdb/image/upload/t_cover_big/game.png";

    [Test]
    public async Task ProcessGame_SuccessfulLookups_StoreImagesAndReuseAuthenticationAsync()
    {
        using var scenario = new GameScenario(this);
        scenario.Auth();
        scenario.Search(GameResults());
        scenario.Search(GameResults());
        scenario.Art();
        scenario.Art();
        await scenario.InsertAsync(new Game { Title = GameTitle });
        await scenario.ProcessAsync();
        await scenario.AssertImageAsync("30", GameArt);
        // A second entity proves token reuse across production scopes/batches.
        const string secondId = "000000000000000000000031";
        await scenario.InsertAsync(new Game { Id = secondId, Title = GameTitle });
        await scenario.ProcessAsync();
        await scenario.AssertImageAsync("30", GameArt, id: secondId, fileCount: 2);
        await scenario.ProcessAsync();
        Assert.That((await scenario.SavedAsync()).ImageLookup!.AttemptCount, Is.EqualTo(1));
        Assert.That((await scenario.SavedAsync(secondId)).ImageLookup!.AttemptCount, Is.EqualTo(1));
        scenario.Verify("auth", "search", "art", "search", "art");
    }

    [Test]
    public async Task ProcessGame_FreshScenario_DoesNotReuseAnotherScenarioTokenAsync()
    {
        using (var first = new GameScenario(this))
        {
            first.Auth();
            first.Search("[]");
            await first.InsertAsync(new Game { Title = GameTitle });
            await first.ProcessAsync();
            await first.AssertTerminalAsync(ImageLookupOutcome.NoMatch);
            first.Verify("auth", "search");
        }
        await _database.GetCollection<Game>().DeleteManyAsync(Builders<Game>.Filter.Empty);
        using var second = new GameScenario(this);
        second.Auth();
        second.Search(GameResults());
        second.Art();
        await second.InsertAsync(new Game { Title = GameTitle });
        await second.ProcessAsync();
        await second.AssertImageAsync("30", GameArt);
        second.Verify("auth", "search", "art");
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task ProcessGame_NoMatchOrMatchedWithoutArtwork_PersistsTerminalOutcomeAsync(bool matched)
    {
        using var scenario = new GameScenario(this);
        scenario.Auth();
        scenario.Search(matched ? GameResults(artwork: false) : "[]");
        await scenario.InsertAsync(new Game { Title = GameTitle });
        await scenario.ProcessAsync();
        await scenario.AssertTerminalAsync(matched ? ImageLookupOutcome.MissingArtwork : ImageLookupOutcome.NoMatch,
            matched ? "30" : null);
        scenario.Verify("auth", "search");
    }

    [Test]
    public async Task ProcessGame_UnavailableArtwork_PersistsConfirmedIdentityAndMissingArtworkAsync()
    {
        using var scenario = new GameScenario(this);
        scenario.Auth();
        scenario.Search(GameResults());
        scenario.Art(status: 404);
        await scenario.InsertAsync(new Game { Title = GameTitle });
        await scenario.ProcessAsync();
        await scenario.AssertTerminalAsync(ImageLookupOutcome.MissingArtwork, "30");
        scenario.Verify("auth", "search", "art");
    }

    [TestCase(false, "401")]
    [TestCase(false, "403")]
    [TestCase(false, "503")]
    [TestCase(false, "network")]
    [TestCase(false, "timeout")]
    [TestCase(true, "401")]
    [TestCase(true, "403")]
    [TestCase(true, "503")]
    [TestCase(true, "network")]
    [TestCase(true, "timeout")]
    public async Task ProcessGame_AuthenticationOrProviderFailure_RetriesAndRecoversAsync(bool authentication, string failure)
    {
        using var assertions = Assert.EnterMultipleScope();
        using var scenario = new GameScenario(this);
        var status = int.TryParse(failure, out var code) ? code : 200;
        var transportFailure = status == 200 ? failure : null;
        if (authentication)
        {
            scenario.Auth(status, transportFailure);
            scenario.Auth();
        }
        else
        {
            scenario.Auth();
            scenario.Search("{}", status, transportFailure);
        }
        scenario.Search(GameResults());
        scenario.Art();
        await scenario.InsertAsync(new Game { Title = GameTitle });
        await scenario.ProcessAsync();
        var outcome = status is 401 or 403 ? ImageLookupOutcome.Unauthorized : ImageLookupOutcome.ProviderUnavailable;
        var saved = await scenario.AssertRetryAsync(outcome,
            scenario.Clock.GetUtcNow().UtcDateTime.AddMinutes(scenario.Config.RetryInitialDelayMinutes));
        var requests = scenario.Transport.RequestIds.ToArray();
        await scenario.ProcessAsync();
        Assert.That(scenario.Transport.RequestIds, Is.EqualTo(requests));
        Assert.That((await scenario.SavedAsync()).ImageLookup!.AttemptCount, Is.EqualTo(1));
        scenario.Clock.Now = new DateTimeOffset(saved.ImageLookup!.NextEligibleAt!.Value, TimeSpan.Zero);
        await scenario.AssertDueAsync(true);
        await scenario.ProcessAsync();
        await scenario.AssertImageAsync("30", GameArt, attempts: 2);
        Assert.That((await scenario.SavedAsync()).ImageLookup!.IsBacklogRecovery, Is.True);
        scenario.Verify(authentication ? ["auth", "auth", "search", "art"] : ["auth", "search", "search", "art"]);
    }

    private static string GameResults(bool artwork = true) => JsonSerializer.Serialize(new[]
    {
        new { id = 30, name = GameTitle, platforms = new[] { new { name = "Nintendo Switch", abbreviation = "Switch" } },
            cover = artwork ? new { url = "//images.igdb.com/igdb/image/upload/t_thumb/game.png" } : null }
    });

    private sealed class GameScenario : IDisposable
    {
        private readonly RegressionSuite _suite;
        private readonly string _host = $"igdb-{Guid.NewGuid():N}.invalid";
        private readonly string _accessToken = $"fixture-token-{Guid.NewGuid():N}";
        private readonly CancellationTokenSource _deadline = new(TimeSpan.FromSeconds(20));
        private readonly BackgroundImageLookupService _processor;
        private readonly Dictionary<string, Fixture> _requests = [];
        internal StrictHttpFixtures Transport { get; } = new(Progress);
        private ServiceProvider Services { get; }
        internal ScenarioTimeProvider Clock { get; } = new();
        internal BackgroundImageLookupConfiguration Config { get; } = new() { BatchSize = 1, RequestsPerMinute = int.MaxValue };
        private CancellationToken Token => _deadline.Token;

        internal GameScenario(RegressionSuite suite)
        {
            _suite = suite;
            var services = new ServiceCollection();
            services.AddLogging(builder => builder.AddProvider(new ProgressLoggerProvider()));
            services.AddMemoryCache(); // Owned by this scenario, including the IGDB token cache.
            services.AddSingleton<IDatabaseService>(suite._database);
            services.AddScoped<LookupStrategyFactory>();
            services.AddSingleton<IHttpClientFactory>(Transport);
            services.AddSingleton<IOptions<IgdbConfiguration>>(Options.Create(new IgdbConfiguration
            {
                ClientId = "fixture-client",
                ClientSecret = "fixture-secret",
                TokenUrl = $"https://{_host}/oauth2/token",
                BaseUrl = $"https://{_host}/v4/"
            }));
            services.AddSingleton<IIgdbTokenService, IgdbTokenService>();
            services.AddSingleton<IIgdbClient>(sp => new IgdbClient(
                new HttpClient(Transport, false) { BaseAddress = new Uri($"https://{_host}/v4/") },
                sp.GetRequiredService<IIgdbTokenService>(), sp.GetRequiredService<IOptions<IgdbConfiguration>>(),
                sp.GetRequiredService<ILogger<IgdbClient>>()));
            services.AddSingleton<IUpcItemDbClient>(sp => new UpcItemDbClient(
                new HttpClient(Transport, false) { BaseAddress = new Uri("https://api.upcitemdb.com/") },
                Options.Create(new UpcItemDbConfiguration()), sp.GetRequiredService<ILogger<UpcItemDbClient>>()));
            services.AddScoped<ILookupStrategy<GameResponse>, GameLookupStrategy>();
            services.AddSingleton<IImageStorageProvider>(sp => new LocalFileStorageProvider(suite._images, sp.GetRequiredService<ILogger<LocalFileStorageProvider>>()));
            services.AddSingleton<IImageService>(sp => new ImageService(sp.GetRequiredService<IImageStorageProvider>(),
                Options.Create(new ImageConfiguration()), new HttpClient(Transport, false), sp.GetRequiredService<ILogger<ImageService>>()));
            services.AddScoped<IImageLookupService, ImageLookupService>();
            Services = services.BuildServiceProvider();
            _processor = new BackgroundImageLookupService(Services.GetRequiredService<IServiceScopeFactory>(),
                Options.Create(Config), Services.GetRequiredService<ILogger<BackgroundImageLookupService>>(), Clock);
        }

        internal void Auth(int status = 200, string? failure = null) => Add("auth", "/oauth2/token",
            "client_id=fixture-client&client_secret=fixture-secret&grant_type=client_credentials",
            JsonSerializer.Serialize(new { access_token = _accessToken, expires_in = 3600, token_type = "bearer" }), status, failure);
        internal void Search(string text, int status = 200, string? failure = null) => Add("search", "/v4/games",
            $"fields id,name,summary,first_release_date,genres.name,involved_companies.company.name,involved_companies.developer,involved_companies.publisher,platforms.name,platforms.abbreviation,age_ratings.category,age_ratings.rating,cover.url,alternative_names.name; search \"{GameTitle}\"; limit 10;",
            text, status, failure);
        internal void Art(int status = 200) => Add("art", new Uri(GameArt).AbsolutePath, "", "", status: status, image: true);
        private void Add(string id, string path, string body, string text, int status = 200, string? failure = null, bool image = false)
        {
            var response = new FixtureResponse
            {
                Text = text,
                Status = status,
                Failure = failure,
                ContentType = image ? "image/png" : "application/json",
                Base64 = image ? Png : null
            };
            if (_requests.TryGetValue(path, out var existing))
            { existing.Responses.Add(response); }
            else
            {
                var fixture = new Fixture
                {
                    Id = id,
                    Method = image ? "GET" : "POST",
                    Host = image ? "images.igdb.com" : _host,
                    Path = path,
                    Body = body,
                    RequestHeaders = id == "search"
                        ? new() { ["Client-ID"] = "fixture-client", ["Authorization"] = $"Bearer {_accessToken}" } : [],
                    Responses = [response]
                };
                _requests.Add(path, fixture);
                Transport.Add(fixture);
            }
        }
        internal Task InsertAsync(Game game)
        {
            game.Id ??= GameId;
            return _suite._database.GetCollection<Game>().InsertOneAsync(game, cancellationToken: Token);
        }
        internal Task ProcessAsync() => _processor.ProcessAllEntityTypesAsync(Token);
        internal Task<Game> SavedAsync(string id = GameId) => _suite._database.GetCollection<Game>().Find(m => m.Id == id).SingleAsync(Token);
        internal async Task AssertDueAsync(bool expected, string id = GameId)
        {
            var due = await _suite._database.GetCollection<Game>().Find(ImageRetryPolicy.DueFilter<Game>(Clock.GetUtcNow().UtcDateTime)).ToListAsync(Token);
            Assert.That(due.Any(m => m.Id == id), Is.EqualTo(expected));
        }
        private void AssertState(Game saved, ImageLookupOutcome outcome, int attempts = 1)
        {
            Assert.That(saved.ImageLookup, Is.Not.Null);
            Assert.Multiple(() =>
            {
                Assert.That(saved.ImageLookup!.LastOutcome, Is.EqualTo(outcome));
                Assert.That(saved.ImageLookup.AttemptCount, Is.EqualTo(attempts));
                Assert.That(saved.ImageLookup.LookupAttemptedAt, Is.EqualTo(Clock.GetUtcNow().UtcDateTime));
                Assert.That(saved.ImageLookup.AttemptedStrategy, Is.EqualTo("Games:Entity"));
                Assert.That(saved.ImageLookup.MetadataFingerprint, Is.EqualTo(ImageRetryPolicy.Fingerprint(saved)));
            });
        }
        internal async Task AssertImageAsync(string providerId, string poster, int attempts = 1, string id = GameId, int fileCount = 1)
        {
            var saved = await SavedAsync(id);
            AssertState(saved, ImageLookupOutcome.Success, attempts);
            Assert.That(saved.CoverImage, Is.Not.Null);
            Assert.Multiple(() =>
            {
                Assert.That(saved.ImageLookup!.Provider, Is.EqualTo("Igdb"));
                Assert.That(saved.ImageLookup.ConfirmedMatch!.Provider, Is.EqualTo("Igdb"));
                Assert.That(saved.ImageLookup.ConfirmedMatch.ProviderId, Is.EqualTo(providerId));
                Assert.That(saved.ImageLookup.ConfirmedMatch.MetadataFingerprint, Is.EqualTo(ImageRetryPolicy.Fingerprint(saved)));
                Assert.That(saved.ImageLookup.ConfirmedMatch.ImageUrl, Is.EqualTo(poster));
                Assert.That(saved.ImageLookup.ConfirmedMatch.ArtworkScope, Is.EqualTo("Game artwork; region and package unverified"));
                Assert.That(saved.ImageLookup.PermanentFailure, Is.False);
                Assert.That(saved.ImageLookup.NextEligibleAt, Is.Null);
                Assert.That(saved.ImageLookup.FailureReason, Is.Null);
            });
            await using var stream = await Services.GetRequiredService<IImageService>().GetImageStreamAsync(saved.CoverImage!.FilePath, Token);
            using var bitmap = SKBitmap.Decode(stream);
            Assert.That(bitmap, Is.Not.Null);
            Assert.Multiple(() =>
            {
                Assert.That(bitmap.Width, Is.EqualTo(2));
                Assert.That(bitmap.Height, Is.EqualTo(2));
                Assert.That(File.Exists(Path.Combine(_suite._images, saved.CoverImage.FilePath)), Is.True);
                Assert.That(Directory.GetFiles(_suite._images, "*", SearchOption.AllDirectories), Has.Length.EqualTo(fileCount));
            });
            await AssertDueAsync(false, id);
        }
        internal async Task AssertTerminalAsync(ImageLookupOutcome outcome, string? providerId = null)
        {
            var saved = await SavedAsync();
            AssertState(saved, outcome);
            Assert.Multiple(() =>
            {
                Assert.That(saved.CoverImage, Is.Null);
                Assert.That(saved.ImageLookup!.PermanentFailure, Is.True);
                Assert.That(saved.ImageLookup.NextEligibleAt, Is.Null);
                Assert.That(saved.ImageLookup.FailureReason, Is.Not.Empty);
                Assert.That(saved.ImageLookup.ConfirmedMatch?.ProviderId, Is.EqualTo(providerId));
            });
            _suite.AssertNoFiles();
            await AssertDueAsync(false);
            var requests = Transport.RequestIds.ToArray();
            await ProcessAsync();
            Assert.That(Transport.RequestIds, Is.EqualTo(requests));
            Assert.That((await SavedAsync()).ImageLookup!.AttemptCount, Is.EqualTo(1));
        }
        internal async Task<Game> AssertRetryAsync(ImageLookupOutcome outcome, DateTime retryAt, string id = GameId)
        {
            var saved = await SavedAsync(id);
            AssertState(saved, outcome);
            Assert.Multiple(() =>
            {
                Assert.That(saved.CoverImage, Is.Null);
                Assert.That(saved.ImageLookup!.Provider, Is.EqualTo("Igdb"));
                Assert.That(saved.ImageLookup.PermanentFailure, Is.False);
                Assert.That(saved.ImageLookup.NextEligibleAt, Is.EqualTo(retryAt));
                Assert.That(saved.ImageLookup.FailureReason, Is.Not.Empty);
                Assert.That(saved.ImageLookup.ConfirmedMatch, Is.Null);
            });
            _suite.AssertNoFiles();
            await AssertDueAsync(false, id);
            return saved;
        }
        internal void Verify(params string[] ids)
        {
            Transport.VerifyComplete();
            Assert.That(Transport.RequestIds, Is.EqualTo(ids));
        }
        public void Dispose()
        {
            _processor.Dispose();
            Services.Dispose();
            Transport.Dispose();
            _deadline.Dispose();
        }
    }
}
