using MediaSet.Api.Models;
using Microsoft.Extensions.Configuration;

namespace MediaSet.Api.Config;

internal static class IntegrationsApi
{
    public static RouteGroupBuilder MapIntegrations(this IEndpointRouteBuilder routes)
    {
        var logger = ((WebApplication)routes).Logger;
        var group = routes.MapGroup("/config/integrations");

        group.WithTags("Config");

        group.MapGet("/", (IConfiguration configuration) =>
        {
            // Build integration attribution entries from configuration so values can be changed at runtime
            var tmdbSection = configuration.GetSection("TmdbConfiguration");
            var openLibrarySection = configuration.GetSection("OpenLibraryConfiguration");
            var upcItemDbSection = configuration.GetSection("UpcItemDbConfiguration");
            var giantBombSection = configuration.GetSection("GiantBombConfiguration");
            var musicBrainzSection = configuration.GetSection("MusicBrainzConfiguration");

            IntegrationAttributionDto Build(string key, string displayName, IConfigurationSection section, string defaultLogo)
            {
                var enabled = section.Exists();
                return new IntegrationAttributionDto
                {
                    Key = key,
                    DisplayName = displayName,
                    Enabled = enabled,
                    AttributionUrl = enabled ? section.GetValue<string>("AttributionUrl") : null,
                    AttributionText = enabled ? section.GetValue<string>("AttributionText") : null,
                    LogoPath = enabled ? section.GetValue<string>("LogoPath") ?? defaultLogo : null
                };
            }

            var result = new[]
            {
                Build("tmdb", "TMDB", tmdbSection, "/integrations/tmdb.svg"),
                Build("openlibrary", "OpenLibrary", openLibrarySection, "/integrations/openlibrary.svg"),
                Build("upcitemdb", "UPCitemdb", upcItemDbSection, "/integrations/upcitemdb.png"),
                Build("giantbomb", "GiantBomb", giantBombSection, "/integrations/giantbomb.svg"),
                Build("musicbrainz", "MusicBrainz", musicBrainzSection, "/integrations/musicbrainz.svg")
            };

            return Results.Ok(result);
        });

        return group;
    }
}
