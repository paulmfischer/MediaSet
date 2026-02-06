namespace MediaSet.Api.Infrastructure.Lookup.Models;

public record DiscResponse(
    int TrackNumber,
    string Title,
    int? Duration
);
