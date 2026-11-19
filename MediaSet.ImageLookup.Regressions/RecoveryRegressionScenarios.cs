using System.Text.Json;
using MediaSet.Api.Infrastructure.Database;
using MediaSet.Api.Shared.Models;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using NUnit.Framework;
using SkiaSharp;
using ImageLookupState = MediaSet.Api.Shared.Models.ImageLookup;

namespace MediaSet.ImageLookup.Regressions;

public sealed partial class RegressionSuite
{
    private const string RecoveryMetadata = "{\"docs\":[{\"key\":\"/works/OL7W\",\"title\":\"Fixture Book\",\"cover_i\":101}]}";
    private const string RecoveryArtwork = "https://covers.openlibrary.org/b/id/101-L.jpg?default=false";

    [Test]
    public async Task ProcessRecovery_SuccessiveFailuresAndDueBoundaries_RetainsHistoryAndRecoversAsync()
    {
        using var scenario = new BookScenario(this);
        scenario.Config.RetryInitialDelayMinutes = 10;
        scenario.Config.RetryMaxDelayMinutes = 15;
        scenario.OpenTitle("retry", "", count: 3, status: 503);
        scenario.OpenTitle("recovery", RecoveryMetadata);
        scenario.Art("recovered-cover", RecoveryArtwork);
        await scenario.InsertAsync(new Book { Id = BookId, Title = BookTitle });

        for (var attempt = 1; attempt <= 3; attempt++)
        {
            var attemptedAt = scenario.Clock.Now;
            await RecoveryPassAsync(scenario, $"failure-{attempt}");
            var saved = await scenario.SavedAsync();
            AssertFailureState(saved, ImageLookupOutcome.ProviderUnavailable, "OpenLibrary",
                attemptedAt.AddMinutes(attempt == 1 ? 10 : 15).UtcDateTime, attempt);
            Assert.Multiple(() =>
            {
                Assert.That(saved.ImageLookup!.AttemptCount, Is.EqualTo(attempt));
                Assert.That(saved.ImageLookup.LookupAttemptedAt, Is.EqualTo(attemptedAt.UtcDateTime));
                Assert.That(saved.ImageLookup.IsBacklogRecovery, Is.EqualTo(attempt > 1));
            });
            var before = JsonSerializer.Serialize(saved);
            scenario.Clock.Now = new DateTimeOffset(saved.ImageLookup!.NextEligibleAt!.Value, TimeSpan.Zero).AddMilliseconds(-1);
            await scenario.AssertDueAsync(false);
            await RecoveryPassAsync(scenario, $"deferred-{attempt}");
            Assert.That(JsonSerializer.Serialize(await scenario.SavedAsync()), Is.EqualTo(before));
            Assert.That(scenario.Transport.RequestIds.Count, Is.EqualTo(attempt));
            scenario.Clock.Now = scenario.Clock.Now.AddMilliseconds(1);
            await scenario.AssertDueAsync(true);
        }

        await RecoveryPassAsync(scenario, "recovered");
        var recovered = await scenario.SavedAsync();
        Assert.Multiple(() =>
        {
            Assert.That(recovered.ImageLookup!.LastOutcome, Is.EqualTo(ImageLookupOutcome.Success));
            Assert.That(recovered.ImageLookup.AttemptCount, Is.EqualTo(4));
            Assert.That(recovered.ImageLookup.LookupAttemptedAt, Is.EqualTo(scenario.Clock.Now.UtcDateTime));
            Assert.That(recovered.ImageLookup.NextEligibleAt, Is.Null);
            Assert.That(recovered.ImageLookup.FailureReason, Is.Null);
            Assert.That(recovered.ImageLookup.PermanentFailure, Is.False);
            Assert.That(recovered.ImageLookup.IsBacklogRecovery, Is.True);
        });
        using var bitmap = SKBitmap.Decode(Path.Combine(_images, recovered.CoverImage!.FilePath));
        Assert.That(bitmap, Is.Not.Null);
        Assert.That(bitmap.Width, Is.EqualTo(2));
        var completed = JsonSerializer.Serialize(recovered);
        scenario.Clock.Now = scenario.Clock.Now.AddDays(30);
        await RecoveryPassAsync(scenario, "completed-excluded");
        Assert.That(JsonSerializer.Serialize(await scenario.SavedAsync()), Is.EqualTo(completed));
        scenario.VerifyOrder("retry", "retry", "retry", "retry", "recovered-cover");
    }

