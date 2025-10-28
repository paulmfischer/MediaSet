using System.Text.Json.Serialization;

namespace MediaSet.Api.Models;

public record TmdbSearchResponse(
    [property: JsonPropertyName("page")]
    int Page,

    [property: JsonPropertyName("results")]
    List<TmdbMovieSearchResult> Results,

    [property: JsonPropertyName("total_pages")]
    int TotalPages,

    [property: JsonPropertyName("total_results")]
    int TotalResults
);

public record TmdbMovieSearchResult(
    [property: JsonPropertyName("id")]
    int Id,

    [property: JsonPropertyName("title")]
    string Title,

    [property: JsonPropertyName("release_date")]
    string? ReleaseDate,

    [property: JsonPropertyName("overview")]
    string? Overview
);

public record TmdbMovieResponse(
    [property: JsonPropertyName("id")]
    int Id,

    [property: JsonPropertyName("title")]
    string Title,

    [property: JsonPropertyName("overview")]
    string? Overview,

    [property: JsonPropertyName("release_date")]
    string? ReleaseDate,

    [property: JsonPropertyName("runtime")]
    int? Runtime,

    [property: JsonPropertyName("genres")]
    List<TmdbGenre> Genres,

    [property: JsonPropertyName("production_companies")]
    List<TmdbProductionCompany> ProductionCompanies,

    [property: JsonPropertyName("vote_average")]
    double VoteAverage
);

public record TmdbGenre(
    [property: JsonPropertyName("id")]
    int Id,

    [property: JsonPropertyName("name")]
    string Name
);

public record TmdbProductionCompany(
    [property: JsonPropertyName("id")]
    int Id,

    [property: JsonPropertyName("name")]
    string Name
);
