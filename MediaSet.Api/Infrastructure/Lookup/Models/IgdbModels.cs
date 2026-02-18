using System.Text.Json.Serialization;

namespace MediaSet.Api.Infrastructure.Lookup.Models;

public record IgdbGame(
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("summary")] string? Summary,
    [property: JsonPropertyName("first_release_date")] long? FirstReleaseDate,
    [property: JsonPropertyName("genres")] List<IgdbNamedObject>? Genres,
    [property: JsonPropertyName("involved_companies")] List<IgdbInvolvedCompany>? InvolvedCompanies,
    [property: JsonPropertyName("platforms")] List<IgdbPlatform>? Platforms,
    [property: JsonPropertyName("age_ratings")] List<IgdbAgeRating>? AgeRatings,
    [property: JsonPropertyName("cover")] IgdbCover? Cover
);

public record IgdbNamedObject(
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("name")] string Name
);

public record IgdbInvolvedCompany(
    [property: JsonPropertyName("company")] IgdbNamedObject? Company,
    [property: JsonPropertyName("developer")] bool Developer,
    [property: JsonPropertyName("publisher")] bool Publisher
);

public record IgdbPlatform(
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("abbreviation")] string? Abbreviation
);

public record IgdbAgeRating(
    [property: JsonPropertyName("category")] int Category,
    [property: JsonPropertyName("rating")] int Rating
);

public record IgdbCover(
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("url")] string? Url
);

public record IgdbTokenResponse(
    [property: JsonPropertyName("access_token")] string AccessToken,
    [property: JsonPropertyName("expires_in")] int ExpiresIn,
    [property: JsonPropertyName("token_type")] string TokenType
);
