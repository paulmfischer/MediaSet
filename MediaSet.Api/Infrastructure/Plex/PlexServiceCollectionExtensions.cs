using System.Net.Security;
using Microsoft.Extensions.Options;

namespace MediaSet.Api.Infrastructure.Plex;

public static class PlexServiceCollectionExtensions
{
    public const string TokenHeader = "X-Plex-Token";

    /// <summary>
    /// Registers the read-only Plex client and the search service. The token is sent in the
    /// X-Plex-Token header so request URLs (which HttpClient logs) never contain it.
    /// </summary>
    public static IServiceCollection AddPlexClient(this IServiceCollection services, IConfiguration plexSection)
    {
        services.Configure<PlexConfiguration>(plexSection);
        services.AddHttpClient<IPlexClient, PlexClient>((serviceProvider, client) =>
                ConfigureClient(client, serviceProvider.GetRequiredService<IOptions<PlexConfiguration>>().Value))
            .RedactLoggedHeaders(name => string.Equals(name, TokenHeader, StringComparison.OrdinalIgnoreCase))
            .ConfigurePrimaryHttpMessageHandler(serviceProvider =>
                CreatePrimaryHandler(serviceProvider.GetRequiredService<IOptions<PlexConfiguration>>().Value));
        return services;
    }

    public static void ConfigureClient(HttpClient client, PlexConfiguration config)
    {
        client.BaseAddress = new Uri(config.BaseUrl.TrimEnd('/') + "/");
        client.Timeout = TimeSpan.FromSeconds(config.Timeout > 0 ? config.Timeout : 30);
        client.DefaultRequestHeaders.TryAddWithoutValidation(TokenHeader, config.Token);
    }

    public static SocketsHttpHandler CreatePrimaryHandler(PlexConfiguration config)
    {
        var handler = new SocketsHttpHandler();
        if (config.AcceptInvalidCertificate)
        {
            handler.SslOptions = new SslClientAuthenticationOptions
            {
                RemoteCertificateValidationCallback = (_, _, _, _) => true
            };
        }

        return handler;
    }
}
