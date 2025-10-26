namespace MediaSet.Api.Models;

public record MovieLookupResponse(
    string Title,
    List<string> Studios,
    List<string> Genres,
    string ReleaseDate,
    string Rating,
    int? Runtime,
    string Plot,
    string? Format,
    bool IsTvSeries
);
