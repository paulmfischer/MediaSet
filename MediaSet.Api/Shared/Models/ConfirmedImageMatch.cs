namespace MediaSet.Api.Shared.Models;

/// <summary>A provider identity confirmed independently of artwork availability.</summary>
public record ConfirmedImageMatch(
    string Provider,
    string ProviderId,
    string Title,
    string? ImageUrl,
    string MetadataFingerprint,
    string Rationale,
    string ArtworkScope);
