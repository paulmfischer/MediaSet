using System.Diagnostics;
using System.Net;
using System.Text.Json;
using MediaSet.Api.Features.Images.Models;
using MediaSet.Api.Features.Images.Services;
using MediaSet.Api.Infrastructure.Database;
using MediaSet.Api.Infrastructure.Lookup.Clients.OpenLibrary;
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

[TestFixture]
[NonParallelizable]
public sealed class RegressionSuite
{
    private const string MongoImage = "docker.io/library/mongo:8.0.17";
    private readonly string _databaseName = "image_regression_" + Guid.NewGuid().ToString("N");
    private readonly string _images = Environment.GetEnvironmentVariable("IMAGE_REGRESSION_IMAGES")
        ?? Path.Combine(Path.GetTempPath(), "mediaset-images-" + Guid.NewGuid().ToString("N"));
    private readonly string _container = Environment.GetEnvironmentVariable("IMAGE_REGRESSION_CONTAINER")
        ?? ("mediaset-image-regression-" + Guid.NewGuid().ToString("N"));
    private readonly string _runtime = Environment.GetEnvironmentVariable("IMAGE_REGRESSION_RUNTIME") ?? "podman";
    private Func<string, CancellationToken, Task<IPAddress[]>>? _originalDns;
    private DatabaseService _database = null!;
    private bool _started;

    internal static void Progress(string action, object? details)
    {
        var directory = Environment.GetEnvironmentVariable("IMAGE_REGRESSION_ARTIFACTS") ?? TestContext.CurrentContext.WorkDirectory;
        Directory.CreateDirectory(directory);
        File.AppendAllText(Path.Combine(directory, "progress.jsonl"),
            JsonSerializer.Serialize(new { utc = DateTimeOffset.UtcNow, @event = action, scenarioId = TestContext.CurrentContext.Test.Name, details }) + "\n");
    }

    [OneTimeSetUp]
    public async Task StartAsync()
    {
        try
        {
            Progress("mongo-starting", new { _container, MongoImage, _databaseName });
            // Always create a new container; an existing name is an error, never a reuse path.
            await CommandAsync("run", "--detach", "--name", _container, "--label", "mediaset.image-regression=true",
                "--publish", "127.0.0.1::27017", MongoImage);
            _started = true;
            var binding = (await CommandAsync("port", _container, "27017/tcp")).Trim();
            if (!binding.StartsWith("127.0.0.1:", StringComparison.Ordinal) || !int.TryParse(binding.Split(':')[1], out var port))
            {
                throw new InvalidOperationException("Mongo container must bind exclusively to loopback");
            }
            var connection = $"mongodb://127.0.0.1:{port}/?directConnection=true&serverSelectionTimeoutMS=1000";
            var client = new MongoClient(connection);
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
            while (true)
            {
                try
                {
                    await client.GetDatabase(_databaseName).RunCommandAsync<MongoDB.Bson.BsonDocument>(
                        new MongoDB.Bson.BsonDocument("ping", 1), cancellationToken: timeout.Token);
                    break;
                }
                catch (TimeoutException) when (!timeout.IsCancellationRequested)
                {
                    await Task.Delay(100, timeout.Token);
                }
            }
            _database = new DatabaseService(Options.Create(new MediaSetDatabaseSettings
            {
                ConnectionString = connection,
                DatabaseName = _databaseName
            }));
            _originalDns = SsrfGuard._dnsResolver;
            SsrfGuard._dnsResolver = (_, ct) =>
            {
                ct.ThrowIfCancellationRequested();
                return Task.FromResult(new[] { IPAddress.Parse("93.184.216.34") });
            };
            Progress("mongo-ready", new { _container, _databaseName });
        }
        catch (Exception ex)
        {
            Progress("harness-failure", ex.ToString());
            await CleanupAsync();
            throw;
        }
    }

    [SetUp]
    public async Task ResetAsync()
    {
        await _database.GetCollection<Book>().DeleteManyAsync(Builders<Book>.Filter.Empty);
        await _database.GetCollection<Movie>().DeleteManyAsync(Builders<Movie>.Filter.Empty);
        await _database.GetCollection<Game>().DeleteManyAsync(Builders<Game>.Filter.Empty);
        await _database.GetCollection<Music>().DeleteManyAsync(Builders<Music>.Filter.Empty);
        if (Directory.Exists(_images))
        {
            Directory.Delete(_images, recursive: true);
        }
        Progress("scenario-start", TestContext.CurrentContext.Test.Name);
    }

    [TearDown]
    public void RecordResult() => Progress("scenario-finished", new
    {
        id = TestContext.CurrentContext.Test.Name,
        status = TestContext.CurrentContext.Result.Outcome.Status.ToString(),
        failure = TestContext.CurrentContext.Result.Message
    });

