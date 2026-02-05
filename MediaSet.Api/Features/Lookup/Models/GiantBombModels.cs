using System.Text.Json.Serialization;

namespace MediaSet.Api.Features.Lookup.Models;

public record GiantBombSearchResponse(
    [property: JsonPropertyName("status_code")] int StatusCode,
    [property: JsonPropertyName("error")] string Error,
    [property: JsonPropertyName("number_of_total_results")] int TotalResults,
    [property: JsonPropertyName("results")] List<GiantBombSearchResult> Results
);

public record GiantBombSearchResult(
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("original_release_date")] string? OriginalReleaseDate,
    [property: JsonPropertyName("deck")] string? Deck,
    [property: JsonPropertyName("api_detail_url")] string ApiDetailUrl
);

public record GiantBombDetailsResponse(
    [property: JsonPropertyName("status_code")] int StatusCode,
    [property: JsonPropertyName("error")] string Error,
    [property: JsonPropertyName("results")] GiantBombGameDetails Results
);

public record GiantBombGameDetails(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("genres")] List<GiantBombNameRef>? Genres,
    [property: JsonPropertyName("developers")] List<GiantBombCompanyRef>? Developers,
    [property: JsonPropertyName("publishers")] List<GiantBombCompanyRef>? Publishers,
    [property: JsonPropertyName("platforms")] List<GiantBombPlatformRef>? Platforms,
    [property: JsonPropertyName("original_release_date")] string? OriginalReleaseDate,
    [property: JsonPropertyName("description")] string? Description,
    [property: JsonPropertyName("deck")] string? Deck,
    [property: JsonPropertyName("original_game_rating")] List<GiantBombRatingRef>? Ratings,
    [property: JsonPropertyName("image")] GiantBombImage? Image = null
);

public record GiantBombNameRef([property: JsonPropertyName("name")] string Name);
public record GiantBombCompanyRef([property: JsonPropertyName("name")] string Name);
public record GiantBombRatingRef([property: JsonPropertyName("name")] string Name);
public record GiantBombPlatformRef([property: JsonPropertyName("name")] string Name, [property: JsonPropertyName("abbreviation")] string? Abbreviation);
public record GiantBombImage(
    [property: JsonPropertyName("small_url")] string? SmallUrl,
    [property: JsonPropertyName("medium_url")] string? MediumUrl,
    [property: JsonPropertyName("super_url")] string? SuperUrl
);
