using System;
using MediaSet.Api.Features.Images.Models;
using MediaSet.Api.Features.Images.Services;
using MediaSet.Api.Shared.Models;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using NUnit.Framework;

namespace MediaSet.Api.Tests.Features.Images.Services;

[TestFixture]
public class ImageRetryPolicyTests
{
    [Test]
    public void Complete_TransientFailure_SchedulesConfiguredBoundedBackoff()
    {
        var config = new BackgroundImageLookupConfiguration { RetryInitialDelayMinutes = 5, RetryMaxDelayMinutes = 12 };
        var book = new Book { Title = "Dune" };
        var now = DateTime.UtcNow;
        var failure = new ImageLookupResult(false, null, null, "network");
        foreach (var minutes in new[] { 5, 10, 12, 12 })
        {
            book.ImageLookup = ImageRetryPolicy.Complete(book, failure, config, now);
            Assert.That(book.ImageLookup.NextEligibleAt, Is.EqualTo(now.AddMinutes(minutes)));
            Assert.That(book.ImageLookup.LastOutcome, Is.EqualTo(ImageLookupOutcome.TransientFailure));
            Assert.That(book.ImageLookup.PermanentFailure, Is.False);
        }
        Assert.That(book.ImageLookup!.AttemptCount, Is.EqualTo(4));
    }

    [TestCase(ImageLookupOutcome.NoMatch)]
    [TestCase(ImageLookupOutcome.MissingArtwork)]
    [TestCase(ImageLookupOutcome.MissingMetadata)]
    public void Complete_TerminalFailure_HasNoNextRun(ImageLookupOutcome outcome)
    {
        var result = ImageRetryPolicy.Complete(new Book(), new(false, null, null, "terminal", Outcome: outcome),
            new(), DateTime.UtcNow);
        Assert.That(result.PermanentFailure, Is.True);
        Assert.That(result.NextEligibleAt, Is.Null);
    }

    [Test]
    public void DueFilter_ExcludesSuccessManualAndTerminal_RequiresDueTime()
    {
        var now = DateTime.UtcNow;
        var filter = ImageRetryPolicy.DueFilter<Book>(now).Render(new RenderArgs<Book>(
            BsonSerializer.LookupSerializer<Book>(), BsonSerializer.SerializerRegistry));
        Assert.That(filter["CoverImage"], Is.EqualTo(BsonNull.Value));
        Assert.That(filter["ImageLookup.LastOutcome"]["$in"].AsBsonArray,
            Is.EquivalentTo(new[] { new BsonInt32((int)ImageLookupOutcome.TransientFailure),
                new BsonInt32((int)ImageLookupOutcome.Queued),
                new BsonInt32((int)ImageLookupOutcome.QuotaLimited),
                new BsonInt32((int)ImageLookupOutcome.Unauthorized),
                new BsonInt32((int)ImageLookupOutcome.ProviderUnavailable),
                new BsonInt32((int)ImageLookupOutcome.DownloadFailure) }));
        Assert.That(filter["ImageLookup.NextEligibleAt"]["$lte"].ToUniversalTime(),
            Is.EqualTo(new BsonDateTime(now).ToUniversalTime()));
    }

    [Test]
    public void CanRequeue_TerminalFailure_RequiresMetadataOrStrategyChangeAndOptIn()
    {
        var config = new BackgroundImageLookupConfiguration();
        var book = new Book { Title = "Dune" };
        book.ImageLookup = ImageRetryPolicy.Complete(book,
            new(false, null, null, "no match", Outcome: ImageLookupOutcome.NoMatch), config, DateTime.UtcNow);
        Assert.That(ImageRetryPolicy.CanRequeue(book, config, true), Is.False);
        book.Title = "Dune Messiah";
        Assert.That(ImageRetryPolicy.CanRequeue(book, config, false), Is.False);
        Assert.That(ImageRetryPolicy.CanRequeue(book, config, true), Is.True);
        book.Title = "Dune";
        config.StrategyVersion++;
        Assert.That(ImageRetryPolicy.CanRequeue(book, config, true), Is.True);
    }

    [Test]
    public void CanRequeue_HistoricalPermanentFailure_ReassessesMissingVersion()
    {
        var music = new Music { Title = "Album", ImageLookup = new() { FailureReason = "no barcode", PermanentFailure = true } };
        Assert.That(ImageRetryPolicy.CanRequeue(music, new(), true), Is.True);
        music.CoverImage = new Image();
        Assert.That(ImageRetryPolicy.CanRequeue(music, new(), true), Is.False);
        music.CoverImage = null;
        music.ImageLookup.LastOutcome = ImageLookupOutcome.Success;
        Assert.That(ImageRetryPolicy.CanRequeue(music, new(), true), Is.False);
    }

    [Test]
    public void Complete_RequeuedHistoricalItem_PreservesBacklogAndOperation()
    {
        var book = new Book { ImageLookup = new() { AttemptCount = 3, RequeueOperationId = "recovery-1" } };
        var result = ImageRetryPolicy.Complete(book, new(true, null, new Image(), null), new(), DateTime.UtcNow);
        Assert.That(result.IsBacklogRecovery, Is.True);
        Assert.That(result.AttemptCount, Is.EqualTo(4));
        Assert.That(result.RequeueOperationId, Is.EqualTo("recovery-1"));
        Assert.That(result.NextEligibleAt, Is.Null);
        Assert.That(ImageRetryPolicy.Complete(new Book(), new(true, null, new Image(), null), new(), DateTime.UtcNow)
            .IsBacklogRecovery, Is.False);
    }
}
