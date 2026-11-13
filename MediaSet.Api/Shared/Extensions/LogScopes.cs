using MediaSet.Api.Shared.Models;

namespace MediaSet.Api.Shared.Extensions;

/// <summary>Values of the <c>Component</c> log property: the feature an event belongs to.</summary>
public static class LogComponents
{
    public const string Plex = "Plex";
    public const string Lookup = "Lookup";
    public const string Images = "Images";
    public const string Stats = "Stats";
}

/// <summary>Values of the <c>Provider</c> log property: the upstream service an event concerns.</summary>
public static class LogProviders
{
    public const string Tmdb = "TMDB";
    public const string Igdb = "IGDB";
    public const string OpenLibrary = "OpenLibrary";
    public const string MusicBrainz = "MusicBrainz";
    public const string UpcItemDb = "UpcItemDb";
    public const string GoogleBooks = "GoogleBooks";

    /// <summary>
    /// The provider value for a client or provenance name ("Tmdb", "MusicBrainz", "OpenLibrary"), case-insensitive;
    /// null for anything else (a media type, "ImageDownload", "Unknown").
    /// </summary>
    public static string? Normalize(string? name) => name?.Trim().ToLowerInvariant() switch
    {
        "tmdb" => Tmdb,
        "igdb" => Igdb,
        "openlibrary" => OpenLibrary,
        "musicbrainz" => MusicBrainz,
        "upcitemdb" => UpcItemDb,
        "googlebooks" => GoogleBooks,
        _ => null
    };
}

/// <summary>
/// Logger scopes that add the <c>Component</c>, <c>Provider</c> and <c>MediaType</c> properties to every event
/// logged inside them (Serilog turns scope dictionaries into event properties). An inner scope wins over an outer one.
/// </summary>
public static class LogScopes
{
    public const string ComponentProperty = "Component";
    public const string ProviderProperty = "Provider";
    public const string MediaTypeProperty = "MediaType";
    public const string ApplicationProperty = "Application";

    /// <summary>Application of a Plex sync started by its schedule, like the background image processor's.</summary>
    public const string PlexSyncProcessorApplication = "MediaSet.Api.Processor.PlexSync";

    public static IDisposable? BeginComponentScope(this ILogger logger, string component, MediaTypes? mediaType = null)
    {
        var properties = new Dictionary<string, object?> { [ComponentProperty] = component };
        if (mediaType is { } type)
        {
            properties[MediaTypeProperty] = type.ToString();
        }
        return logger.BeginScope(properties);
    }

    /// <summary>A Provider scope; null (no scope) when the name is not a known provider.</summary>
    public static IDisposable? BeginProviderScope(this ILogger logger, string? provider) =>
        LogProviders.Normalize(provider) is { } name
            ? logger.BeginScope(new Dictionary<string, object?> { [ProviderProperty] = name })
            : null;

    /// <summary>Tags every endpoint of the group with the Component (and the request span's child events).</summary>
    public static RouteGroupBuilder WithLogComponent(this RouteGroupBuilder group, string component)
    {
        group.AddEndpointFilter(async (context, next) =>
        {
            var logger = context.HttpContext.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(LogScopes).FullName!);
            using (logger.BeginComponentScope(component))
            {
                return await next(context);
            }
        });
        return group;
    }
}
