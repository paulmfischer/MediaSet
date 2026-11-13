namespace MediaSet.Api.Infrastructure.Lookup;

/// <summary>
/// Carries provider failures through legacy clients that return empty results on errors.
/// Scoped to one background lookup; foreground client behavior remains unchanged.
/// </summary>
public sealed class LookupFailureScope : IDisposable
{
    /// <summary>Key under which provider clients stamp their name on an exception (<see cref="Exception.Data"/>).</summary>
    public const string ProviderDataKey = "Provider";

    private static readonly AsyncLocal<LookupFailureScope?> _current = new();
    private readonly LookupFailureScope? _previous = _current.Value;
    private ProviderLookupException? _failure;
    private readonly object _failureLock = new();
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, byte> _providers = new();
    public string? AttemptedProviders => _providers.IsEmpty ? null : string.Join(", ", _providers.Keys.Order());
    public static bool IsActive => _current.Value != null;

    public LookupFailureScope() => _current.Value = this;

    public static void Attempt(string provider) => _current.Value?._providers.TryAdd(provider, 0);

    /// <summary>Provider name used when the caller is not a <c>*Client.cs</c> file.</summary>
    public const string UnknownProvider = "Unknown";

    private const string ClientSuffix = "Client";

    /// <summary>
    /// Friendly provider name ("MusicBrainz") from the calling client's source file name: only a trailing
    /// "Client" is removed. Any other file (or an empty path) yields <see cref="UnknownProvider"/>.
    /// </summary>
    public static string ProviderName(string callerFilePath)
    {
        var name = Path.GetFileNameWithoutExtension(callerFilePath);
        return name.Length > ClientSuffix.Length && name.EndsWith(ClientSuffix, StringComparison.Ordinal)
            ? name[..^ClientSuffix.Length]
            : UnknownProvider;
    }

    /// <summary>Names the provider on an exception so the lookup endpoint can report it; the first stamp wins.</summary>
    public static void Stamp(Exception exception, string provider)
    {
        if (exception is not ProviderLookupException)
        {
            exception.Data[ProviderDataKey] ??= provider;
        }
    }

    public static void Record(Exception exception, [System.Runtime.CompilerServices.CallerFilePath] string caller = "")
    {
        Stamp(exception, ProviderName(caller));
        if (exception is HttpRequestException { StatusCode: System.Net.HttpStatusCode.NotFound })
        {
            return;
        }
        if (_current.Value is { } scope)
        {
            var typed = exception as ProviderLookupException ?? new ProviderLookupException(
                ProviderName(caller),
                exception is HttpRequestException http ? Classify(http.StatusCode) : Shared.Models.ImageLookupOutcome.ProviderUnavailable,
                exception.Message);
            Attempt(typed.Provider);
            lock (scope._failureLock)
            {
                if (scope._failure == null || typed.RetryAt > scope._failure.RetryAt ||
                    (typed.Outcome == Shared.Models.ImageLookupOutcome.QuotaLimited && scope._failure.Outcome != typed.Outcome))
                {
                    scope._failure = typed;
                }
            }
        }
    }

    public static void RecordStatus(HttpResponseMessage response, [System.Runtime.CompilerServices.CallerFilePath] string caller = "")
    {
        if (!response.IsSuccessStatusCode && response.StatusCode != System.Net.HttpStatusCode.NotFound)
        {
            Record(new ProviderLookupException(ProviderName(caller),
                Classify(response.StatusCode), "Lookup provider request failed", RetryAt(response)), caller);
        }
    }

    public void ThrowIfFailed()
    {
        if (_failure != null)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(_failure).Throw();
        }
    }

    public void Dispose() => _current.Value = _previous;

    public static Shared.Models.ImageLookupOutcome Classify(System.Net.HttpStatusCode? status) => status switch
    {
        System.Net.HttpStatusCode.TooManyRequests => Shared.Models.ImageLookupOutcome.QuotaLimited,
        System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden => Shared.Models.ImageLookupOutcome.Unauthorized,
        System.Net.HttpStatusCode.NotFound => Shared.Models.ImageLookupOutcome.NoMatch,
        _ => Shared.Models.ImageLookupOutcome.ProviderUnavailable
    };

    public static DateTime? RetryAt(HttpResponseMessage response)
    {
        var now = DateTime.UtcNow;
        DateTime? retry = response.Headers.RetryAfter?.Date?.UtcDateTime;
        if (response.Headers.RetryAfter?.Delta is { } delta)
        {
            retry = now.Add(delta);
        }
        if (response.Headers.TryGetValues("X-RateLimit-Reset", out var values) &&
            long.TryParse(values.FirstOrDefault(), out var unix) && unix is >= 0 and <= 253402300799)
        {
            var reset = DateTimeOffset.FromUnixTimeSeconds(unix).UtcDateTime;
            if (reset > now && (retry == null || reset > retry))
            {
                retry = reset;
            }
        }
        return retry > now ? retry : null;
    }
}

public sealed class ProviderLookupException(string provider, Shared.Models.ImageLookupOutcome outcome,
    string message, DateTime? retryAt = null) : Exception(message)
{
    public string Provider { get; } = provider;
    public Shared.Models.ImageLookupOutcome Outcome { get; } = outcome;
    public DateTime? RetryAt { get; } = retryAt;
}

/// <summary>
/// A provider rate-limit response (429, or MusicBrainz's 503) surfaced as an <see cref="HttpRequestException"/> (so existing catch blocks keep working) that
/// also carries when the provider can be retried, from its Retry-After header or <see cref="ProviderHttp"/>
/// reset tracking. <see cref="RetryAt"/> is null when unknown.
/// </summary>
public sealed class ProviderRateLimitException(string message, DateTime? retryAt,
    System.Net.HttpStatusCode statusCode = System.Net.HttpStatusCode.TooManyRequests)
    : HttpRequestException(message, null, statusCode)
{
    public DateTime? RetryAt { get; } = retryAt;

    public static ProviderRateLimitException FromResponse(string message, HttpResponseMessage response)
    {
        var retryAt = LookupFailureScope.RetryAt(response);
        if (retryAt == null && response.RequestMessage?.RequestUri is { } uri)
        {
            retryAt = ProviderHttp.ResetFor(uri.Host);
        }
        return new ProviderRateLimitException(message, retryAt, response.StatusCode);
    }
}
