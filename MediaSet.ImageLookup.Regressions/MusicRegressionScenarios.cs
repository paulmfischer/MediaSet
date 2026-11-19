using System.Text.Json;
using MediaSet.Api.Features.Images.Models;
using MediaSet.Api.Features.Images.Services;
using MediaSet.Api.Infrastructure.Database;
using MediaSet.Api.Infrastructure.Lookup.Clients.MusicBrainz;
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
    private const string MusicId = "000000000000000000000040";
    private const string MusicArt = "https://coverartarchive.org/release/fixture-release/front";
    private const string MusicGroupArt = "https://coverartarchive.org/release-group/fixture-group/front";

    [TestCase(false)]
    [TestCase(true)]
    public async Task ProcessMusic_ReleaseArtworkOrGroupFallback_SavesReadableImageAsync(bool group)
    {
        using var scenario = new MusicScenario(this);
        scenario.Search();
        scenario.Detail(group);
        scenario.Art(group ? 404 : 200);
        if (group)
        { scenario.Art(group: true); }
        await scenario.InsertAsync(MusicEntity());
        await scenario.ProcessAsync();
        await scenario.AssertImageAsync("fixture-release", group ? MusicGroupArt : MusicArt, group: group);
        await scenario.ProcessAsync();
        Assert.That((await scenario.SavedAsync()).ImageLookup!.AttemptCount, Is.EqualTo(1));
        scenario.Verify(group ? ["search", "detail", "art", "group-art"] : ["search", "detail", "art"]);
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task ProcessMusic_NoMatchOrUnavailableArtwork_PersistsTerminalOutcomeAsync(bool matched)
    {
        using var scenario = new MusicScenario(this);
        scenario.Search(matched);
        if (matched)
        { scenario.Detail(group: true); scenario.Art(404); scenario.Art(404, group: true); }
        await scenario.InsertAsync(MusicEntity());
        await scenario.ProcessAsync();
        await scenario.AssertTerminalAsync(matched ? ImageLookupOutcome.MissingArtwork : ImageLookupOutcome.NoMatch,
            matched ? "fixture-release" : null);
        scenario.Verify(matched ? ["search", "detail", "art", "group-art"] : ["search"]);
    }

    [TestCase(false, "502")]
    [TestCase(false, "network")]
    [TestCase(false, "timeout")]
    [TestCase(true, "502")]
    [TestCase(true, "network")]
    [TestCase(true, "timeout")]
    public Task ProcessMusic_TemporaryProviderFailure_RetriesAndRecoversAsync(bool detail, string failure) => VerifyMusicRecoveryAsync(detail, failure);

    private async Task VerifyMusicRecoveryAsync(bool detail, string failure)
    {
        using var assertions = Assert.EnterMultipleScope();
        using var scenario = new MusicScenario(this);
        var status = int.TryParse(failure, out var code) ? code : 200;
        var transportFailure = status == 200 ? failure : null;
        if (detail)
        { scenario.Search(); scenario.Detail(status: status, failure: transportFailure); }
        else
        { scenario.Search(status: status, failure: transportFailure); }
        scenario.Search();
        scenario.Detail();
        scenario.Art();
        await scenario.InsertAsync(MusicEntity());
        await scenario.ProcessAsync();
        var saved = await scenario.AssertRetryAsync(ImageLookupOutcome.ProviderUnavailable,
            scenario.Clock.GetUtcNow().UtcDateTime.AddMinutes(scenario.Config.RetryInitialDelayMinutes));
        var requests = scenario.Transport.RequestIds.ToArray();
        await scenario.ProcessAsync();
        Assert.That(scenario.Transport.RequestIds, Is.EqualTo(requests));
        Assert.That((await scenario.SavedAsync()).ImageLookup!.AttemptCount, Is.EqualTo(1));
        scenario.Clock.Now = new DateTimeOffset(saved.ImageLookup!.NextEligibleAt!.Value, TimeSpan.Zero);
        await scenario.AssertDueAsync(true);
        await scenario.ProcessAsync();
        await scenario.AssertImageAsync("fixture-release", MusicArt, attempts: 2);
        Assert.That((await scenario.SavedAsync()).ImageLookup!.IsBacklogRecovery, Is.True);
        scenario.Verify(detail ? ["search", "detail", "search", "detail", "art"] : ["search", "search", "detail", "art"]);
    }

    [TestCase("oversized")]
    [TestCase("corrupt")]
    [TestCase("non-image")]
    [TestCase("pixels")]
    public Task ProcessMusic_InvalidArtwork_RejectsBytesAndPersistsInvalidOutcomeAsync(string invalid) => VerifyInvalidMusicArtworkAsync(invalid);

    private async Task VerifyInvalidMusicArtworkAsync(string invalid)
    {
        using var assertions = Assert.EnterMultipleScope();
        var config = new ImageConfiguration { MaxFileSizeMb = 1, MaxPixelCount = invalid == "pixels" ? 3 : 25_000_000 };
        var bytes = invalid switch
        {
            "oversized" => new byte[(1024 * 1024) + 1],
            "corrupt" => Convert.FromBase64String(Png)[..40],
            "non-image" => System.Text.Encoding.UTF8.GetBytes("<html>not an image</html>"),
            _ => Convert.FromBase64String(Png)
        };
        var type = invalid == "non-image" ? "text/html" : "image/png";
        using var scenario = new MusicScenario(this, config);
        // Probe the same real image pipeline first so each fixture proves the intended
        // validation guard, independently of the persisted-outcome product assertion.
        scenario.Art(data: Convert.ToBase64String(bytes), contentType: type);
        scenario.Art(data: Convert.ToBase64String(bytes), contentType: type);
        var reason = invalid switch
        {
            "oversized" => ImageFailureReason.TooLarge,
            "pixels" => ImageFailureReason.PixelLimit,
            _ => ImageFailureReason.UnsupportedFormat
        };
        await scenario.AssertRejectedAsync(reason);
        scenario.Search();
        scenario.Detail();
        await scenario.InsertAsync(MusicEntity());
        await scenario.ProcessAsync();
        await scenario.AssertTerminalAsync(ImageLookupOutcome.InvalidArtwork, "fixture-release");
        scenario.Verify("art", "search", "detail", "art");
    }

    private static Music MusicEntity() => new() { Title = "Fixture Album", Artist = "Fixture Artist" };

    private sealed class MusicScenario : IDisposable
    {
        private readonly RegressionSuite _suite;
        private readonly string _host = $"musicbrainz-{Guid.NewGuid():N}.invalid";
        private readonly CancellationTokenSource _deadline = new(TimeSpan.FromSeconds(20));
        private readonly BackgroundImageLookupService _processor;
        private readonly Dictionary<string, Fixture> _requests = [];
        internal StrictHttpFixtures Transport { get; } = new(Progress);
        private ServiceProvider Services { get; }
        internal ScenarioTimeProvider Clock { get; } = new();
        internal BackgroundImageLookupConfiguration Config { get; } = new() { BatchSize = 1, RequestsPerMinute = int.MaxValue };
        private CancellationToken Token => _deadline.Token;

        internal MusicScenario(RegressionSuite suite, ImageConfiguration? imageConfig = null)
        {
            _suite = suite;
            var services = new ServiceCollection();
            services.AddLogging(builder => builder.AddProvider(new ProgressLoggerProvider()));
            services.AddSingleton<IDatabaseService>(suite._database);
            services.AddScoped<LookupStrategyFactory>();
            services.AddSingleton<IMusicBrainzClient>(sp => new MusicBrainzClient(
                new HttpClient(Transport, false) { BaseAddress = new Uri($"https://{_host}/") },
                sp.GetRequiredService<ILogger<MusicBrainzClient>>()));
            services.AddScoped<ILookupStrategy<MusicResponse>, MusicLookupStrategy>();
            services.AddSingleton<IImageStorageProvider>(sp => new LocalFileStorageProvider(suite._images, sp.GetRequiredService<ILogger<LocalFileStorageProvider>>()));
            services.AddSingleton<IImageService>(sp => new ImageService(sp.GetRequiredService<IImageStorageProvider>(),
                Options.Create(imageConfig ?? new ImageConfiguration()), new HttpClient(Transport, false), sp.GetRequiredService<ILogger<ImageService>>()));
            services.AddScoped<IImageLookupService, ImageLookupService>();
            Services = services.BuildServiceProvider();
            _processor = new BackgroundImageLookupService(Services.GetRequiredService<IServiceScopeFactory>(),
                Options.Create(Config), Services.GetRequiredService<ILogger<BackgroundImageLookupService>>(), Clock);
        }

        internal void Search(bool matched = true, int status = 200, string? failure = null) => Add("search",
            $"https://{_host}/ws/2/release/?query=release:%22Fixture%20Album%22%20AND%20artist:%22Fixture%20Artist%22&limit=10&fmt=json",
            new FixtureResponse
            {
                Status = status,
                Failure = failure,
                Text = matched ? "{\"releases\":[{\"id\":\"fixture-release\",\"title\":\"Fixture Album\"}]}" : "{\"releases\":[]}"
            });
        internal void Detail(bool group = false, int status = 200, string? failure = null) => Add("detail",
            $"https://{_host}/ws/2/release/fixture-release?inc=artist-credits+labels+recordings+tags+release-groups&fmt=json",
            new FixtureResponse
            {
                Status = status,
                Failure = failure,
                Text = JsonSerializer.Serialize(new Dictionary<string, object?>
                {
                    ["id"] = "fixture-release",
                    ["title"] = "Fixture Album",
                    ["artist-credit"] = new[] { new { name = "Fixture Artist" } },
                    ["release-group"] = group ? new { id = "fixture-group" } : null
                })
            });
        internal void Art(int status = 200, string? data = null, string contentType = "image/png", bool group = false) => Add(
            group ? "group-art" : "art", group ? MusicGroupArt : MusicArt,
            new FixtureResponse { Status = status, Base64 = data ?? Png, ContentType = contentType });
        private void Add(string id, string url, FixtureResponse response)
        {
            var uri = new Uri(url);
            if (_requests.TryGetValue(url, out var existing))
            { existing.Responses.Add(response); }
            else
            {
                var fixture = new Fixture { Id = id, Host = uri.Authority, Path = uri.AbsolutePath, Query = uri.Query, Responses = [response] };
                _requests.Add(url, fixture);
                Transport.Add(fixture);
            }
        }
        internal async Task AssertRejectedAsync(ImageFailureReason reason)
        {
            var exception = await Assert.ThrowsAsync<ArgumentException>(async () =>
                await Services.GetRequiredService<IImageService>().DownloadAndSaveImageAsync(
                    MusicArt, "Musics", "validation-probe", Token));
            Assert.That(exception, Is.Not.Null);
            Assert.That(ImageFailure.GetReason(exception!), Is.EqualTo(reason));
            _suite.AssertNoFiles();
            Assert.That(await _suite._database.GetCollection<Music>().CountDocumentsAsync(
                Builders<Music>.Filter.Empty, cancellationToken: Token), Is.Zero);
        }
        internal Task InsertAsync(Music music)
        {
            music.Id ??= MusicId;
            return _suite._database.GetCollection<Music>().InsertOneAsync(music, cancellationToken: Token);
        }
        internal Task ProcessAsync() => _processor.ProcessAllEntityTypesAsync(Token);
        internal Task<Music> SavedAsync(string id = MusicId) => _suite._database.GetCollection<Music>().Find(m => m.Id == id).SingleAsync(Token);
        internal async Task AssertDueAsync(bool expected, string id = MusicId)
        {
            var due = await _suite._database.GetCollection<Music>().Find(ImageRetryPolicy.DueFilter<Music>(Clock.GetUtcNow().UtcDateTime)).ToListAsync(Token);
            Assert.That(due.Any(m => m.Id == id), Is.EqualTo(expected));
        }
        private void AssertState(Music saved, ImageLookupOutcome outcome, int attempts = 1)
        {
            Assert.That(saved.ImageLookup, Is.Not.Null);
            Assert.Multiple(() =>
            {
                Assert.That(saved.ImageLookup!.LastOutcome, Is.EqualTo(outcome));
                Assert.That(saved.ImageLookup.AttemptCount, Is.EqualTo(attempts));
                Assert.That(saved.ImageLookup.LookupAttemptedAt, Is.EqualTo(Clock.GetUtcNow().UtcDateTime));
                Assert.That(saved.ImageLookup.AttemptedStrategy, Is.EqualTo("Musics:Entity"));
                Assert.That(saved.ImageLookup.MetadataFingerprint, Is.EqualTo(ImageRetryPolicy.Fingerprint(saved)));
            });
        }
        internal async Task AssertImageAsync(string providerId, string poster, int attempts = 1, string id = MusicId, int fileCount = 1, bool group = false)
        {
            var saved = await SavedAsync(id);
            AssertState(saved, ImageLookupOutcome.Success, attempts);
            Assert.That(saved.CoverImage, Is.Not.Null);
            Assert.Multiple(() =>
            {
                Assert.That(saved.ImageLookup!.Provider, Is.EqualTo("MusicBrainz"));
                Assert.That(saved.ImageLookup.ConfirmedMatch!.Provider, Is.EqualTo("MusicBrainz"));
                Assert.That(saved.ImageLookup.ConfirmedMatch.ProviderId, Is.EqualTo(providerId));
                Assert.That(saved.ImageLookup.ConfirmedMatch.MetadataFingerprint, Is.EqualTo(ImageRetryPolicy.Fingerprint(saved)));
                Assert.That(saved.ImageLookup.ConfirmedMatch.ImageUrl, Is.EqualTo(poster));
                Assert.That(saved.ImageLookup.ConfirmedMatch.ArtworkScope, Is.EqualTo(group ? "Release-group artwork; the edition's own cover is missing" : "Provider artwork"));
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
                Assert.That(saved.ImageLookup.Provider, Is.EqualTo("MusicBrainz"));
                Assert.That(saved.ImageLookup.ConfirmedMatch?.ProviderId, Is.EqualTo(providerId));
                if (providerId != null)
                {
                    Assert.That(saved.ImageLookup.ConfirmedMatch!.Provider, Is.EqualTo("MusicBrainz"));
                    Assert.That(saved.ImageLookup.ConfirmedMatch.ImageUrl, Is.EqualTo(MusicArt));
                    Assert.That(saved.ImageLookup.ConfirmedMatch.MetadataFingerprint, Is.EqualTo(ImageRetryPolicy.Fingerprint(saved)));
                }
            });
            _suite.AssertNoFiles();
            await AssertDueAsync(false);
            var requests = Transport.RequestIds.ToArray();
            await ProcessAsync();
            Assert.That(Transport.RequestIds, Is.EqualTo(requests));
            Assert.That((await SavedAsync()).ImageLookup!.AttemptCount, Is.EqualTo(1));
        }
        internal async Task<Music> AssertRetryAsync(ImageLookupOutcome outcome, DateTime retryAt, string id = MusicId)
        {
            var saved = await SavedAsync(id);
            AssertState(saved, outcome);
            Assert.Multiple(() =>
            {
                Assert.That(saved.CoverImage, Is.Null);
                Assert.That(saved.ImageLookup!.Provider, Is.EqualTo("MusicBrainz"));
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
