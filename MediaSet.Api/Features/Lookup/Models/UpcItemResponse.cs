using System.Text.Json.Serialization;

namespace MediaSet.Api.Features.Lookup.Models;

public record UpcItemResponse(
    [property: JsonPropertyName("code")]
    string Code,

    [property: JsonPropertyName("total")]
    int Total,

    [property: JsonPropertyName("items")]
    List<UpcItem> Items
);

public record UpcItem(
    [property: JsonPropertyName("ean")]
    string Ean,

    [property: JsonPropertyName("title")]
    string Title,

    [property: JsonPropertyName("description")]
    string? Description,

    [property: JsonPropertyName("category")]
    string? Category,

    [property: JsonPropertyName("brand")]
    string? Brand,

    [property: JsonPropertyName("model")]
    string? Model,

    [property: JsonPropertyName("isbn")]
    string? Isbn
);
