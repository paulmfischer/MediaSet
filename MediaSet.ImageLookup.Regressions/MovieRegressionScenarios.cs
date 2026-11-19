using System.Net.Http.Headers;
using System.Text.Json;
using System.Web;
using MediaSet.Api.Features.Images.Models;
using MediaSet.Api.Features.Images.Services;
using MediaSet.Api.Infrastructure.Database;
using MediaSet.Api.Infrastructure.Lookup.Clients.Tmdb;
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
    private const string MovieId = "000000000000000000000020";
    private const string MovieTitle = "Fixture Film";
    private const string SeriesTitle = "Fixture Show";

    [Test]
    public async Task ProcessMovie_RemakesAndUnrelatedCandidates_StoresOnlyMatchingYearAsync()
    {
        using var scenario = new MovieScenario(this);
        scenario.Search("movie-search", "movie", MovieTitle, MovieResults((20, MovieTitle), (21, MovieTitle), (22, "Other Film")));
        scenario.Movie("original", 20, MovieTitle, "1980-01-01", "/original.png");
        scenario.Movie("remake", 21, MovieTitle, "2020-01-01", "/remake.png");
        scenario.Search("tv-search", "tv", MovieTitle, "{\"results\":[]}");
        scenario.Art("remake-art", "/remake.png");
        await scenario.InsertAsync(new Movie { Title = MovieTitle, ReleaseDate = "2020" });

        await scenario.ProcessAsync();

        await scenario.AssertImageAsync("21", "/remake.png");
        scenario.Verify("movie-search", "original", "remake", "tv-search", "remake-art");
        await scenario.ProcessAsync();
        Assert.That((await scenario.SavedAsync()).ImageLookup!.AttemptCount, Is.EqualTo(1));
        scenario.Verify("movie-search", "original", "remake", "tv-search", "remake-art");
    }

    [Test]
    public async Task ProcessMovie_EquallyMatchingRemakesWithOnlyOnePoster_PersistsAmbiguityWithoutDownloadAsync()
    {
        using var scenario = new MovieScenario(this);
        scenario.Search("movie-search", "movie", MovieTitle, MovieResults((20, MovieTitle), (21, MovieTitle)));
        scenario.Movie("original", 20, MovieTitle, "1980-01-01", null);
        scenario.Movie("remake", 21, MovieTitle, "2020-01-01", "/remake.png");
        scenario.Search("tv-search", "tv", MovieTitle, "{\"results\":[]}");
        await scenario.InsertAsync(new Movie { Title = MovieTitle });

        await scenario.ProcessAsync();

        await scenario.AssertTerminalAsync(ImageLookupOutcome.AmbiguousMatch);
        scenario.Verify("movie-search", "original", "remake", "tv-search");
    }

    [Test]
    public async Task ProcessMovie_UnverifiedDetailTitle_PersistsNoMatchWithoutDownloadingAsync()
    {
        using var scenario = new MovieScenario(this);
        scenario.Search("movie-search", "movie", MovieTitle, MovieResults((20, MovieTitle)));
        scenario.Movie("wrong-detail", 20, "Another Film", "2020-01-01", "/wrong.png");
        scenario.Search("tv-search", "tv", MovieTitle, "{\"results\":[]}");
        await scenario.InsertAsync(new Movie { Title = MovieTitle });

        await scenario.ProcessAsync();

        await scenario.AssertTerminalAsync(ImageLookupOutcome.NoMatch);
        scenario.Verify("movie-search", "wrong-detail", "tv-search");
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task ProcessMovie_CollectionTitle_VerifiesPackageIdentityAndNeverUsesFilmPosterAsync(bool wrongCount)
    {
        using var scenario = new MovieScenario(this);
        const string title = "Fixture Trilogy";
        scenario.Search("collection-search", "collection", "Fixture", "{\"results\":[{\"id\":30,\"name\":\"Fixture Collection\"},{\"id\":31,\"name\":\"Other Collection\"}]}");
        scenario.Add("collection-detail", "collection/30", JsonSerializer.Serialize(new
        {
            id = 30,
            name = "Fixture Collection",
            poster_path = "/collection.png",
            parts = Enumerable.Range(1, wrongCount ? 2 : 3).Select(id => new { id, title = $"Fixture {id}" }).ToArray()
        }));
        if (!wrongCount)
        { scenario.Art("collection-art", "/collection.png"); }
        await scenario.InsertAsync(new Movie { Title = title });

        await scenario.ProcessAsync();

        if (wrongCount)
        { await scenario.AssertTerminalAsync(ImageLookupOutcome.NoMatch); }
        else
        { await scenario.AssertImageAsync("collection:30", "/collection.png"); }
        scenario.Verify(wrongCount ? ["collection-search", "collection-detail"] : ["collection-search", "collection-detail", "collection-art"]);
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task ProcessTv_VerifiedSeason_UsesSeasonPosterOrSeriesArtworkFallbackAsync(bool missingSeasonPoster)
    {
        using var scenario = new MovieScenario(this);
        scenario.TvSearchAndDetails();
        scenario.Season(missingSeasonPoster ? null : "/season.png");
        var poster = missingSeasonPoster ? "/series.png" : "/season.png";
        scenario.Art("season-art", poster);
        await scenario.InsertAsync(new Movie { Title = $"{SeriesTitle}: Season 2", IsTvSeries = true, ReleaseDate = "2021" });

        await scenario.ProcessAsync();

        await scenario.AssertImageAsync("tv:40:2", poster);
        scenario.Verify("tv-search", "series-detail", "season-detail", "season-art");
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task ProcessTv_MissingSeasonResource_OnlySplitSeasonMayUseVerifiedSeriesAsync(bool splitSeason)
    {
        using var scenario = new MovieScenario(this);
        scenario.TvSearchAndDetails();
        scenario.Add("season-missing", "tv/40/season/2", "{}", status: 404);
        if (splitSeason)
        { scenario.Art("series-art", "/series.png"); }
        await scenario.InsertAsync(new Movie { Title = $"{SeriesTitle}: Season {(splitSeason ? "2.5" : "2")}", IsTvSeries = true });

        await scenario.ProcessAsync();

        if (splitSeason)
        { await scenario.AssertImageAsync("tv:40", "/series.png"); }
        else
        { await scenario.AssertTerminalAsync(ImageLookupOutcome.NoMatch); }
        scenario.Verify(splitSeason ? ["tv-search", "series-detail", "season-missing", "series-art"] : ["tv-search", "series-detail", "season-missing"]);
    }

    [Test]
    public async Task ProcessTv_EquallyMatchingSeries_PersistsAmbiguityWithoutDownloadAsync()
    {
        using var scenario = new MovieScenario(this);
        scenario.Search("tv-search", "tv", SeriesTitle, "{\"results\":[{\"id\":40,\"name\":\"Fixture Show\"},{\"id\":41,\"name\":\"Fixture Show\"}]}");
        scenario.Series(40, "/series.png");
        scenario.Series(41, null);
        await scenario.InsertAsync(new Movie { Title = SeriesTitle, IsTvSeries = true });

        await scenario.ProcessAsync();

        await scenario.AssertTerminalAsync(ImageLookupOutcome.AmbiguousMatch);
        scenario.Verify("tv-search", "series-detail", "series-detail");
    }

    [Test]
    public async Task ProcessMovie_ExistingArtworkMissing_FallsBackThroughRealTmdbLookupAsync()
    {
        using var scenario = new MovieScenario(this);
        scenario.Art("old-art-missing", "/old.png", status: 404);
        scenario.SuccessfulMovie();
        await scenario.InsertAsync(new Movie { Title = MovieTitle, ImageUrl = "https://image.tmdb.org/t/p/w500/old.png" });

        await scenario.ProcessAsync();

        await scenario.AssertImageAsync("20", "/movie.png");
        scenario.Verify("old-art-missing", "movie-search", "movie-detail", "tv-search", "movie-art");
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task ProcessVideo_VerifiedIdentityWithoutUsableArtwork_PersistsTerminalMissingArtworkAsync(bool tv)
    {
        using var scenario = new MovieScenario(this);
        if (tv)
        {
            scenario.TvSearchAndDetails(poster: null);
            scenario.Season(null);
        }
        else
        {
            scenario.SuccessfulMovie(artStatus: 404);
        }
        await scenario.InsertAsync(new Movie { Title = tv ? $"{SeriesTitle}: Season 2" : MovieTitle, IsTvSeries = tv });

        await scenario.ProcessAsync();

        await scenario.AssertTerminalAsync(ImageLookupOutcome.MissingArtwork, tv ? "tv:40:2" : "20");
        scenario.Verify(tv ? ["tv-search", "series-detail", "season-detail"] : ["movie-search", "movie-detail", "tv-search", "movie-art"]);
    }

    [Test]
    public async Task ProcessMovie_InvalidPosterBytes_PersistsInvalidArtworkWithoutSavingFileAsync()
    {
        using var assertions = Assert.EnterMultipleScope();
        using var scenario = new MovieScenario(this);
        scenario.SuccessfulMovie(invalidArt: true);
        await scenario.InsertAsync(new Movie { Title = MovieTitle });

        await scenario.ProcessAsync();

        await scenario.AssertTerminalAsync(ImageLookupOutcome.InvalidArtwork, "20");
        scenario.Verify("movie-search", "movie-detail", "tv-search", "movie-art");
    }

    [TestCase(false, "503")]
    [TestCase(false, "network")]
    [TestCase(false, "timeout")]
    [TestCase(true, "503")]
    [TestCase(true, "network")]
    [TestCase(true, "timeout")]
    public async Task ProcessVideo_TemporaryProviderFailure_RetriesWhenDueAndRecoversAsync(bool tv, string failure)
    {
        using var scenario = new MovieScenario(this);
        scenario.Search("provider-failure", tv ? "tv" : "movie", tv ? SeriesTitle : MovieTitle, "",
            status: failure == "503" ? 503 : 200, failure: failure == "503" ? null : failure);
        // HTTP status/timeout returns an empty film result; network errors throw before TV search.
        if (!tv && failure != "network")
        { scenario.Search("empty-tv", "tv", MovieTitle, "{\"results\":[]}"); }
        if (tv)
        {
            scenario.TvSearchAndDetails();
            scenario.Art("recovered-art", "/series.png");
        }
        else
        { scenario.SuccessfulMovie(); }
        await scenario.InsertAsync(new Movie { Title = tv ? SeriesTitle : MovieTitle, IsTvSeries = tv });

        await scenario.ProcessAsync();

        var first = await scenario.AssertRetryAsync(ImageLookupOutcome.ProviderUnavailable,
            scenario.Clock.GetUtcNow().UtcDateTime.AddMinutes(scenario.Config.RetryInitialDelayMinutes));
        var requests = scenario.Transport.RequestIds.ToArray();
        await scenario.ProcessAsync();
        Assert.That(scenario.Transport.RequestIds, Is.EqualTo(requests));
        Assert.That((await scenario.SavedAsync()).ImageLookup!.AttemptCount, Is.EqualTo(1));
        scenario.Clock.Now = new DateTimeOffset(first.ImageLookup!.NextEligibleAt!.Value, TimeSpan.Zero);
        await scenario.AssertDueAsync(true);

        await scenario.ProcessAsync();

        await scenario.AssertImageAsync(tv ? "tv:40" : "20", tv ? "/series.png" : "/movie.png", attempts: 2);
        Assert.That((await scenario.SavedAsync()).ImageLookup!.IsBacklogRecovery, Is.True);
        scenario.Verify(tv ? ["provider-failure", "provider-failure", "series-detail", "recovered-art"]
            : failure == "network" ? ["provider-failure", "provider-failure", "movie-detail", "tv-search", "movie-art"]
            : ["provider-failure", "empty-tv", "provider-failure", "movie-detail", "empty-tv", "movie-art"]);
    }

    [TestCase(false, 401)]
    [TestCase(false, 403)]
    [TestCase(true, 401)]
    [TestCase(true, 403)]
    public async Task ProcessVideo_RejectedSyntheticAuthentication_PersistsUnauthorizedAsync(bool tv, int status)
    {
        using var scenario = new MovieScenario(this);
        scenario.Search("unauthorized", tv ? "tv" : "movie", tv ? SeriesTitle : MovieTitle, "{}", status: status);
        if (!tv)
        { scenario.Search("tv-search", "tv", MovieTitle, "{\"results\":[]}"); }
        await scenario.InsertAsync(new Movie { Title = tv ? SeriesTitle : MovieTitle, IsTvSeries = tv });

        await scenario.ProcessAsync();

        await scenario.AssertRetryAsync(ImageLookupOutcome.Unauthorized,
            scenario.Clock.GetUtcNow().UtcDateTime.AddMinutes(scenario.Config.RetryInitialDelayMinutes));
        scenario.Verify(tv ? ["unauthorized"] : ["unauthorized", "tv-search"]);
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task ProcessVideo_ProviderQuota_PersistsResetAndDefersFreshEntityWithoutHttpAsync(bool tv)
    {
        using var scenario = new MovieScenario(this);
        var reset = new DateTimeOffset(DateTime.UtcNow.AddHours(1).Ticks / TimeSpan.TicksPerSecond * TimeSpan.TicksPerSecond, TimeSpan.Zero);
        scenario.Clock.Now = reset.AddHours(-1);
        scenario.Search("quota", tv ? "tv" : "movie", tv ? SeriesTitle : MovieTitle, "{}", status: 429, reset: reset);
        await scenario.InsertAsync(new Movie { Title = tv ? SeriesTitle : MovieTitle, IsTvSeries = tv });

        await scenario.ProcessAsync();

        await scenario.AssertRetryAsync(ImageLookupOutcome.QuotaLimited, reset.UtcDateTime);
        await scenario.ProcessAsync();
        Assert.That((await scenario.SavedAsync()).ImageLookup!.AttemptCount, Is.EqualTo(1));
        const string secondId = "000000000000000000000021";
        await scenario.InsertAsync(new Movie { Id = secondId, Title = tv ? SeriesTitle : MovieTitle, IsTvSeries = tv });
        await scenario.ProcessAsync();
        await scenario.AssertRetryAsync(ImageLookupOutcome.QuotaLimited, reset.UtcDateTime, secondId);
        scenario.Clock.Now = reset;
        await scenario.AssertDueAsync(true);
        scenario.Verify("quota");
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task ProcessVideo_PartialDetailFailure_DoesNotAcceptSurvivingCandidateAsync(bool tv)
    {
        using var scenario = new MovieScenario(this);
        if (tv)
        {
            scenario.Search("tv-search", "tv", SeriesTitle, "{\"results\":[{\"id\":40,\"name\":\"Fixture Show\"},{\"id\":41,\"name\":\"Fixture Show\"}]}");
            scenario.Series(40, "/series.png");
            scenario.Add("detail-unavailable", "tv/41", "{}", status: 503);
        }
        else
        {
            scenario.Search("movie-search", "movie", MovieTitle, MovieResults((20, MovieTitle), (21, MovieTitle)));
            scenario.Movie("movie-detail", 20, MovieTitle, "2020-01-01", "/movie.png");
            scenario.Add("detail-unavailable", "movie/21", "{}", status: 503);
            scenario.Search("tv-search", "tv", MovieTitle, "{\"results\":[]}");
        }
        await scenario.InsertAsync(new Movie { Title = tv ? SeriesTitle : MovieTitle, IsTvSeries = tv });

        await scenario.ProcessAsync();

        await scenario.AssertRetryAsync(ImageLookupOutcome.ProviderUnavailable,
            scenario.Clock.GetUtcNow().UtcDateTime.AddMinutes(scenario.Config.RetryInitialDelayMinutes));
        scenario.Verify(tv ? ["tv-search", "series-detail", "detail-unavailable"] : ["movie-search", "movie-detail", "detail-unavailable", "tv-search"]);
    }

    private static string MovieResults(params (int Id, string Title)[] results) =>
        JsonSerializer.Serialize(new { results = results.Select(r => new { id = r.Id, title = r.Title }).ToArray() });

    private sealed class MovieScenario : IDisposable
    {
        private readonly RegressionSuite _suite;
        private readonly string _host = $"tmdb-{Guid.NewGuid():N}.invalid";
        private readonly CancellationTokenSource _deadline = new(TimeSpan.FromSeconds(20));
        private readonly BackgroundImageLookupService _processor;
        private readonly Dictionary<string, Fixture> _requests = [];
        internal StrictHttpFixtures Transport { get; } = new(Progress);
        private ServiceProvider Services { get; }
        internal ScenarioTimeProvider Clock { get; } = new();
        internal BackgroundImageLookupConfiguration Config { get; } = new() { BatchSize = 1, RequestsPerMinute = int.MaxValue };
        private CancellationToken Token => _deadline.Token;

        internal MovieScenario(RegressionSuite suite)
        {
            _suite = suite;
            var services = new ServiceCollection();
            services.AddLogging(builder => builder.AddProvider(new ProgressLoggerProvider()));
            services.AddSingleton<IDatabaseService>(suite._database);
            services.AddScoped<LookupStrategyFactory>();
            services.AddSingleton<IUpcItemDbClient>(sp => new UpcItemDbClient(
                new HttpClient(Transport, false) { BaseAddress = new Uri("https://api.upcitemdb.com/") },
                Options.Create(new UpcItemDbConfiguration()), sp.GetRequiredService<ILogger<UpcItemDbClient>>()));
            services.AddSingleton<ITmdbClient>(sp =>
            {
                var client = new HttpClient(Transport, false) { BaseAddress = new Uri($"https://{_host}/3/") };
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "fixture-token");
                return new TmdbClient(client, Options.Create(new TmdbConfiguration()), sp.GetRequiredService<ILogger<TmdbClient>>());
            });
            services.AddScoped<ILookupStrategy<MovieResponse>, MovieLookupStrategy>();
            services.AddSingleton<IImageStorageProvider>(sp => new LocalFileStorageProvider(suite._images, sp.GetRequiredService<ILogger<LocalFileStorageProvider>>()));
            services.AddSingleton<IImageService>(sp => new ImageService(sp.GetRequiredService<IImageStorageProvider>(),
                Options.Create(new ImageConfiguration()), new HttpClient(Transport, false), sp.GetRequiredService<ILogger<ImageService>>()));
            services.AddScoped<IImageLookupService, ImageLookupService>();
            Services = services.BuildServiceProvider();
            _processor = new BackgroundImageLookupService(Services.GetRequiredService<IServiceScopeFactory>(),
                Options.Create(Config), Services.GetRequiredService<ILogger<BackgroundImageLookupService>>(), Clock);
        }

        internal void Search(string id, string catalog, string title, string text, int status = 200, string? failure = null, DateTimeOffset? reset = null) =>
            Add(id, $"search/{catalog}?query={HttpUtility.UrlEncode(title)}", text, status, failure, reset);
        internal void Movie(string id, int movieId, string title, string date, string? poster) => Add(id, $"movie/{movieId}", JsonSerializer.Serialize(new
        { id = movieId, title, release_date = date, poster_path = poster, genres = Array.Empty<object>(), production_companies = Array.Empty<object>() }));
        internal void Series(int seriesId = 40, string? poster = "/series.png") => Add("series-detail", $"tv/{seriesId}", JsonSerializer.Serialize(new
        { id = seriesId, name = SeriesTitle, first_air_date = "2020-01-01", poster_path = poster }));
        internal void TvSearchAndDetails(string? poster = "/series.png")
        {
            Search("tv-search", "tv", SeriesTitle, "{\"results\":[{\"id\":40,\"name\":\"Fixture Show\"}]}");
            Series(poster: poster);
        }
        internal void Season(string? poster) => Add("season-detail", "tv/40/season/2", JsonSerializer.Serialize(new
        { season_number = 2, air_date = "2021-01-01", poster_path = poster }));
        internal void SuccessfulMovie(int artStatus = 200, bool invalidArt = false)
        {
            Search("movie-search", "movie", MovieTitle, MovieResults((20, MovieTitle)));
            Movie("movie-detail", 20, MovieTitle, "2020-01-01", "/movie.png");
            Search("tv-search", "tv", MovieTitle, "{\"results\":[]}");
            Art("movie-art", "/movie.png", artStatus, invalidArt);
        }
        internal void Art(string id, string path, int status = 200, bool invalid = false) =>
            Add(id, $"https://image.tmdb.org/t/p/w500{path}", invalid ? "not an image" : "", status, image: !invalid);
        internal void Add(string id, string path, string text, int status = 200, string? failure = null, DateTimeOffset? reset = null, bool image = false)
        {
            var uri = new Uri(path.StartsWith("https://", StringComparison.Ordinal) ? path : $"https://{_host}/3/{path}");
            var response = new FixtureResponse
            {
                Status = status,
                Text = text,
                Failure = failure,
                ContentType = image ? "image/png" : "application/json",
                Base64 = image && status == 200 ? Png : null,
                Headers = reset == null ? [] : new() { ["Retry-After"] = reset.Value.ToString("R") }
            };
            if (_requests.TryGetValue(uri.AbsoluteUri, out var existing))
            { existing.Responses.Add(response); }
            else
            {
                var fixture = new Fixture
                {
                    Id = id,
                    Host = uri.Authority,
                    Path = uri.AbsolutePath,
                    Query = uri.Query,
                    RequestHeaders = uri.Host == _host ? new() { ["Authorization"] = "Bearer fixture-token" } : [],
                    Responses = [response]
                };
                _requests.Add(uri.AbsoluteUri, fixture);
                Transport.Add(fixture);
            }
        }
        internal Task InsertAsync(Movie movie)
        {
            movie.Id ??= MovieId;
            return _suite._database.GetCollection<Movie>().InsertOneAsync(movie, cancellationToken: Token);
        }
        internal Task ProcessAsync() => _processor.ProcessAllEntityTypesAsync(Token);
        internal Task<Movie> SavedAsync(string id = MovieId) => _suite._database.GetCollection<Movie>().Find(m => m.Id == id).SingleAsync(Token);
        internal async Task AssertDueAsync(bool expected, string id = MovieId)
        {
            var due = await _suite._database.GetCollection<Movie>().Find(ImageRetryPolicy.DueFilter<Movie>(Clock.GetUtcNow().UtcDateTime)).ToListAsync(Token);
            Assert.That(due.Any(m => m.Id == id), Is.EqualTo(expected));
        }
        private void AssertState(Movie saved, ImageLookupOutcome outcome, int attempts = 1)
        {
            Assert.That(saved.ImageLookup, Is.Not.Null);
            Assert.Multiple(() =>
            {
                Assert.That(saved.ImageLookup!.LastOutcome, Is.EqualTo(outcome));
                Assert.That(saved.ImageLookup.AttemptCount, Is.EqualTo(attempts));
                Assert.That(saved.ImageLookup.LookupAttemptedAt, Is.EqualTo(Clock.GetUtcNow().UtcDateTime));
                Assert.That(saved.ImageLookup.AttemptedStrategy, Is.EqualTo("Movies:Entity"));
                Assert.That(saved.ImageLookup.MetadataFingerprint, Is.EqualTo(ImageRetryPolicy.Fingerprint(saved)));
            });
        }
        internal async Task AssertImageAsync(string providerId, string poster, int attempts = 1)
        {
            var saved = await SavedAsync();
            AssertState(saved, ImageLookupOutcome.Success, attempts);
            Assert.That(saved.CoverImage, Is.Not.Null);
            Assert.Multiple(() =>
            {
                Assert.That(saved.ImageLookup!.Provider, Is.EqualTo("Tmdb"));
                Assert.That(saved.ImageLookup.ConfirmedMatch!.Provider, Is.EqualTo("Tmdb"));
                Assert.That(saved.ImageLookup.ConfirmedMatch.ProviderId, Is.EqualTo(providerId));
                Assert.That(saved.ImageLookup.ConfirmedMatch.MetadataFingerprint, Is.EqualTo(ImageRetryPolicy.Fingerprint(saved)));
                Assert.That(saved.ImageLookup.ConfirmedMatch.ImageUrl, Is.EqualTo($"https://image.tmdb.org/t/p/w500{poster}"));
                Assert.That(saved.ImageLookup.ConfirmedMatch.ArtworkScope, Is.EqualTo("Provider artwork"));
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
                Assert.That(Directory.GetFiles(_suite._images, "*", SearchOption.AllDirectories), Has.Length.EqualTo(1));
            });
            await AssertDueAsync(false);
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
        internal async Task<Movie> AssertRetryAsync(ImageLookupOutcome outcome, DateTime retryAt, string id = MovieId)
        {
            var saved = await SavedAsync(id);
            AssertState(saved, outcome);
            Assert.Multiple(() =>
            {
                Assert.That(saved.CoverImage, Is.Null);
                Assert.That(saved.ImageLookup!.Provider, Is.EqualTo("Tmdb"));
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
