namespace MediaSet.Api.Features.Lookup.Models;

public record DiscResponse(
    int TrackNumber,
    string Title,
    int? Duration
);
