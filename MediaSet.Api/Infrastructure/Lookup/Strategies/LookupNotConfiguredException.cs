namespace MediaSet.Api.Infrastructure.Lookup.Strategies;

/// <summary>
/// Thrown when a lookup is requested but no lookup provider configuration section is set.
/// </summary>
public class LookupNotConfiguredException() : InvalidOperationException(DefaultMessage)
{
    public const string DefaultMessage =
        "Lookup is not available because no lookup provider is configured. " +
        "Set at least one of the configuration sections OpenLibraryConfiguration, TmdbConfiguration or MusicBrainzConfiguration.";
}
