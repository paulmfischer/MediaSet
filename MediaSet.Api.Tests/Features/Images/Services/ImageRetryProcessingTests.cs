using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using MediaSet.Api.Features.Images.Models;
using MediaSet.Api.Features.Images.Services;
using MediaSet.Api.Infrastructure.Database;
using MediaSet.Api.Shared.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using Moq;
using NUnit.Framework;

namespace MediaSet.Api.Tests.Features.Images.Services;

[TestFixture]
public class ImageRetryProcessingTests
{
    private Mock<IMongoCollection<Book>> collection = null!;
    private Mock<IDatabaseService> database = null!;
    private readonly List<BsonDocument> filters = [];
    private readonly List<BsonDocument> updates = [];

    private static BsonDocument Render(FilterDefinition<Book> filter) => filter.Render(
        new RenderArgs<Book>(BsonSerializer.LookupSerializer<Book>(), BsonSerializer.SerializerRegistry));

    private static IAsyncCursor<Book> Cursor(IEnumerable<Book> books)
    {
        var cursor = new Mock<IAsyncCursor<Book>>();
        cursor.SetupGet(c => c.Current).Returns(books);
        cursor.SetupSequence(c => c.MoveNextAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true).ReturnsAsync(false);
        return cursor.Object;
    }

    [SetUp]
    public void Setup()
    {
        filters.Clear();
        updates.Clear();
        collection = new();
        database = new();
        database.Setup(d => d.GetCollection<Book>()).Returns(collection.Object);
        collection.Setup(c => c.UpdateOneAsync(It.IsAny<FilterDefinition<Book>>(),
                It.IsAny<UpdateDefinition<Book>>(), It.IsAny<UpdateOptions>(), It.IsAny<CancellationToken>()))
            .Callback<FilterDefinition<Book>, UpdateDefinition<Book>, UpdateOptions, CancellationToken>((f, u, _, _) =>
            {
                filters.Add(Render(f));
                updates.Add(u.Render(new RenderArgs<Book>(BsonSerializer.LookupSerializer<Book>(),
                    BsonSerializer.SerializerRegistry)).AsBsonDocument);
            })
            .ReturnsAsync(new UpdateResult.Acknowledged(1, 1, null));
    }

