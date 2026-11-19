using System.Text.Json;
using MediaSet.Api.Features.Images.Models;
using MediaSet.Api.Features.Images.Services;
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
    private const string BookId = "000000000000000000000007";
    private const string Isbn = "9780000000001";
    private const string BookTitle = "Fixture Book";
    private const string Png = "iVBORw0KGgoAAAANSUhEUgAAAAIAAAACCAIAAAD91JpzAAAAEklEQVR4nGNQSFigkLCAAUIBAB2OBIG+B74KAAAAAElFTkSuQmCC";
    private const string SearchFields = "&fields=key,title,author_name,first_publish_year,isbn,publisher,subject,number_of_pages_median,cover_i,cover_edition_key";

    [Test]
    public async Task ProcessBook_IsbnArtworkUnavailable_SavesGoogleExactEditionAsync()
    {
        using var scenario = new BookScenario(this);
        scenario.OpenIsbn("isbn-metadata", "{\"records\":{},\"items\":[]}");
        scenario.Art("isbn-cover-missing", $"https://covers.openlibrary.org/b/isbn/{Isbn}-L.jpg?default=false", 404);
        scenario.Google("google-isbn", $"isbn:{Isbn}", GoogleVolume("http://books.google.com/cover?edge=curl&id=exact"));
        scenario.Art("google-art", "https://books.google.com/cover?id=exact");
        await scenario.InsertAsync(new Book { Id = BookId, Title = BookTitle, ISBN = Isbn });

        await scenario.ProcessAsync();

        var saved = await scenario.SavedAsync();
        await AssertSavedImageAsync(scenario, saved, "GoogleBooks", "Books:Isbn", "Exact-edition artwork");
        Assert.That(saved.ImageLookup!.ConfirmedMatch!.ProviderId, Is.EqualTo(Isbn));
        scenario.VerifyOrder("isbn-metadata", "isbn-cover-missing", "google-isbn", "google-art");
        await scenario.ProcessAsync();
        Assert.That((await scenario.SavedAsync()).ImageLookup!.AttemptCount, Is.EqualTo(1));
        scenario.VerifyOrder("isbn-metadata", "isbn-cover-missing", "google-isbn", "google-art");
    }

    [Test]
    public async Task ProcessBook_TitleAndAuthorFallback_RejectsOtherVolumeAndSavesGoogleArtworkAsync()
    {
        using var scenario = new BookScenario(this);
        scenario.OpenTitle("title-search", "{\"docs\":[]}", "Alice Example");
        scenario.Google("google-title", $"intitle:\"{BookTitle}\" inauthor:\"Alice Example\"",
            "{\"items\":[{\"volumeInfo\":{\"title\":\"Other Book\",\"authors\":[\"Alice Example\"],\"imageLinks\":{\"large\":\"https://books.google.com/wrong-title\"}}}," +
            "{\"volumeInfo\":{\"title\":\"Fixture Book\",\"authors\":[\"Other Author\"],\"imageLinks\":{\"large\":\"https://books.google.com/wrong-author\"}}}," +
            "{\"volumeInfo\":{\"title\":\"Fixture Book\",\"authors\":[\"Alice Example\"],\"imageLinks\":{\"large\":\"http://books.google.com/title?edge=curl&id=title\"}}}]}");
        scenario.Art("title-art", "https://books.google.com/title?id=title");
        await scenario.InsertAsync(new Book { Id = BookId, Title = BookTitle, Authors = ["Alice Example"] });

        await scenario.ProcessAsync();

        await AssertSavedImageAsync(scenario, await scenario.SavedAsync(), "GoogleBooks", "Books:Entity", "Provider artwork");
        scenario.VerifyOrder("title-search", "google-title", "title-art");
    }

    [Test]
    public async Task ProcessBook_RepeatedUnavailableCover_TriesEachUrlOnceThenSavesNextCandidateAsync()
    {
        using var scenario = new BookScenario(this);
        const string missing = "https://covers.openlibrary.org/b/id/101-L.jpg?default=false";
        scenario.OpenIsbn("isbn-metadata", "{\"records\":{\"/books/OL7M\":{\"data\":{\"title\":\"Fixture Book\",\"cover\":{\"large\":\"http://covers.openlibrary.org/b/id/101-L.jpg\",\"medium\":\"https://covers.openlibrary.org/b/id/101-L.jpg?default=true\",\"small\":\"https://covers.openlibrary.org/b/id/102-L.jpg\"}},\"olids\":[\"OL7M\"],\"isbns\":[\"9780000000001\"]}},\"items\":[]}");
        scenario.Art("duplicate-cover-missing", missing, 404);
        scenario.Art("next-cover", "https://covers.openlibrary.org/b/id/102-L.jpg?default=false");
        await scenario.InsertAsync(new Book { Id = BookId, Title = BookTitle, ISBN = Isbn, ImageUrl = missing });

        await scenario.ProcessAsync();

        await AssertSavedImageAsync(scenario, await scenario.SavedAsync(), "OpenLibrary", "Books:Isbn", "Exact-edition artwork");
        scenario.VerifyOrder("duplicate-cover-missing", "isbn-metadata", "next-cover");
    }

    [Test]
    public async Task ProcessBook_IdentifierAndTitleCandidatesExhausted_PersistsTerminalMissAsync()
    {
        using var scenario = new BookScenario(this);
        scenario.OpenIsbn("isbn-metadata", "{\"records\":{},\"items\":[]}");
        scenario.Art("isbn-cover-missing", $"https://covers.openlibrary.org/b/isbn/{Isbn}-L.jpg?default=false", 404);
        scenario.Google("google-isbn", $"isbn:{Isbn}", "{\"items\":[{\"volumeInfo\":{\"industryIdentifiers\":[{\"type\":\"ISBN_13\",\"identifier\":\"9780000000002\"}],\"imageLinks\":{\"large\":\"https://books.google.com/wrong-isbn\"}}}]}");
        scenario.OpenTitle("title-search", "{\"docs\":[]}");
        scenario.Google("google-title", $"intitle:\"{BookTitle}\"", "{\"items\":[]}");
        await scenario.InsertAsync(new Book { Id = BookId, Title = BookTitle, ISBN = Isbn });

        await scenario.ProcessAsync();

        var saved = await scenario.SavedAsync();
        AssertTerminal(saved, ImageLookupOutcome.NoMatch);
        Assert.That(saved.ImageLookup!.ConfirmedMatch, Is.Null);
        await scenario.AssertDueAsync(false);
        await scenario.ProcessAsync();
        Assert.That((await scenario.SavedAsync()).ImageLookup!.AttemptCount, Is.EqualTo(1));
        scenario.VerifyOrder("isbn-metadata", "isbn-cover-missing", "google-isbn", "title-search", "google-title");
        AssertNoFiles();
    }

    [Test]
    public async Task ProcessBook_MatchingTitleWithoutArtwork_PersistsIdentityAndTerminalMissingArtworkAsync()
    {
        using var scenario = new BookScenario(this);
        scenario.OpenTitle("title-search", "{\"docs\":[{\"key\":\"/works/OL7W\",\"title\":\"Fixture Book\",\"cover_i\":101}]}");
        scenario.Art("title-cover-missing", "https://covers.openlibrary.org/b/id/101-L.jpg?default=false", 404);
        scenario.Google("google-title", $"intitle:\"{BookTitle}\"", "{\"items\":[]}");
        await scenario.InsertAsync(new Book { Id = BookId, Title = BookTitle });

        await scenario.ProcessAsync();

        var saved = await scenario.SavedAsync();
        AssertTerminal(saved, ImageLookupOutcome.MissingArtwork);
        Assert.That(saved.ImageLookup!.ConfirmedMatch!.ProviderId, Is.EqualTo("/works/OL7W"));
        await scenario.AssertDueAsync(false);
        scenario.VerifyOrder("title-search", "title-cover-missing", "google-title");
        AssertNoFiles();
    }

    [TestCase(false)]
    [TestCase(true)]
    public Task ProcessBook_ProviderQuota_DefersNextBookWithoutAnotherProviderRequestAsync(bool google) => VerifyBookQuotaAsync(google);

    private async Task VerifyBookQuotaAsync(bool google)
    {
        using var scenario = new BookScenario(this);
        // HTTP-date, rather than a relative delay, makes persisted reset assertions exact.
        var reset = new DateTimeOffset(DateTime.UtcNow.AddHours(1).Ticks / TimeSpan.TicksPerSecond * TimeSpan.TicksPerSecond, TimeSpan.Zero);
        scenario.Clock.Now = reset.AddHours(-1);
        if (google)
        {
            scenario.OpenTitle("title-search", "{\"docs\":[]}", count: 2);
            scenario.Google("quota", $"intitle:\"{BookTitle}\"", "", 429, reset);
        }
        else
        {
            scenario.OpenTitle("quota", "", status: 429, reset: reset);
        }
        await scenario.InsertAsync(new Book { Id = BookId, Title = BookTitle });

        await scenario.ProcessAsync();

        var first = await scenario.SavedAsync();
        AssertFailureState(first, ImageLookupOutcome.QuotaLimited, google ? "GoogleBooks" : "OpenLibrary", reset.UtcDateTime);
        await scenario.AssertDueAsync(false);
        await scenario.ProcessAsync(); // Same book is not yet eligible: no HTTP and no new attempt.
        Assert.That((await scenario.SavedAsync()).ImageLookup!.AttemptCount, Is.EqualTo(1));
        const string secondId = "000000000000000000000008";
        await scenario.InsertAsync(new Book { Id = secondId, Title = BookTitle });
        await scenario.ProcessAsync(); // Fresh book hits the provider's host-level quota guard.
        AssertFailureState(await scenario.SavedAsync(secondId), ImageLookupOutcome.QuotaLimited,
            google ? "GoogleBooks" : "OpenLibrary", reset.UtcDateTime);
        scenario.Clock.Now = reset;
        await scenario.AssertDueAsync(true);
        scenario.VerifyOrder(google ? ["title-search", "quota", "title-search"] : ["quota"]);
        AssertNoFiles();
    }

    [TestCase(false, "503")]
    [TestCase(false, "network")]
    [TestCase(false, "timeout")]
    [TestCase(true, "503")]
    [TestCase(true, "network")]
    [TestCase(true, "timeout")]
    public async Task ProcessBook_TemporaryProviderFailure_RetriesWhenDueAndRetainsAttemptHistoryAsync(bool google, string failure)
    {
        using var assertions = Assert.EnterMultipleScope();
        using var scenario = new BookScenario(this);
        if (google)
        {
            scenario.OpenTitle("title-search", "{\"docs\":[]}", count: 2);
            scenario.Google("temporary-failure", $"intitle:\"{BookTitle}\"", "", failure == "503" ? 503 : 200, failure: failure == "503" ? null : failure);
        }
        else
        {
            scenario.OpenTitle("temporary-failure", "", status: failure == "503" ? 503 : 200, failure: failure == "503" ? null : failure);
            scenario.OpenTitle("title-search", "{\"docs\":[]}");
        }
        scenario.Google("google-recovery", $"intitle:\"{BookTitle}\"", GoogleVolume("https://books.google.com/recovered"));
        scenario.Art("recovered-art", "https://books.google.com/recovered");
        await scenario.InsertAsync(new Book { Id = BookId, Title = BookTitle });

        await scenario.ProcessAsync();

        var first = await scenario.SavedAsync();
        AssertFailureState(first, ImageLookupOutcome.ProviderUnavailable, google ? "GoogleBooks" : "OpenLibrary",
            scenario.Clock.GetUtcNow().UtcDateTime.AddMinutes(scenario.Config.RetryInitialDelayMinutes));
        await scenario.AssertDueAsync(false);
        AssertNoFiles();
        var beforeRetry = scenario.Transport.RequestIds.ToArray();
        await scenario.ProcessAsync();
        Assert.That(scenario.Transport.RequestIds, Is.EqualTo(beforeRetry));
        Assert.That((await scenario.SavedAsync()).ImageLookup!.AttemptCount, Is.EqualTo(1));
        scenario.Clock.Now = new DateTimeOffset(first.ImageLookup!.NextEligibleAt!.Value, TimeSpan.Zero);
        await scenario.AssertDueAsync(true);
        await scenario.ProcessAsync();
        var recovered = await scenario.SavedAsync();
        await AssertSavedImageAsync(scenario, recovered, "GoogleBooks", "Books:Entity", "Provider artwork", attempts: 2);
        Assert.That(recovered.ImageLookup!.IsBacklogRecovery, Is.True);
        scenario.VerifyOrder(google
            ? ["title-search", "temporary-failure", "title-search", "temporary-failure", "recovered-art"]
            : ["temporary-failure", "temporary-failure", "google-recovery", "recovered-art"]);
    }

    private static string GoogleVolume(string url) => JsonSerializer.Serialize(new
    {
        items = new[] { new { volumeInfo = new { title = BookTitle, industryIdentifiers = new[] { new { type = "ISBN_13", identifier = Isbn } }, imageLinks = new { large = url } } } }
    });

    private void AssertNoFiles() => Assert.That(Directory.Exists(_images) ? Directory.GetFiles(_images, "*", SearchOption.AllDirectories) : [], Is.Empty);

    private static void AssertTerminal(Book saved, ImageLookupOutcome outcome)
    {
        Assert.That(saved.ImageLookup, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(saved.CoverImage, Is.Null);
            Assert.That(saved.ImageLookup!.LastOutcome, Is.EqualTo(outcome));
            Assert.That(saved.ImageLookup.PermanentFailure, Is.True);
            Assert.That(saved.ImageLookup.NextEligibleAt, Is.Null);
            Assert.That(saved.ImageLookup.AttemptCount, Is.EqualTo(1));
            Assert.That(saved.ImageLookup.FailureReason, Is.Not.Empty);
            Assert.That(saved.ImageLookup.MetadataFingerprint, Is.EqualTo(ImageRetryPolicy.Fingerprint(saved)));
        });
    }

    private static void AssertFailureState(Book saved, ImageLookupOutcome outcome, string provider, DateTime retry, int attempts = 1)
    {
        Assert.That(saved.ImageLookup, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(saved.CoverImage, Is.Null);
            Assert.That(saved.ImageLookup!.LastOutcome, Is.EqualTo(outcome));
            Assert.That(saved.ImageLookup.Provider, Is.EqualTo(provider));
            Assert.That(saved.ImageLookup.AttemptedStrategy, Is.EqualTo("Books:Entity"));
            Assert.That(saved.ImageLookup.PermanentFailure, Is.False);
            Assert.That(saved.ImageLookup.AttemptCount, Is.EqualTo(attempts));
            Assert.That(saved.ImageLookup.NextEligibleAt, Is.EqualTo(retry));
            Assert.That(saved.ImageLookup.FailureReason, Is.Not.Empty);
            Assert.That(saved.ImageLookup.MetadataFingerprint, Is.EqualTo(ImageRetryPolicy.Fingerprint(saved)));
        });
    }

    private async Task AssertSavedImageAsync(BookScenario scenario, Book saved, string provider, string strategy, string scope, int attempts = 1)
    {
        Assert.That(saved.CoverImage, Is.Not.Null);
        Assert.That(saved.ImageLookup, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(saved.ImageLookup!.LastOutcome, Is.EqualTo(ImageLookupOutcome.Success));
            Assert.That(saved.ImageLookup.Provider, Is.EqualTo(provider));
            Assert.That(saved.ImageLookup.AttemptedStrategy, Is.EqualTo(strategy));
            Assert.That(saved.ImageLookup.AttemptCount, Is.EqualTo(attempts));
            Assert.That(saved.ImageLookup.LookupAttemptedAt, Is.EqualTo(scenario.Clock.GetUtcNow().UtcDateTime));
            Assert.That(saved.ImageLookup.NextEligibleAt, Is.Null);
            Assert.That(saved.ImageLookup.FailureReason, Is.Null);
            Assert.That(saved.ImageLookup.PermanentFailure, Is.False);
            Assert.That(saved.ImageLookup.ConfirmedMatch!.Provider, Is.EqualTo(provider));
            Assert.That(saved.ImageLookup.ConfirmedMatch.ArtworkScope, Is.EqualTo(scope));
            Assert.That(saved.ImageLookup.MetadataFingerprint, Is.EqualTo(ImageRetryPolicy.Fingerprint(saved)));
        });
        await using var stream = await scenario.Services.GetRequiredService<IImageService>().GetImageStreamAsync(saved.CoverImage!.FilePath, scenario.Token);
        using var bitmap = SKBitmap.Decode(stream);
        Assert.That(bitmap, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(bitmap.Width, Is.EqualTo(2));
            Assert.That(bitmap.Height, Is.EqualTo(2));
            Assert.That(Directory.GetFiles(_images, "*", SearchOption.AllDirectories), Has.Length.EqualTo(1));
            Assert.That(File.Exists(Path.Combine(_images, saved.CoverImage.FilePath)), Is.True);
        });
        await scenario.AssertDueAsync(false);
    }

    private sealed class BookScenario : IDisposable
    {
        private readonly RegressionSuite _suite;
        private readonly string _openHost = $"openlibrary-{Guid.NewGuid():N}.invalid";
        private readonly string _googleHost = $"googlebooks-{Guid.NewGuid():N}.invalid";
        private readonly CancellationTokenSource _deadline = new(TimeSpan.FromSeconds(20));
        private readonly BackgroundImageLookupService _processor;
        private readonly Dictionary<string, Fixture> _requests = [];
        internal StrictHttpFixtures Transport { get; } = new(Progress);
        internal ServiceProvider Services { get; }
        internal ScenarioTimeProvider Clock { get; } = new();
        internal List<JsonElement> Logs { get; } = [];
        internal BackgroundImageLookupConfiguration Config { get; } = new() { BatchSize = 1, RequestsPerMinute = int.MaxValue };
        internal CancellationToken Token => _deadline.Token;

        internal BookScenario(RegressionSuite suite)
        {
            _suite = suite;
            Services = suite.CreateBookServices(Transport, _openHost, _googleHost, new ProgressLoggerProvider((action, details) =>
            {
                Logs.Add(JsonSerializer.SerializeToElement(details));
                Progress(action, details);
            }));
            _processor = new BackgroundImageLookupService(Services.GetRequiredService<IServiceScopeFactory>(),
                Options.Create(Config), Services.GetRequiredService<ILogger<BackgroundImageLookupService>>(), Clock);
        }

        internal void OpenIsbn(string id, string text) => Add(id, $"https://{_openHost}/api/volumes/brief/isbn/{Isbn}.json", text);
        internal void OpenTitle(string id, string text, string? author = null, int count = 1, int status = 200, DateTimeOffset? reset = null, string? failure = null) =>
            Add(id, $"https://{_openHost}/search.json?{(author == null ? $"q={Uri.EscapeDataString(BookTitle)}" : $"title={Uri.EscapeDataString(BookTitle)}&author={Uri.EscapeDataString(author)}")}&limit=10{SearchFields}", text, status, reset, failure, count);
        internal void Google(string id, string query, string text, int status = 200, DateTimeOffset? reset = null, string? failure = null) =>
            Add(id, $"https://{_googleHost}/books/v1/volumes?q={Uri.EscapeDataString(query).Replace("%3A", ":")}&maxResults=10&printType=books", text, status, reset, failure);
        internal void Art(string id, string url, int status = 200, string? failure = null) => Add(id, url, "", status, failure: failure, image: true);

        private void Add(string id, string url, string text, int status = 200, DateTimeOffset? reset = null, string? failure = null, int count = 1, bool image = false)
        {
            var uri = new Uri(url);
            var fixture = new Fixture
            {
                Id = id,
                Host = uri.Authority,
                Path = uri.AbsolutePath,
                Query = uri.Query,
                Responses = [.. Enumerable.Range(0, count).Select(_ => new FixtureResponse
                {
                    Status = status,
                    Text = text,
                    Failure = failure,
                    Base64 = image && status == 200 ? Png : null,
                    ContentType = image ? "image/png" : "application/json",
                    Headers = reset == null ? [] : new() { ["Retry-After"] = reset.Value.ToString("R") }
                })]
            };
            if (_requests.TryGetValue(url, out var existing))
            {
                existing.Responses.AddRange(fixture.Responses);
            }
            else
            {
                _requests.Add(url, fixture);
                Transport.Add(fixture);
            }
        }

        internal Task InsertAsync(Book book) => _suite._database.GetCollection<Book>().InsertOneAsync(book, cancellationToken: Token);
        internal Task ProcessAsync() => _processor.ProcessAllEntityTypesAsync(Token);
        internal Task ProcessAsync(CancellationToken token) => _processor.ProcessAllEntityTypesAsync(token);
        internal Task<Book> SavedAsync(string id = BookId) => _suite._database.GetCollection<Book>().Find(b => b.Id == id).SingleAsync(Token);
        internal async Task AssertDueAsync(bool expected)
        {
            var due = await _suite._database.GetCollection<Book>().Find(ImageRetryPolicy.DueFilter<Book>(Clock.GetUtcNow().UtcDateTime)).ToListAsync(Token);
            Assert.That(due.Any(b => b.Id == BookId), Is.EqualTo(expected));
        }
        internal void VerifyOrder(params string[] ids)
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
