namespace MediaSet.Api.Models;

public record DiscResponse(
    int TrackNumber,
    string Title,
    int? Duration
);
