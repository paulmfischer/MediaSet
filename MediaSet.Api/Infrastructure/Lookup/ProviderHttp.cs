using System.Collections.Concurrent;
using System.Text.Json;

namespace MediaSet.Api.Infrastructure.Lookup;

/// <summary>Preserves provider status and reset headers before legacy clients consume responses.</summary>
public static class ProviderHttp
{
    private static readonly ConcurrentDictionary<string, DateTime> resets = new();

    public static async Task<HttpResponseMessage> SendProviderAsync(this HttpClient client, HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var uri = request.RequestUri!.IsAbsoluteUri ? request.RequestUri : new Uri(client.BaseAddress!, request.RequestUri);
        var provider = uri.Host;
        LookupFailureScope.Attempt(provider);
        if (LookupFailureScope.IsActive && resets.TryGetValue(provider, out var reset) && reset > DateTime.UtcNow)
        {
            var error = new ProviderLookupException(provider, Shared.Models.ImageLookupOutcome.QuotaLimited,
                "Provider quota deferred until reset", reset);
            LookupFailureScope.Record(error);
            throw error;
        }
        var response = await client.SendAsync(request, cancellationToken);
        if (LookupFailureScope.IsActive && response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
        {
            resets[provider] = LookupFailureScope.RetryAt(response) ?? DateTime.UtcNow.AddMinutes(5);
        }
        if (!response.IsSuccessStatusCode && response.StatusCode != System.Net.HttpStatusCode.NotFound)
        {
            LookupFailureScope.Record(new ProviderLookupException(provider, LookupFailureScope.Classify(response.StatusCode),
                "Lookup provider request failed", response.StatusCode == System.Net.HttpStatusCode.TooManyRequests
                    ? resets.GetValueOrDefault(provider) : LookupFailureScope.RetryAt(response)));
        }
        return response;
    }

    public static async Task<HttpResponseMessage> GetProviderAsync(this HttpClient client, string url,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        return await client.SendProviderAsync(request, cancellationToken);
    }

    public static async Task<T?> GetProviderFromJsonAsync<T>(this HttpClient client, string url,
        JsonSerializerOptions? options, CancellationToken cancellationToken)
    {
        using var response = await client.GetProviderAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<T>(options, cancellationToken);
    }

    public static Task<T?> GetProviderFromJsonAsync<T>(this HttpClient client, string url,
        CancellationToken cancellationToken) => client.GetProviderFromJsonAsync<T>(url, null, cancellationToken);
}