    [TestCase(ImageLookupOutcome.NoMatch)]
    [TestCase(ImageLookupOutcome.MissingArtwork)]
    [TestCase(ImageLookupOutcome.MissingMetadata)]
    [TestCase(ImageLookupOutcome.UnsupportedStrategy)]
    [TestCase(ImageLookupOutcome.InvalidArtwork)]
    [TestCase(ImageLookupOutcome.AmbiguousMatch)]
    [TestCase(ImageLookupOutcome.Success)]
    [TestCase(ImageLookupOutcome.ProviderUnavailable)]
    public async Task ProcessRecovery_IneligibleState_ExcludesWithoutChangingDocumentAsync(ImageLookupOutcome outcome)
    {
        using var scenario = new BookScenario(this);
        // A retryable outcome without an eligibility time must also remain excluded.
        await scenario.InsertAsync(new Book
        {
            Id = BookId,
            Title = BookTitle,
            ImageLookup = new ImageLookupState
            {
                LastOutcome = outcome,
                AttemptCount = 3,
                LookupAttemptedAt = scenario.Clock.Now.AddDays(-1).UtcDateTime,
                NextEligibleAt = outcome == ImageLookupOutcome.ProviderUnavailable ? null : scenario.Clock.Now.AddDays(-1).UtcDateTime
            }
        });
        var before = JsonSerializer.Serialize(await scenario.SavedAsync());
        await RecoveryPassAsync(scenario, "excluded");
        scenario.Clock.Now = scenario.Clock.Now.AddDays(7);
        await RecoveryPassAsync(scenario, "still-excluded");
        Assert.That(JsonSerializer.Serialize(await scenario.SavedAsync()), Is.EqualTo(before));
        scenario.VerifyOrder();
        AssertNoFiles();
    }

    [TestCase(false, false)]
    [TestCase(true, false)]
    [TestCase(false, true)]
    [TestCase(true, true)]
    public async Task ProcessRecovery_ActiveLookupCancellation_LeavesFreshOrRetryStateUnchangedAsync(bool retry, bool artwork)
    {
        using var scenario = new BookScenario(this);
        scenario.OpenTitle("lookup", artwork ? RecoveryMetadata : "", failure: artwork ? null : "wait-for-cancellation");
        if (artwork)
        {
            scenario.Art("blocked-artwork", RecoveryArtwork, failure: "wait-for-cancellation");
        }
        await scenario.InsertAsync(new Book
        {
            Id = BookId,
            Title = BookTitle,
            ImageLookup = retry ? new ImageLookupState
            {
                LastOutcome = ImageLookupOutcome.ProviderUnavailable,
                AttemptCount = 2,
                Provider = "OpenLibrary",
                FailureReason = "previous failure",
                NextEligibleAt = scenario.Clock.Now.UtcDateTime,
                LookupAttemptedAt = scenario.Clock.Now.AddHours(-1).UtcDateTime
            } : null
        });
        var before = JsonSerializer.Serialize(await scenario.SavedAsync());
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        scenario.Transport.RequestStarted = id =>
        {
            if (id == (artwork ? "blocked-artwork" : "lookup"))
            {
                entered.TrySetResult();
            }
        };
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(scenario.Token);
        var processing = scenario.ProcessAsync(cancellation.Token);
        await entered.Task.WaitAsync(scenario.Token);
        Assert.That(processing.IsCompleted, Is.False, "The request must be active before cancelling");
        cancellation.Cancel();
        await Assert.ThatAsync(async () => await processing.WaitAsync(scenario.Token), Throws.InstanceOf<OperationCanceledException>());
        Assert.That(JsonSerializer.Serialize(await scenario.SavedAsync()), Is.EqualTo(before));
        scenario.VerifyOrder(artwork ? ["lookup", "blocked-artwork"] : ["lookup"]);
        AssertNoFiles();
        Progress("pass-cancelled", new { retry, artwork, unchanged = true });
    }

    [Test]
    public async Task ProcessRecovery_BoundedBatch_ReservesFreshAndDueSlotsAndResetsProgressAsync()
    {
        using var scenario = new BookScenario(this);
        scenario.Config.BatchSize = 2;
        scenario.OpenTitle("batch-search", RecoveryMetadata, count: 3);
        for (var index = 0; index < 3; index++)
        {
            scenario.Art("batch-cover", RecoveryArtwork);
            await scenario.InsertAsync(new Book
            {
                Id = (index + 1).ToString("x24"),
                Title = BookTitle,
                ImageLookup = index == 2 ? new ImageLookupState
                {
                    LastOutcome = ImageLookupOutcome.Queued,
                    NextEligibleAt = scenario.Clock.Now.UtcDateTime,
                    AttemptCount = 2,
                    RequeueOperationId = "fixture-requeue"
                } : null
            });
        }
        await RecoveryPassAsync(scenario, "bounded-first");
        var books = await _database.GetCollection<Book>().Find(Builders<Book>.Filter.Empty).SortBy(b => b.Id).ToListAsync(scenario.Token);
        Assert.Multiple(() =>
        {
            Assert.That(books[0].CoverImage, Is.Not.Null);
            Assert.That(books[1].ImageLookup, Is.Null);
            Assert.That(books[2].CoverImage, Is.Not.Null);
            Assert.That(books[2].ImageLookup!.AttemptCount, Is.EqualTo(3));
            Assert.That(books[2].ImageLookup!.RequeueOperationId, Is.EqualTo("fixture-requeue"));
        });
        AssertRunProgress(scenario, 2, 2);
        scenario.Logs.Clear();
        await RecoveryPassAsync(scenario, "bounded-second");
        AssertRunProgress(scenario, 1, 1);
        scenario.Logs.Clear();
        await RecoveryPassAsync(scenario, "bounded-empty");
        AssertRunProgress(scenario, 0, 0);
        scenario.VerifyOrder("batch-search", "batch-cover", "batch-search", "batch-cover", "batch-search", "batch-cover");
    }

