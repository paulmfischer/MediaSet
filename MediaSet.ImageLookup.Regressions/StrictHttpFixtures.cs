using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace MediaSet.ImageLookup.Regressions;

// No delegating handler or sockets: every request terminates here, including auth and artwork.
internal sealed class StrictHttpFixtures(Action<string, object?> progress) : HttpMessageHandler, IHttpClientFactory
{
    private readonly List<Fixture> _fixtures = [];
    internal List<string> RequestIds { get; } = [];
    internal Action<string>? RequestStarted { get; set; }
    private readonly List<string> _violations = [];
    internal void Load(string path)
    {
        _fixtures.AddRange(JsonSerializer.Deserialize<List<Fixture>>(File.ReadAllText(path))!);
    }

    internal void Add(Fixture fixture) => _fixtures.Add(fixture);
    public HttpClient CreateClient(string name) => new(this, disposeHandler: false);

    internal void VerifyComplete()
    {
        var unused = _fixtures.Where(f => f.Responses.Count > 0).Select(f => f.Id);
        var problems = _violations.Concat(unused.Select(id => $"Unused responses: {id}")).ToArray();
        if (problems.Length > 0)
        {
            throw new InvalidOperationException(string.Join("; ", problems));
        }
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var uri = request.RequestUri!;
        var body = request.Content == null ? "" : await request.Content.ReadAsStringAsync(cancellationToken);
        var fixture = _fixtures.SingleOrDefault(f => f.Method == request.Method.Method && f.Host == uri.Authority
            && f.Path == uri.AbsolutePath && f.Query == uri.Query && f.Body == body && f.Responses.Count > 0
            && f.RequestHeaders.All(header => request.Headers.TryGetValues(header.Key, out var values)
                && values.SequenceEqual(new[] { header.Value })));
        if (fixture == null)
        {
            var message = $"Unexpected HTTP request: {request.Method} {uri.GetLeftPart(UriPartial.Path)}{uri.Query}";
            _violations.Add(message);
            progress("http-unexpected", message);
            throw new InvalidOperationException(message);
        }
        RequestIds.Add(fixture.Id);
        var response = fixture.Responses[0];
        fixture.Responses.RemoveAt(0);
        progress("http-fixture", new { fixture.Id, response.Status, response.Failure });
        RequestStarted?.Invoke(fixture.Id);
        if (response.Failure == "network")
        {
            throw new HttpRequestException($"Fixture {fixture.Id}: connection failure");
        }
        if (response.Failure == "timeout")
        {
            throw new TaskCanceledException($"Fixture {fixture.Id}: timeout", new TimeoutException());
        }
        if (response.Failure == "wait-for-cancellation")
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        }
        cancellationToken.ThrowIfCancellationRequested();
        var result = new HttpResponseMessage((HttpStatusCode)response.Status)
        {
            Content = new ByteArrayContent(response.Base64 == null ? Encoding.UTF8.GetBytes(response.Text ?? "") : Convert.FromBase64String(response.Base64))
        };
        result.Content.Headers.ContentType = new MediaTypeHeaderValue(response.ContentType);
        foreach (var header in response.Headers)
        {
            if (!result.Headers.TryAddWithoutValidation(header.Key, header.Value))
            {
                result.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
        }
        return result;
    }
}

internal sealed class Fixture
{
    public string Id { get; set; } = "";
    public string Method { get; set; } = "GET";
    public string Host { get; set; } = "";
    public string Path { get; set; } = "";
    public string Query { get; set; } = "";
    public string Body { get; set; } = "";
    public Dictionary<string, string> RequestHeaders { get; set; } = [];
    public List<FixtureResponse> Responses { get; set; } = [];
}

internal sealed class FixtureResponse
{
    public int Status { get; set; } = 200;
    public string ContentType { get; set; } = "application/json";
    public string? Text { get; set; }
    public string? Base64 { get; set; }
    public string? Failure { get; set; }
    public Dictionary<string, string> Headers { get; set; } = [];
}
