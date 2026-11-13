using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using MediaSet.Api.Infrastructure.Lookup;
using MediaSet.Api.Shared.Extensions;
using MediaSet.Api.Shared.Models;
using Microsoft.Extensions.Logging;
using NUnit.Framework;
using Serilog;
using Serilog.Extensions.Logging;

namespace MediaSet.Api.Tests.Observability;

[TestFixture]
public class LogComponentTests
{
    private static (Serilog.ILogger Logger, ListLogSink Sink) CreateLogger()
    {
        var sink = new ListLogSink();
        var logger = new LoggerConfiguration()
            .MinimumLevel.Verbose()
            .Enrich.FromLogContext()
            .Enrich.With<LogComponentEnricher>()
            .WriteTo.Sink(sink)
            .CreateLogger();
        return (logger, sink);
    }

    [TestCase("MediaSet.Api.Features.Plex.Services.PlexSyncService", "Plex")]
    [TestCase("MediaSet.Api.Infrastructure.Plex.PlexClient", "Plex")]
    [TestCase("MediaSet.Api.Features.Lookup.Endpoints.LookupProviderFailureFilter", "Lookup")]
    [TestCase("MediaSet.Api.Infrastructure.Lookup.Strategies.MovieLookupStrategy", "Lookup")]
    [TestCase("MediaSet.Api.Features.Images.Services.BackgroundImageLookupService", "Images")]
    [TestCase("MediaSet.Api.Infrastructure.Storage.ImageService", "Images")]
    [TestCase("MediaSet.Api.Features.Statistics.Services.StatsService", "Stats")]
    public void Enrich_FeatureSourceContext_AddsComponent(string sourceContext, string component)
    {
        var (logger, sink) = CreateLogger();

        logger.ForContext("SourceContext", sourceContext).Information("event");

        Assert.That(sink.Scalar(sink.Events.Single(), "Component"), Is.EqualTo(component));
    }

    [TestCase("MediaSet.Api.Infrastructure.Lookup.Clients.Tmdb.TmdbClient", "TMDB")]
    [TestCase("MediaSet.Api.Infrastructure.Lookup.Clients.Igdb.IgdbTokenService", "IGDB")]
    [TestCase("MediaSet.Api.Infrastructure.Lookup.Clients.OpenLibrary.OpenLibraryClient", "OpenLibrary")]
    [TestCase("MediaSet.Api.Infrastructure.Lookup.Clients.MusicBrainz.MusicBrainzClient", "MusicBrainz")]
    [TestCase("MediaSet.Api.Infrastructure.Lookup.Clients.UpcItemDb.UpcItemDbClient", "UpcItemDb")]
    public void Enrich_ClientSourceContext_AddsProviderAndLookupComponent(string sourceContext, string provider)
    {
        var (logger, sink) = CreateLogger();

        logger.ForContext("SourceContext", sourceContext).Information("event");

        var logEvent = sink.Events.Single();
        Assert.That(sink.Scalar(logEvent, "Provider"), Is.EqualTo(provider));
        Assert.That(sink.Scalar(logEvent, "Component"), Is.EqualTo("Lookup"));
    }

    [TestCase("MediaSet.Api.Features.Entities.Endpoints.EntityApi")]
    [TestCase("Microsoft.AspNetCore.Hosting.Diagnostics")]
    public void Enrich_OtherSourceContext_AddsNoComponentOrProvider(string sourceContext)
    {
        var (logger, sink) = CreateLogger();

        logger.ForContext("SourceContext", sourceContext).Information("event");

        var logEvent = sink.Events.Single();
        Assert.That(logEvent.Properties.ContainsKey("Component"), Is.False);
        Assert.That(logEvent.Properties.ContainsKey("Provider"), Is.False);
    }

    [Test]
    public void BeginComponentScope_ClientLogsInsideImagesScope_KeepScopeComponentAndAddMediaType()
    {
        var (serilog, sink) = CreateLogger();
        using var factory = new SerilogLoggerFactory(serilog);
        var images = factory.CreateLogger("MediaSet.Api.Features.Images.Services.ImageLookupService");
        var client = factory.CreateLogger("MediaSet.Api.Infrastructure.Lookup.Clients.Tmdb.TmdbClient");

        using (images.BeginComponentScope(LogComponents.Images, MediaTypes.Movies))
        {
            client.LogWarning("TMDB said no");
        }

        var logEvent = sink.Events.Single();
        Assert.That(sink.Scalar(logEvent, "Component"), Is.EqualTo("Images"));
        Assert.That(sink.Scalar(logEvent, "MediaType"), Is.EqualTo("Movies"));
        Assert.That(sink.Scalar(logEvent, "Provider"), Is.EqualTo("TMDB"));
    }

    [Test]
    public void BeginComponentScope_EventWrittenStraightToSerilog_CarriesComponent()
    {
        // HTTP spans are written by SerilogTracing to the Serilog logger, not through ILogger
        var (serilog, sink) = CreateLogger();
        using var factory = new SerilogLoggerFactory(serilog);
        var images = factory.CreateLogger("MediaSet.Api.Features.Images.Services.ImageLookupService");

        using (images.BeginComponentScope(LogComponents.Images))
        {
            serilog.Information("HTTP {RequestMethod} {RequestUri}", "GET", "https://covers.example.test/b/1.jpg");
        }

        Assert.That(sink.Scalar(sink.Events.Single(), "Component"), Is.EqualTo("Images"));
    }

    [TestCase("Tmdb", "TMDB")]
    [TestCase("musicbrainz", "MusicBrainz")]
    [TestCase("OpenLibrary", "OpenLibrary")]
    public void BeginProviderScope_KnownProviderName_AddsNormalizedProvider(string name, string expected)
    {
        var (serilog, sink) = CreateLogger();
        using var factory = new SerilogLoggerFactory(serilog);
        var logger = factory.CreateLogger("Test");

        using (logger.BeginProviderScope(name))
        {
            logger.LogInformation("event");
        }

        Assert.That(sink.Scalar(sink.Events.Single(), "Provider"), Is.EqualTo(expected));
    }

    [TestCase("ImageDownload")]
    [TestCase("Movies")]
    [TestCase(null)]
    public void BeginProviderScope_UnknownName_AddsNoProvider(string? name)
    {
        var (serilog, sink) = CreateLogger();
        using var factory = new SerilogLoggerFactory(serilog);
        var logger = factory.CreateLogger("Test");

        using (logger.BeginProviderScope(name))
        {
            logger.LogInformation("event");
        }

        Assert.That(sink.Events.Single().Properties.ContainsKey("Provider"), Is.False);
    }

    private sealed class LoggingHandler(Serilog.ILogger logger) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            logger.Information("HTTP {Method} {RequestUri}", request.Method, request.RequestUri);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }
    }

    [Test]
    public async Task SendProviderAsync_EventsDuringRequest_CarryProviderOfCallingClient()
    {
        var (serilog, sink) = CreateLogger();
        using var http = new HttpClient(new LoggingHandler(serilog)) { BaseAddress = new Uri("https://api.example.test/") };

        using var response = await http.GetProviderAsync("search", CancellationToken.None,
            "/src/Infrastructure/Lookup/Clients/MusicBrainz/MusicBrainzClient.cs");
        serilog.Information("after the request");

        var events = sink.Events.ToList();
        Assert.That(sink.Scalar(events[0], "Provider"), Is.EqualTo("MusicBrainz"));
        Assert.That(events[1].Properties.ContainsKey("Provider"), Is.False);
    }
}