    [Test]
    public async Task ProcessRecovery_RuntimeBoundary_StopsBeforeNextEntityAsync()
    {
        using var scenario = new BookScenario(this);
        scenario.Config.BatchSize = 4;
        scenario.Config.MaxRuntimeMinutes = 1;
        scenario.OpenTitle("runtime-search", RecoveryMetadata);
        scenario.Art("runtime-cover", RecoveryArtwork);
        await scenario.InsertAsync(new Book { Id = BookId, Title = BookTitle });
        await scenario.InsertAsync(new Book { Id = "000000000000000000000008", Title = BookTitle });
        scenario.Transport.RequestStarted = id =>
        {
            if (id == "runtime-cover")
            {
                scenario.Clock.Now = scenario.Clock.Now.AddMinutes(2);
            }
        };
        await RecoveryPassAsync(scenario, "runtime-bound");
        Assert.That((await scenario.SavedAsync()).CoverImage, Is.Not.Null);
        Assert.That((await scenario.SavedAsync("000000000000000000000008")).ImageLookup, Is.Null);
        AssertRunProgress(scenario, 1, 1);
        scenario.VerifyOrder("runtime-search", "runtime-cover");
    }

    [Test]
    public async Task ProcessRecovery_IsolatedDatabaseAndImages_DoesNotReadOrMutateOtherScenarioAsync()
    {
        var other = new DatabaseService(Options.Create(new MediaSetDatabaseSettings
        {
            ConnectionString = _connection,
            DatabaseName = "control_" + Guid.NewGuid().ToString("N")
        }));
        var controlImages = Path.Combine(_imageRoot, "isolation-control");
        Directory.CreateDirectory(controlImages);
        var sentinel = Path.Combine(controlImages, "retained.txt");
        await File.WriteAllTextAsync(sentinel, "previous scenario artifact");
        await other.GetCollection<Book>().InsertOneAsync(new Book { Id = BookId, Title = "Must never be processed" });
        using var scenario = new BookScenario(this);
        await RecoveryPassAsync(scenario, "empty-isolated-database");
        scenario.VerifyOrder();
        Assert.That(await _database.GetCollection<Book>().CountDocumentsAsync(Builders<Book>.Filter.Empty), Is.Zero);
        Assert.That((await other.GetCollection<Book>().Find(b => b.Id == BookId).SingleAsync()).ImageLookup, Is.Null);
        Assert.That(await File.ReadAllTextAsync(sentinel), Is.EqualTo("previous scenario artifact"));
        Assert.That(Directory.Exists(_images), Is.False);
        Progress("isolation-verified", new { controlImages, isolatedImages = _images });
    }

    private async Task RecoveryPassAsync(BookScenario scenario, string pass)
    {
        Progress("pass-start", new { pass, now = scenario.Clock.Now, batchSize = scenario.Config.BatchSize });
        await scenario.ProcessAsync();
        Progress("pass-finished", new
        {
            pass,
            now = scenario.Clock.Now,
            books = await _database.GetCollection<Book>().Find(Builders<Book>.Filter.Empty).ToListAsync(scenario.Token),
            requests = scenario.Transport.RequestIds.ToArray()
        });
    }

    private static void AssertRunProgress(BookScenario scenario, int processed, int succeeded)
    {
        var properties = scenario.Logs.SelectMany(log => log.GetProperty("properties").EnumerateArray())
            .Where(pair => pair.GetProperty("Key").GetString() == "Processed").ToArray();
        Assert.That(properties.Select(p => p.GetProperty("Value").GetInt32()), Is.EqualTo([processed]));
        var completed = scenario.Logs.Single(log => log.GetProperty("message").GetString()!.StartsWith("Background image lookup run completed.", StringComparison.Ordinal));
        var values = completed.GetProperty("properties").EnumerateArray().ToDictionary(p => p.GetProperty("Key").GetString()!, p => p.GetProperty("Value"));
        Assert.That(values["Succeeded"].GetInt32(), Is.EqualTo(succeeded));
        Assert.That(values["Failed"].GetInt32(), Is.EqualTo(processed - succeeded));
        var diagnostics = scenario.Logs.Single(log => log.GetProperty("message").GetString()!.StartsWith("Background image lookup run diagnostics:", StringComparison.Ordinal));
        var attempts = diagnostics.GetProperty("properties").EnumerateArray().Single(p => p.GetProperty("Key").GetString() == "@Attempts").GetProperty("Value");
        Assert.That(attempts.GetArrayLength(), Is.EqualTo(processed));
    }
}
