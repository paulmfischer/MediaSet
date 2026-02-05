using MediaSet.Api.Features.Config.Models;
using MediaSet.Api.Features.Entities.Models;
using MediaSet.Api.Models;
using Microsoft.Extensions.Configuration;

namespace MediaSet.Api.Features.Config.Endpoints;

internal static class ConfigApi
{
    public static RouteGroupBuilder MapConfig(this IEndpointRouteBuilder routes)
    {
        var logger = ((WebApplication)routes).Logger;
        var group = routes.MapGroup("/config");

        group.WithTags("Config");

        // GET /config/integrations
        group.MapGet("/integrations", (IConfiguration configuration) =>
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

        // GET /config/lookup-capabilities
        group.MapGet("/lookup-capabilities", (IConfiguration configuration) =>
        {
            // Read configuration sections (same pattern as Program.cs)
            var tmdbSection = configuration.GetSection("TmdbConfiguration");
            var openLibrarySection = configuration.GetSection("OpenLibraryConfiguration");
            var upcItemDbSection = configuration.GetSection("UpcItemDbConfiguration");
            var giantBombSection = configuration.GetSection("GiantBombConfiguration");
            var musicBrainzSection = configuration.GetSection("MusicBrainzConfiguration");

            // Calculate lookup availability based on required integrations
            // This matches the logic in Program.cs lines 154-169
            var capabilities = new LookupCapabilitiesDto
            {
                SupportsBookLookup = openLibrarySection.Exists() && upcItemDbSection.Exists(),
                SupportsMovieLookup = upcItemDbSection.Exists() && tmdbSection.Exists(),
                SupportsGameLookup = upcItemDbSection.Exists() && giantBombSection.Exists(),
                SupportsMusicLookup = musicBrainzSection.Exists()
            };

            return Results.Ok(capabilities);
        });

        return group;
    }
}