    [Test]
    public async Task BookSuccess_ExactIsbn_SavesReadableImageAndStateAsync()
    {
        using var fixtures = new StrictHttpFixtures(Progress);
        fixtures.Load(Path.Combine(TestContext.CurrentContext.TestDirectory, "Fixtures", "book-success.json"));
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddProvider(new ProgressLoggerProvider()));
        services.AddSingleton<IDatabaseService>(_database);
        services.AddScoped<LookupStrategyFactory>();
        services.AddSingleton<IOpenLibraryClient>(sp => new OpenLibraryClient(
            new HttpClient(fixtures, false) { BaseAddress = new Uri("https://openlibrary.org/") }, sp.GetRequiredService<ILogger<OpenLibraryClient>>()));
        services.AddSingleton<IUpcItemDbClient>(sp => new UpcItemDbClient(
            new HttpClient(fixtures, false) { BaseAddress = new Uri("https://api.upcitemdb.com/") },
            Options.Create(new UpcItemDbConfiguration()), sp.GetRequiredService<ILogger<UpcItemDbClient>>()));
        services.AddScoped<ILookupStrategy<BookResponse>, BookLookupStrategy>();
        services.AddSingleton<IImageStorageProvider>(sp => new LocalFileStorageProvider(_images, sp.GetRequiredService<ILogger<LocalFileStorageProvider>>()));
        services.AddSingleton<IImageService>(sp => new ImageService(sp.GetRequiredService<IImageStorageProvider>(),
            Options.Create(new ImageConfiguration()), new HttpClient(fixtures, false), sp.GetRequiredService<ILogger<ImageService>>()));
        services.AddScoped<IImageLookupService, ImageLookupService>();
        using var provider = services.BuildServiceProvider();
        var clock = new ScenarioTimeProvider();
        var config = Options.Create(new BackgroundImageLookupConfiguration { BatchSize = 1, RequestsPerMinute = int.MaxValue });
        using var processor = new BackgroundImageLookupService(provider.GetRequiredService<IServiceScopeFactory>(),
            config, provider.GetRequiredService<ILogger<BackgroundImageLookupService>>(), clock);
        const string id = "000000000000000000000001";
        await _database.GetCollection<Book>().InsertOneAsync(new Book { Id = id, Title = "Fixture Book", ISBN = "9780000000001" });

        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        await processor.ProcessAllEntityTypesAsync(deadline.Token);

        fixtures.VerifyComplete();
        var saved = await _database.GetCollection<Book>().Find(b => b.Id == id).SingleAsync(deadline.Token);
        Assert.That(saved.CoverImage, Is.Not.Null);
        Assert.That(saved.ImageLookup, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(saved.ImageLookup!.LastOutcome, Is.EqualTo(ImageLookupOutcome.Success));
            Assert.That(saved.ImageLookup.Provider, Is.EqualTo("OpenLibrary"));
            Assert.That(saved.ImageLookup.AttemptCount, Is.EqualTo(1));
            Assert.That(saved.ImageLookup.LookupAttemptedAt, Is.EqualTo(clock.GetUtcNow().UtcDateTime));
            Assert.That(saved.ImageLookup.NextEligibleAt, Is.Null);
            Assert.That(saved.ImageLookup.FailureReason, Is.Null);
        });
        await using var stream = await provider.GetRequiredService<IImageService>().GetImageStreamAsync(saved.CoverImage!.FilePath, deadline.Token);
        Assert.That(stream, Is.Not.Null);
        using var bitmap = SKBitmap.Decode(stream!);
        Assert.That(bitmap, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(bitmap.Width, Is.EqualTo(2));
            Assert.That(bitmap.Height, Is.EqualTo(2));
            Assert.That(File.Exists(Path.Combine(_images, saved.CoverImage.FilePath)), Is.True);
        });
    }

    [OneTimeTearDown]
    public async Task CleanupAsync()
    {
        if (_originalDns != null)
        {
            SsrfGuard._dnsResolver = _originalDns;
        }
        try
        {
            if (_started)
            {
                await CommandAsync("rm", "--force", "--volumes", _container);
                _started = false;
                Progress("mongo-removed", _container);
            }
        }
        finally
        {
            if (Directory.Exists(_images))
            {
                Directory.Delete(_images, recursive: true);
            }
            Progress("images-removed", _images);
        }
    }

    private async Task<string> CommandAsync(params string[] arguments)
    {
        if (_runtime is not ("podman" or "docker"))
        {
            throw new InvalidOperationException("Only local podman or docker runtimes are supported");
        }
        var info = new ProcessStartInfo(_runtime) { RedirectStandardOutput = true, RedirectStandardError = true };
        if (_runtime == "podman")
        {
            info.ArgumentList.Add("--remote=false");
        }
        else
        {
            var endpoint = Environment.GetEnvironmentVariable("DOCKER_HOST") ?? "unix:///var/run/docker.sock";
            if (!endpoint.StartsWith("unix:///", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Docker must use a local Unix socket");
            }
            info.ArgumentList.Add("--host");
            info.ArgumentList.Add(endpoint);
        }
        foreach (var argument in arguments)
        {
            info.ArgumentList.Add(argument);
        }
        using var process = Process.Start(info) ?? throw new InvalidOperationException("Cannot start container runtime");
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(3));
        try
        {
            await process.WaitForExitAsync(timeout.Token);
        }
        catch
        {
            process.Kill(entireProcessTree: true);
            throw;
        }
        var output = await stdout;
        var error = await stderr;
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"Container runtime {arguments[0]} failed: {error}");
        }
        return output;
    }
}

internal sealed class ScenarioTimeProvider : TimeProvider
{
    internal DateTimeOffset Now { get; set; } = new(2026, 1, 2, 3, 4, 5, TimeSpan.Zero);
    public override DateTimeOffset GetUtcNow() => Now;
}

internal sealed class ProgressLoggerProvider : ILoggerProvider
{
    public ILogger CreateLogger(string categoryName) => new ProgressLogger(categoryName);
    public void Dispose() { }

    private sealed class ProgressLogger(string category) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter) => RegressionSuite.Progress("application-log",
                new { category, level = logLevel.ToString(), message = formatter(state, exception), exception = exception?.ToString() });
    }
}
