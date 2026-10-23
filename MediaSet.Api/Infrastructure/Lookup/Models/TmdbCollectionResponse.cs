using System.Text.Json.Serialization;

namespace MediaSet.Api.Infrastructure.Lookup.Models;

public record TmdbCollectionSearchResponse(
    [property: JsonPropertyName("results")] List<TmdbCollectionResponse> Results);

public record TmdbCollectionResponse(
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("poster_path")] string? PosterPath = null,
    [property: JsonPropertyName("overview")] string? Overview = null,
    [property: JsonPropertyName("parts")] List<TmdbMovieSearchResult>? Parts = null);
