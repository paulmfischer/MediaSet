using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MediaSet.Api.Tests.Features.Plex;

/// <summary>Serves saved Plex JSON fixtures by request path and records every request. Never touches a network.</summary>
public sealed class FakePlexHandler : HttpMessageHandler
{
    private readonly Dictionary<string, Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>>> _routes = [];

    public List<RecordedRequest> Requests { get; } = [];

    public static string Fixture(string name) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Features", "Plex", "Fixtures", name));

    public static HttpResponseMessage Json(string body, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    public FakePlexHandler Route(string path, string fixture) =>
        Route(path, _ => Json(Fixture(fixture)));

    public FakePlexHandler Route(string path, Func<HttpRequestMessage, HttpResponseMessage> respond) =>
        Route(path, (request, _) => Task.FromResult(respond(request)));

    public FakePlexHandler Route(string path, Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond)
    {
        _routes[path] = respond;
        return this;
    }

    /// <summary>Libraries and server identity fixtures every client test needs.</summary>
    public static FakePlexHandler WithServer() =>
        new FakePlexHandler().Route("/library/sections", "libraries.json").Route("/identity", "identity.json");

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Add(new RecordedRequest(
            request.Method,
            request.RequestUri!,
            request.Headers.ToDictionary(h => h.Key, h => string.Join(",", h.Value), StringComparer.OrdinalIgnoreCase)));

        return _routes.TryGetValue(request.RequestUri!.AbsolutePath, out var respond)
            ? respond(request, cancellationToken)
            : Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
    }
}

public record RecordedRequest(HttpMethod Method, Uri Uri, Dictionary<string, string> Headers);
