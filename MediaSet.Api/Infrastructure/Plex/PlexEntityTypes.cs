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
        Normalize(entityType) == Movies ? MovieItemType : null;

    public const string MovieItemType = "movie";
    public const string ShowItemType = "show";

    /// <summary>
    /// The Plex library types read for an entity type. Movie entries (and TV series entries, which are stored as
    /// movies) are matched against movie and show libraries.
    /// </summary>
    public static IReadOnlyList<string> LibraryTypes(string? entityType) =>
        Normalize(entityType) == Movies ? [MovieItemType, ShowItemType] : [];

    /// <summary>The Plex item type an entry links to: a show for a TV series entry, a movie otherwise.</summary>
    public static string ItemType(bool isTvSeries) => isTvSeries ? ShowItemType : MovieItemType;
}
