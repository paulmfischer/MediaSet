namespace MediaSet.Api.Infrastructure.Plex;

public static class PlexEntityTypes
{
    public const string Movies = "movies";

    /// <summary>Entity types Plex supports, in the order they are reported.</summary>
    public static readonly IReadOnlyList<string> Supported = [Movies];

    /// <summary>Returns the canonical (lowercase) supported entity type, or null when unknown.</summary>
    public static string? Normalize(string? entityType) =>
        Supported.FirstOrDefault(s => string.Equals(s, entityType?.Trim(), StringComparison.OrdinalIgnoreCase));

    /// <summary>The Plex library type that holds an entity type's items, or null when unsupported.</summary>
    public static string? LibraryType(string? entityType) =>
        Normalize(entityType) == Movies ? "movie" : null;
}
