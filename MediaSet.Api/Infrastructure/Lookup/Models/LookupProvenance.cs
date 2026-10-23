namespace MediaSet.Api.Infrastructure.Lookup.Models;

public record LookupProvenance(string Provider, string ProviderId,
    string Title, IReadOnlyList<string>? Platforms = null, IReadOnlyList<string>? MatchYears = null,
    string? ArtworkScope = null);
