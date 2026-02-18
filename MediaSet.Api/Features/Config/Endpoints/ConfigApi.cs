using MediaSet.Api.Features.Config.Models;

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
            var igdbSection = configuration.GetSection("IgdbConfiguration");
            var musicBrainzSection = configuration.GetSection("MusicBrainzConfiguration");

            IntegrationAttributionDto Build(string key, string displayName, IConfigurationSection section, string defaultLogo, string? defaultAttributionUrl = null, string? defaultAttributionText = null)
            {
                var enabled = section.Exists();
                return new IntegrationAttributionDto
                {
                    Key = key,
                    DisplayName = displayName,
                    Enabled = enabled,
                    AttributionUrl = enabled ? (section.GetValue<string>("AttributionUrl") ?? defaultAttributionUrl) : null,
                    AttributionText = enabled ? (section.GetValue<string>("AttributionText") ?? defaultAttributionText) : null,
                    LogoPath = enabled ? section.GetValue<string>("LogoPath") ?? defaultLogo : null
                };
            }

            var result = new[]
            {
                Build("tmdb", "TMDB", tmdbSection, "/integrations/tmdb.svg",
                    defaultAttributionUrl: "https://www.themoviedb.org/",
                    defaultAttributionText: "This product uses the TMDB API but is not endorsed or certified by TMDB."),
                Build("openlibrary", "OpenLibrary", openLibrarySection, "/integrations/openlibrary.svg"),
                Build("upcitemdb", "UPCitemdb", upcItemDbSection, "/integrations/upcitemdb.png"),
                Build("igdb", "IGDB", igdbSection, "/integrations/igdb.svg",
                    defaultAttributionUrl: "https://www.igdb.com/",
                    defaultAttributionText: "Game data provided by IGDB."),
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
            var igdbSection = configuration.GetSection("IgdbConfiguration");
            var musicBrainzSection = configuration.GetSection("MusicBrainzConfiguration");

            // Calculate lookup availability based on required integrations
            // This matches the logic in Program.cs lines 154-169
            var capabilities = new LookupCapabilitiesDto
            {
                SupportsBookLookup = openLibrarySection.Exists() && upcItemDbSection.Exists(),
                SupportsMovieLookup = upcItemDbSection.Exists() && tmdbSection.Exists(),
                SupportsGameLookup = upcItemDbSection.Exists() && igdbSection.Exists()
                    && !string.IsNullOrEmpty(igdbSection.GetValue<string>("ClientId"))
                    && !string.IsNullOrEmpty(igdbSection.GetValue<string>("ClientSecret")),
                SupportsMusicLookup = musicBrainzSection.Exists()
            };

            return Results.Ok(capabilities);
        });

        return group;
    }
}
