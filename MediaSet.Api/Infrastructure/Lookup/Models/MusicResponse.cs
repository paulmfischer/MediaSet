namespace MediaSet.Api.Infrastructure.Lookup.Models;

public record MusicResponse(
    string Title,
    string Artist,
    string ReleaseDate,
    List<string> Genres,
    int? Duration,
    string Label,
    int? Tracks,
    int? Discs,
    List<DiscResponse> DiscList,
    string Format,
    string? ImageUrl = null
);
