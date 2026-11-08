namespace MediaSet.Api.Infrastructure.Plex;

/// <summary>
/// Startup result of validating the Plex configuration. Registered only when the Plex
/// integration is configured and valid. Holds no secrets.
/// </summary>
public sealed class PlexEnablement(IEnumerable<string> enabledEntityTypes)
{
    private readonly HashSet<string> _enabled = new(enabledEntityTypes, StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<string> EnabledEntityTypes =>
        PlexEntityTypes.Supported.Where(_enabled.Contains).ToList();

    public bool IsEnabled(string entityType) => _enabled.Contains(entityType);
}
