namespace MediaSet.Api.Features.Plex.Models;

/// <summary>The kinds of seasons a TV entry covers.</summary>
public static class PlexScopeKinds
{
    /// <summary>One season (<see cref="PlexScope.From"/> and <see cref="PlexScope.To"/> are the same).</summary>
    public const string Season = "season";

    /// <summary>A consecutive range of seasons, From to To inclusive.</summary>
    public const string Range = "range";

    /// <summary>Every season of the show, including specials.</summary>
    public const string All = "all";

    /// <summary>Only returned by the title parser; never stored or accepted from a client.</summary>
    public const string Unreadable = "unreadable";
}

/// <summary>
/// Which seasons of a Plex show a TV entry covers. Stored on the Plex document of a linked TV entry,
/// and returned by the title parser.
/// </summary>
public record PlexScope(string Kind, int? From = null, int? To = null)
{
    public static readonly PlexScope Unreadable = new(PlexScopeKinds.Unreadable);
    public static readonly PlexScope AllSeasons = new(PlexScopeKinds.All);

    [System.Text.Json.Serialization.JsonIgnore]
    [MongoDB.Bson.Serialization.Attributes.BsonIgnore]
    public bool IsReadable => Kind != PlexScopeKinds.Unreadable;

    public static PlexScope Single(int season) => new(PlexScopeKinds.Season, season, season);

    /// <summary>A range from..to. One season when both are equal, unreadable when to is before from.</summary>
    public static PlexScope Between(int from, int to) =>
        to < from ? Unreadable : from == to ? Single(from) : new(PlexScopeKinds.Range, from, to);

    /// <summary>True when the season index is covered by this scope.</summary>
    public bool Covers(int seasonIndex) => Kind switch
    {
        PlexScopeKinds.All => true,
        PlexScopeKinds.Season or PlexScopeKinds.Range => seasonIndex >= From && seasonIndex <= To,
        _ => false
    };
}
