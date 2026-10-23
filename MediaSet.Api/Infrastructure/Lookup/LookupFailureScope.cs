namespace MediaSet.Api.Infrastructure.Lookup;

/// <summary>
/// Carries provider failures through legacy clients that return empty results on errors.
/// Scoped to one background lookup; foreground client behavior remains unchanged.
/// </summary>
public sealed class LookupFailureScope : IDisposable
{
    private static readonly AsyncLocal<LookupFailureScope?> current = new();
    private readonly LookupFailureScope? previous = current.Value;
    private Exception? failure;

    public LookupFailureScope() => current.Value = this;

    public static void Record(Exception exception)
    {
        if (exception is HttpRequestException { StatusCode: System.Net.HttpStatusCode.NotFound })
        {
            return;
        }
        if (current.Value is { } scope)
        {
            scope.failure ??= exception;
        }
    }

    public static void RecordStatus(HttpResponseMessage response)
    {
        if (!response.IsSuccessStatusCode && response.StatusCode != System.Net.HttpStatusCode.NotFound)
        {
            Record(new HttpRequestException("Lookup provider request failed", null, response.StatusCode));
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
}
