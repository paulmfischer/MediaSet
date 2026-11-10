using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using MediaSet.Api.Infrastructure.Serialization;
using MediaSet.Api.Shared.Attributes;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace MediaSet.Api.Shared.Models;

public class Movie : IEntity
{
    public Movie()
    {
        Type = MediaTypes.Movies;
    }

    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    [BsonIgnore]
    public MediaTypes Type { get; set; }

    [Required]
    [LookupProperty]
    [LookupIdentifier(2, IdentifierType.Entity)]
    public string Title { get; set; } = string.Empty;

    [LookupIdentifier(1, IdentifierType.Upc)]
    public string Barcode { get; set; } = string.Empty;

    public string Format { get; set; } = string.Empty;

    [Upload(HeaderName = "Release Date")]
    public string ReleaseDate { get; set; } = string.Empty;

    [Upload(HeaderName = "Audience Rating")]
    public string Rating { get; set; } = string.Empty;

    [Upload(Converter = typeof(RuntimeConverter))]
    public int? Runtime { get; set; } = null!;

    public List<string> Studios { get; set; } = [];

    public List<string> Genres { get; set; } = [];

    public string Plot { get; set; } = string.Empty;

    [Upload(HeaderName = "Is TV Series", Converter = typeof(BoolConverter))]
    public bool IsTvSeries { get; set; }

    /// <summary>
    /// Optional external identifier in <c>source:value</c> form, e.g. <c>tmdbid:1234</c>.
    /// </summary>
    public string? ExternalId { get; set; }

    public string? ImageUrl { get; set; }

    public Image? CoverImage { get; set; }

    public ImageLookup? ImageLookup { get; set; }

    /// <summary>
    /// Stored Plex data (a Plex item DTO without file paths; typed object so Shared does not depend on the Plex feature), filled only by the single movie lookup when the movie is linked to Plex.
    /// Never stored on the movie and ignored on input, so edits never touch the Plex data.
    /// </summary>
    [BsonIgnore]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? Plex { get; set; }

    public bool IsEmpty()
    {
        return string.IsNullOrWhiteSpace(Title)
            && string.IsNullOrWhiteSpace(Barcode)
            && string.IsNullOrWhiteSpace(Format)
            && string.IsNullOrWhiteSpace(ReleaseDate)
            && string.IsNullOrWhiteSpace(Rating)
            && !Runtime.HasValue
            && Studios.Count == 0
            && Genres.Count == 0
            && string.IsNullOrWhiteSpace(Plot)
            && string.IsNullOrWhiteSpace(ExternalId)
            && CoverImage == null;
    }
}
