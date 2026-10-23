using System.Text.Json.Serialization;

namespace MediaSet.Api.Infrastructure.Lookup.Models;

public record TmdbTvSearchResponse(
    [property: JsonPropertyName("results")] List<TmdbTvSearchResult> Results);

public record TmdbTvSearchResult(
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("name")] string Name);

public record TmdbTvResponse(
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("first_air_date")] string? FirstAirDate,
    [property: JsonPropertyName("overview")] string? Overview,
    [property: JsonPropertyName("poster_path")] string? PosterPath,
    [property: JsonPropertyName("genres")] List<TmdbGenre>? Genres = null,
    [property: JsonPropertyName("production_companies")] List<TmdbProductionCompany>? ProductionCompanies = null);

public record TmdbSeasonResponse(
    [property: JsonPropertyName("season_number")] int SeasonNumber,
    [property: JsonPropertyName("air_date")] string? AirDate,
    [property: JsonPropertyName("poster_path")] string? PosterPath,
    [property: JsonPropertyName("episodes")] List<TmdbEpisode>? Episodes = null);

public record TmdbEpisode(
    [property: JsonPropertyName("episode_number")] int EpisodeNumber,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("air_date")] string? AirDate);
