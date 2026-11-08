using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using MediaSet.Api.Infrastructure.Plex;
using MediaSet.Api.Tests.Features.Plex;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NUnit.Framework;

namespace MediaSet.Api.Tests.Infrastructure.Plex;

[TestFixture]
public class PlexClientRegistrationTests
{
    private const string Token = "super-secret-token-value";

    private sealed class CapturingLoggerProvider : ILoggerProvider
    {
        public List<string> Lines { get; } = [];

        public ILogger CreateLogger(string categoryName) => new CapturingLogger(categoryName, Lines);

        public void Dispose() { }

        private sealed class CapturingLogger(string category, List<string> lines) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                lock (lines)
                {
                    lines.Add($"{category} {formatter(state, exception)} {exception}");
                }
            }
        }
    }

    private static ServiceProvider Provider(Dictionary<string, string?> settings, FakePlexHandler? handler = null, CapturingLoggerProvider? logs = null)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        var services = new ServiceCollection();
        services.AddLogging(b => b.SetMinimumLevel(LogLevel.Trace).AddProvider(logs ?? new CapturingLoggerProvider()));
        services.AddPlexClient(config.GetSection("PlexConfiguration"));
        if (handler != null)
        {
            // The last primary handler registered wins, which swaps the network for the fake
            services.AddHttpClient(nameof(IPlexClient)).ConfigurePrimaryHttpMessageHandler(() => handler);
        }

        return services.BuildServiceProvider();
    }

    private static Dictionary<string, string?> Settings(string? timeout = null, bool acceptInvalid = false) => new()
    {
        ["PlexConfiguration:BaseUrl"] = "http://plex.local:32400",
        ["PlexConfiguration:Token"] = Token,
        ["PlexConfiguration:Timeout"] = timeout ?? "30",
        ["PlexConfiguration:AcceptInvalidCertificate"] = acceptInvalid.ToString()
    };

    [Test]
    public void CreateClient_TimeoutConfigured_UsesConfiguredSeconds()
    {
        using var provider = Provider(Settings(timeout: "7"));

        var http = provider.GetRequiredService<IHttpClientFactory>().CreateClient(nameof(IPlexClient));

        Assert.That(http.Timeout, Is.EqualTo(TimeSpan.FromSeconds(7)));
        Assert.That(http.BaseAddress, Is.EqualTo(new Uri("http://plex.local:32400/")));
    }

    [Test]
    public void CreatePrimaryHandler_AcceptInvalidCertificateTrue_InstallsPermissiveValidation()
    {
        using var handler = PlexServiceCollectionExtensions.CreatePrimaryHandler(new PlexConfiguration { AcceptInvalidCertificate = true });

        Assert.That(handler.SslOptions.RemoteCertificateValidationCallback, Is.Not.Null);
        Assert.That(handler.SslOptions.RemoteCertificateValidationCallback!(this, null, null, default), Is.True);
    }

    [Test]
    public void CreatePrimaryHandler_AcceptInvalidCertificateFalse_KeepsDefaultValidation()
    {
        using var handler = PlexServiceCollectionExtensions.CreatePrimaryHandler(new PlexConfiguration());

        Assert.That(handler.SslOptions.RemoteCertificateValidationCallback, Is.Null);
    }

    [Test]
    public async Task Client_Request_SendsTokenInHeaderAndNotInUrl()
    {
        var handler = FakePlexHandler.WithServer();
        using var provider = Provider(Settings(), handler);

        await provider.GetRequiredService<IPlexClient>().GetLibrariesAsync(CancellationToken.None);

        var request = handler.Requests.Single();
        Assert.That(request.Headers["X-Plex-Token"], Is.EqualTo(Token));
        Assert.That(request.Uri.ToString(), Does.Not.Contain(Token));
        Assert.That(request.Headers["Accept"], Is.EqualTo("application/json"));
    }

    [Test]
    public async Task Client_RequestsAtTraceLevel_LogOutputNeverContainsToken()
    {
        var handler = FakePlexHandler.WithServer().Route("/library/sections/1/all", "movie.json");
        var logs = new CapturingLoggerProvider();
        using var provider = Provider(Settings(), handler, logs);
        var client = provider.GetRequiredService<IPlexClient>();

        await client.GetItemsAsync(new PlexLibrary("1", "Movies", "movie"), "matrix", CancellationToken.None);

        Assert.That(logs.Lines, Is.Not.Empty);
        Assert.That(string.Join('\n', logs.Lines), Does.Not.Contain(Token));
    }

    [Test]
    public async Task Client_ServerError_LoggedExceptionDoesNotContainToken()
    {
        var handler = new FakePlexHandler().Route("/library/sections",
            _ => new HttpResponseMessage(System.Net.HttpStatusCode.InternalServerError));
        var logs = new CapturingLoggerProvider();
        using var provider = Provider(Settings(), handler, logs);

        var ex = (await Assert.ThrowsAsync<PlexClientException>(() => provider.GetRequiredService<IPlexClient>().GetLibrariesAsync(CancellationToken.None)))!;

        Assert.That(ex.ToString(), Does.Not.Contain(Token));
        Assert.That(string.Join('\n', logs.Lines), Does.Not.Contain(Token));
    }
}
