using System.Collections.Concurrent;
using System.Text.Json;
using MediaSet.Api.Shared.Extensions;
using Serilog.Context;

namespace MediaSet.Api.Infrastructure.Lookup;

/// <summary>Preserves provider status and reset headers before legacy clients consume responses.</summary>
public static class ProviderHttp
{
    private static readonly ConcurrentDictionary<string, DateTime> _resets = new();

    /// <summary>The tracked quota reset for a host, or null when none is known or it has passed.</summary>
    public static DateTime? ResetFor(string host) =>
        _resets.TryGetValue(host, out var reset) && reset > DateTime.UtcNow ? reset : null;

    private static readonly HttpRequestOptionsKey<string> _hostKey = new("MediaSet.ProviderHost");

    /// <summary>The tracked quota reset for the provider host that produced a response sent via <see cref="SendProviderAsync"/>.</summary>
    public static DateTime? ResetFor(HttpResponseMessage response) =>
        response.RequestMessage is { } request && request.Options.TryGetValue(_hostKey, out var host) ? ResetFor(host) : null;

    public static async Task<HttpResponseMessage> SendProviderAsync(this HttpClient client, HttpRequestMessage request,
        CancellationToken cancellationToken, [System.Runtime.CompilerServices.CallerFilePath] string caller = "")
    {
        var uri = request.RequestUri!.IsAbsoluteUri ? request.RequestUri : new Uri(client.BaseAddress!, request.RequestUri);
        // Quota deferral is keyed by host; errors and attempts use the friendly provider name from the calling client.
        var host = uri.Host;
        request.Options.Set(_hostKey, host);
        var provider = LookupFailureScope.ProviderName(caller);
        LookupFailureScope.Attempt(provider);
        if (LookupFailureScope.IsActive && _resets.TryGetValue(host, out var reset) && reset > DateTime.UtcNow)
        {
            var error = new ProviderLookupException(provider, Shared.Models.ImageLookupOutcome.QuotaLimited,
                "Provider quota deferred until reset", reset);
            LookupFailureScope.Record(error);
            throw error;
        }
        HttpResponseMessage response;
        // No logger here: the Provider goes on the log context, so the HTTP client span and handler events carry it
        using var providerProperty = LogProviders.Normalize(provider) is { } providerName
            ? LogContext.PushProperty(LogScopes.ProviderProperty, providerName)
            : null;
        try
        {
            response = await client.SendAsync(request, cancellationToken);
            response.RequestMessage ??= request;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or TimeoutException && !cancellationToken.IsCancellationRequested)
        {
            LookupFailureScope.Stamp(ex, provider);
            throw;
        }
        if (LookupFailureScope.IsActive && response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
        {
            _resets[host] = LookupFailureScope.RetryAt(response) ?? DateTime.UtcNow.AddMinutes(5);
        }
        if (!response.IsSuccessStatusCode && response.StatusCode != System.Net.HttpStatusCode.NotFound)
        {
            LookupFailureScope.Record(new ProviderLookupException(provider, LookupFailureScope.Classify(response.StatusCode),
                "Lookup provider request failed", response.StatusCode == System.Net.HttpStatusCode.TooManyRequests
                    ? _resets.GetValueOrDefault(host) : LookupFailureScope.RetryAt(response)));
        }
        return response;
    }

    public static async Task<HttpResponseMessage> GetProviderAsync(this HttpClient client, string url,
        CancellationToken cancellationToken, [System.Runtime.CompilerServices.CallerFilePath] string caller = "")
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        return await client.SendProviderAsync(request, cancellationToken, caller);
    }

    public static async Task<T?> GetProviderFromJsonAsync<T>(this HttpClient client, string url,
        JsonSerializerOptions? options, CancellationToken cancellationToken,
        [System.Runtime.CompilerServices.CallerFilePath] string caller = "")
    {
        using var response = await client.GetProviderAsync(url, cancellationToken, caller);
        try
        {
            response.EnsureSuccessStatusCode();
        }
        catch (HttpRequestException ex)
        {
            LookupFailureScope.Stamp(ex, LookupFailureScope.ProviderName(caller));
            throw;
        }
        return await response.Content.ReadFromJsonAsync<T>(options, cancellationToken);
    }

    public static Task<T?> GetProviderFromJsonAsync<T>(this HttpClient client, string url,
        CancellationToken cancellationToken, [System.Runtime.CompilerServices.CallerFilePath] string caller = "") =>
        client.GetProviderFromJsonAsync<T>(url, null, cancellationToken, caller);
}
