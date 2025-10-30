namespace MediaSet.Api.Models;

public record GameResponse(
    string Title,
    string Platform,
    List<string> Genres,
    List<string> Developers,
    List<string> Publishers,
    string ReleaseDate,
    string Rating,
    string Description,
    string Format
);