    [Test]
    public async Task RequeueAsync_SelectivePages_ResumesAndRetainsAttemptHistory()
    {
        var first = new Book { Id = "000000000000000000000001", Title = "First",
            ImageLookup = new() { FailureReason = "old permanent", PermanentFailure = true } };
        var second = new Book { Id = "000000000000000000000002", Title = "Second",
            ImageLookup = new() { FailureReason = "network" } };
        collection.SetupSequence(c => c.FindAsync(It.IsAny<FilterDefinition<Book>>(),
                It.IsAny<FindOptions<Book, Book>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Cursor([first])).ReturnsAsync(Cursor([second])).ReturnsAsync(Cursor([]));
        var service = new ImageRequeueService(database.Object, Options.Create(new BackgroundImageLookupConfiguration()));
        var request = new ImageRequeueRequest("recover-1", DateTime.UtcNow, 1,
            IncludeChangedTerminal: true, EntityIds: [first.Id, second.Id]);
        var page1 = await service.RequeueAsync("books", request, CancellationToken.None);
        var page2 = await service.RequeueAsync("books", request with { AfterId = page1.NextCursor }, CancellationToken.None);
        var page3 = await service.RequeueAsync("books", request with { AfterId = page2.NextCursor }, CancellationToken.None);
        Assert.That(page1, Is.EqualTo(new ImageRequeueResult(1, 1, first.Id, false)));
        Assert.That(page2, Is.EqualTo(new ImageRequeueResult(1, 1, second.Id, false)));
        Assert.That(page3.Complete, Is.True);
        Assert.That(updates.All(u => u["$set"]["ImageLookup.RequeueOperationId"] == "recover-1"), Is.True);
        Assert.That(updates.All(u => !u["$set"].AsBsonDocument.Contains("ImageLookup.AttemptCount")), Is.True);
        Assert.That(filters[1].ToJson(), Does.Contain("$gt").And.Contain("$in").And.Contain("CoverImage"));
        Assert.That(filters[1].ToJson(), Does.Contain("ImageLookup.LookupAttemptedAt"));
    }

    [Test]
    public async Task RequeueAsync_UnchangedTerminalAndSuccessAndManual_AreSkipped()
    {
        var config = new BackgroundImageLookupConfiguration();
        var terminal = new Book { Title = "No match" };
        terminal.ImageLookup = ImageRetryPolicy.Complete(terminal,
            new(false, null, null, "terminal", Outcome: ImageLookupOutcome.NoMatch), config, DateTime.UtcNow);
        var manual = new Book { CoverImage = new Image(), ImageLookup = new() { FailureReason = "old" } };
        var success = new Book { ImageLookup = new() { LastOutcome = ImageLookupOutcome.Success } };
        collection.Setup(c => c.FindAsync(It.IsAny<FilterDefinition<Book>>(), It.IsAny<FindOptions<Book, Book>>(),
            It.IsAny<CancellationToken>())).ReturnsAsync(Cursor([terminal, manual, success]));
        var result = await new ImageRequeueService(database.Object, Options.Create(config))
            .RequeueAsync("books", new("operation", DateTime.UtcNow, IncludeChangedTerminal: true), CancellationToken.None);
        Assert.That(result.Queued, Is.Zero);
        Assert.That(updates, Is.Empty);
    }

    [Test]
    public void RequeueAsync_Cancelled_DoesNotUpdate()
    {
        collection.Setup(c => c.FindAsync(It.IsAny<FilterDefinition<Book>>(), It.IsAny<FindOptions<Book, Book>>(),
            It.IsAny<CancellationToken>())).ReturnsAsync(Cursor([new Book { ImageLookup = new() { FailureReason = "old" } }]));
        var service = new ImageRequeueService(database.Object, Options.Create(new BackgroundImageLookupConfiguration()));
        Assert.ThrowsAsync<OperationCanceledException>(async () => await service.RequeueAsync("books",
            new("operation", DateTime.UtcNow), new CancellationToken(true)));
        Assert.That(updates, Is.Empty);
    }

    [Test]
    public async Task GetEntitiesAsync_ReservesNewSlotsAndSelectsDueRetries()
    {
        var options = new List<FindOptions<Book, Book>>();
        var queries = new List<BsonDocument>();
        var responses = new Queue<IAsyncCursor<Book>>([Cursor([new Book()]), Cursor([new Book()])]);
        collection.Setup(c => c.FindAsync(It.IsAny<FilterDefinition<Book>>(), It.IsAny<FindOptions<Book, Book>>(),
                It.IsAny<CancellationToken>()))
            .Callback<FilterDefinition<Book>, FindOptions<Book, Book>, CancellationToken>((f, o, _) =>
            {
                options.Add(o);
                queries.Add(Render(f));
            }).ReturnsAsync(() => responses.Dequeue());
        var worker = new BackgroundImageLookupService(Mock.Of<IServiceScopeFactory>(),
            Options.Create(new BackgroundImageLookupConfiguration()), NullLogger<BackgroundImageLookupService>.Instance);
        var method = typeof(BackgroundImageLookupService).GetMethod("GetEntitiesAsync", BindingFlags.NonPublic | BindingFlags.Instance)!
            .MakeGenericMethod(typeof(Book));
        await (Task)method.Invoke(worker, [database.Object, 2, CancellationToken.None])!;
        Assert.That(options[0].Limit, Is.EqualTo(1));
        Assert.That(options[1].Limit, Is.EqualTo(1));
        Assert.That(queries[0]["ImageLookup"], Is.EqualTo(BsonNull.Value));
        Assert.That(queries[1].ToJson(), Does.Contain("NextEligibleAt").And.Contain("$lte"));
    }

    [Test]
    public void ProcessEntityAsync_CancelledLookup_DoesNotWriteDefinitiveFailure()
    {
        var lookup = new Mock<IImageLookupService>();
        lookup.Setup(l => l.LookupAndSaveImageAsync(It.IsAny<IEntity>(), It.IsAny<MediaTypes>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());
        using var provider = new ServiceCollection().AddSingleton(lookup.Object).BuildServiceProvider();
        using var scope = provider.CreateScope();
        var worker = new BackgroundImageLookupService(provider.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new BackgroundImageLookupConfiguration()), NullLogger<BackgroundImageLookupService>.Instance);
        var method = typeof(BackgroundImageLookupService).GetMethod("ProcessEntityAsync", BindingFlags.NonPublic | BindingFlags.Instance)!;
        Assert.ThrowsAsync<OperationCanceledException>(async () => await (Task)method.Invoke(worker,
            [scope, new Book { Id = "000000000000000000000001" }, MediaTypes.Books, collection.Object, new CancellationToken(true)])!);
        Assert.That(updates, Is.Empty);
    }

    [Test]
    public async Task ProcessMediaTypeAsync_EmitsSeparateBacklogAndNewSuccessCounts()
    {
        var fresh = new Book { Id = "000000000000000000000001" };
        var backlog = new Book { Id = "000000000000000000000002", ImageLookup = new() { FailureReason = "old failure" } };
        collection.SetupSequence(c => c.FindAsync(It.IsAny<FilterDefinition<Book>>(),
                It.IsAny<FindOptions<Book, Book>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Cursor([fresh])).ReturnsAsync(Cursor([backlog]));
        var lookup = new Mock<IImageLookupService>();
        lookup.Setup(l => l.LookupAndSaveImageAsync(It.IsAny<IEntity>(), It.IsAny<MediaTypes>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ImageLookupResult(true, null, new Image(), null,
                Provider: "OpenLibrary", AttemptedStrategy: "Books:Isbn"));
        using var provider = new ServiceCollection().AddSingleton(lookup.Object).AddSingleton(database.Object).BuildServiceProvider();
        using var scope = provider.CreateScope();
        var logger = new Mock<ILogger<BackgroundImageLookupService>>();
        var messages = new List<string>();
        logger.Setup(l => l.Log(It.IsAny<LogLevel>(), It.IsAny<EventId>(), It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception?>(), It.IsAny<Func<It.IsAnyType, Exception?, string>>()))
            .Callback(new InvocationAction(i => messages.Add(i.Arguments[2].ToString()!)));
        var worker = new BackgroundImageLookupService(provider.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new BackgroundImageLookupConfiguration { RequestsPerMinute = int.MaxValue }), logger.Object);
        var method = typeof(BackgroundImageLookupService).GetMethod("ProcessMediaTypeAsync", BindingFlags.NonPublic | BindingFlags.Instance)!;
        await (Task)method.Invoke(worker, [scope, MediaTypes.Books, 2, DateTime.UtcNow, TimeSpan.FromMinutes(1), CancellationToken.None])!;
        Assert.That(messages, Has.Some.Contains("New processed: 1, New succeeded: 1; Backlog processed: 1, Backlog recovered: 1"));
        Assert.That(messages, Has.Some.Contains("Cohort: NewItem"));
        Assert.That(messages, Has.Some.Contains("Cohort: BacklogRecovery"));
        Assert.That(messages, Has.Some.Contains("Provider = OpenLibrary").And.Contains("Outcome = Success")
            .And.Contains("AttemptedStrategy = Books:Isbn").And.Contains("RetryEligible = False").And.Contains("LastAttempt ="));
        Assert.That(filters.All(f => f["CoverImage"] == BsonNull.Value), Is.True);
        Assert.That(updates.Count, Is.EqualTo(2));
    }
}
