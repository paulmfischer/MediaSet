namespace MediaSet.Api.Features.Plex.Models;

/// <summary>The values of the movie list's <c>plex</c> filter.</summary>
public static class PlexListFilters
{
    public const string OnPlex = "onPlex";
    public const string NotOnPlex = "notOnPlex";
    public const string Unwatched = "unwatched";
    public const string Broken = "broken";

    /// <summary>The canonical spelling of a filter value (case-insensitive), or null when it is not one.</summary>
    public static string? Normalize(string? value) =>
        new[] { OnPlex, NotOnPlex, Unwatched, Broken }
            .FirstOrDefault(f => string.Equals(f, value, StringComparison.OrdinalIgnoreCase));
}

/// <summary>
/// The small Plex summary attached to each movie of the paged search. It never carries a file path. A movie that is
/// not linked has no summary. For a TV entry Watched and Resolution come from the entry's rollup.
/// </summary>
public record PlexListSummary(bool OnPlex, string? Resolution, bool Watched, bool Broken)
{
    public static PlexListSummary From(PlexItemData d) => new(d.OnPlex, d.Resolution, d.Watched, d.Broken);
}
