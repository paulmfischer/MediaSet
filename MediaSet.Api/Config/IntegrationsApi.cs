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
            // Determine enabled integrations by checking config sections
            bool tmdb = configuration.GetSection("TmdbConfiguration").Exists();
            bool openLibrary = configuration.GetSection("OpenLibraryConfiguration").Exists();
            bool upcItemDb = configuration.GetSection("UpcItemDbConfiguration").Exists();
            bool giantBomb = configuration.GetSection("GiantBombConfiguration").Exists();
            bool musicBrainz = configuration.GetSection("MusicBrainzConfiguration").Exists();

            var result = new[]
            {
                new IntegrationAttributionDto
                {
                    Key = "tmdb",
                    DisplayName = "TMDB",
                    Enabled = tmdb,
                    AttributionUrl = "https://www.themoviedb.org/",
                    LogoPath = "/integrations/tmdb.svg"
                },
                new IntegrationAttributionDto
                {
                    Key = "openlibrary",
                    DisplayName = "OpenLibrary",
                    Enabled = openLibrary,
                    AttributionUrl = "https://openlibrary.org/",
                    LogoPath = "/integrations/openlibrary.svg"
                },
                new IntegrationAttributionDto
                {
                    Key = "upcitemdb",
                    DisplayName = "UPCitemdb",
                    Enabled = upcItemDb,
                    AttributionUrl = "https://upcitemdb.com/",
                    LogoPath = "/integrations/upcitemdb.svg"
                },
                new IntegrationAttributionDto
                {
                    Key = "giantbomb",
                    DisplayName = "GiantBomb",
                    Enabled = giantBomb,
                    AttributionUrl = "https://www.giantbomb.com/",
                    LogoPath = "/integrations/giantbomb.svg"
                },
                new IntegrationAttributionDto
                {
                    Key = "musicbrainz",
                    DisplayName = "MusicBrainz",
                    Enabled = musicBrainz,
                    AttributionUrl = "https://musicbrainz.org/",
                    LogoPath = "/integrations/musicbrainz.svg"
                }
            };

            return Results.Ok(result);
        });

        return group;
    }
}
