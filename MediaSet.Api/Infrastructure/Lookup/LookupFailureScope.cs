namespace MediaSet.Api.Infrastructure.Lookup;

/// <summary>
/// Carries provider failures through legacy clients that return empty results on errors.
/// Scoped to one background lookup; foreground client behavior remains unchanged.
/// </summary>
public sealed class LookupFailureScope : IDisposable
{
    private static readonly AsyncLocal<LookupFailureScope?> current = new();
    private readonly LookupFailureScope? previous = current.Value;
    private ProviderLookupException? failure;
    private readonly object failureLock = new();
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, byte> providers = new();
    public string? AttemptedProviders => providers.IsEmpty ? null : string.Join(", ", providers.Keys.Order());
    public static bool IsActive => current.Value != null;

    public LookupFailureScope() => current.Value = this;

    public static void Attempt(string provider) => current.Value?.providers.TryAdd(provider, 0);

    public static void Record(Exception exception, [System.Runtime.CompilerServices.CallerFilePath] string caller = "")
    {
        if (exception is HttpRequestException { StatusCode: System.Net.HttpStatusCode.NotFound })
        {
            return;
        }
        if (current.Value is { } scope)
        {
            var typed = exception as ProviderLookupException ?? new ProviderLookupException(
                Path.GetFileNameWithoutExtension(caller).Replace("Client", ""),
                exception is HttpRequestException http ? Classify(http.StatusCode) : Shared.Models.ImageLookupOutcome.ProviderUnavailable,
                exception.Message);
            Attempt(typed.Provider);
            lock (scope.failureLock)
            {
                if (scope.failure == null || typed.RetryAt > scope.failure.RetryAt ||
                    (typed.Outcome == Shared.Models.ImageLookupOutcome.QuotaLimited && scope.failure.Outcome != typed.Outcome))
                {
                    scope.failure = typed;
                }
            }
        }
    }

    public static void RecordStatus(HttpResponseMessage response, [System.Runtime.CompilerServices.CallerFilePath] string caller = "")
    {
        if (!response.IsSuccessStatusCode && response.StatusCode != System.Net.HttpStatusCode.NotFound)
        {
            Record(new ProviderLookupException(Path.GetFileNameWithoutExtension(caller).Replace("Client", ""),
                Classify(response.StatusCode), "Lookup provider request failed", RetryAt(response)), caller);
        }
    }

    public void ThrowIfFailed()
    {
        if (failure != null)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
        }
    }

    public void Dispose() => current.Value = previous;

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
