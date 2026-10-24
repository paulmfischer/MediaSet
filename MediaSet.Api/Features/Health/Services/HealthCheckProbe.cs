namespace MediaSet.Api.Features.Health.Services;

public static class HealthCheckProbe
{
    public static async Task<int> RunAsync(
        HttpClient client,
        Uri endpoint,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await client.GetAsync(
                endpoint, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                return 0;
            }

            Console.Error.WriteLine($"Healthcheck failed: HTTP {(int)response.StatusCode}");
        }
        catch (HttpRequestException)
        {
            Console.Error.WriteLine("Healthcheck failed: API is unreachable");
        }
        catch (OperationCanceledException)
        {
            Console.Error.WriteLine("Healthcheck failed: request timed out or was canceled");
        }

        return 1;
    }
}
