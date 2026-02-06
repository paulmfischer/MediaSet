namespace MediaSet.Api.Infrastructure.Lookup.Models;

public record MovieResponse(
    string Title,
    List<string> Genres,
    List<string> Studios,
    string ReleaseDate,
    string Rating,
    int? Runtime,
    string Plot,
    string Format,
    string? ImageUrl = null
);
