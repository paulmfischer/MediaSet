namespace MediaSet.Api.Infrastructure.Lookup.Strategies;

/// <summary>
/// Thrown when a lookup is requested but no lookup provider configuration section is set.
/// </summary>
public class LookupNotConfiguredException() : InvalidOperationException(DefaultMessage)
{
    public const string DefaultMessage =
        "Lookup is not available because no lookup provider is configured. " +
        "Set MusicBrainzConfiguration (music), or UpcItemDbConfiguration together with OpenLibraryConfiguration (books), " +
        "TmdbConfiguration (movies) or IgdbConfiguration with ClientId and ClientSecret (games).";
}
