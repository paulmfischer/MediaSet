namespace MediaSet.Api.Infrastructure.Lookup.Models;

public record LookupProvenance(string Provider, string ProviderId,
    string Title, IReadOnlyList<string>? Platforms = null, IReadOnlyList<string>? MatchYears = null,
    string? ArtworkScope = null,
    // Artwork to try when the identity's own cover is missing; it also names a wider identity that
    // different provider records share (a MusicBrainz release group groups a release's editions).
    string? FallbackImageUrl = null,
    // Other names the provider knows the title by (IGDB regional or alternative names).
    IReadOnlyList<string>? AlternativeTitles = null);
