
using System.ComponentModel.DataAnnotations;
using MediaSet.Api.Attributes;
using MediaSet.Api.Infrastructure.Serialization;
using MediaSet.Api.Infrastructure.Storage;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace MediaSet.Api.Models;

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
    public string Title { get; set; } = string.Empty;

    [LookupIdentifier]
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

    public string? ImageUrl { get; set; }

    public Image? CoverImage { get; set; }

    public ImageLookup? ImageLookup { get; set; }

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
            && CoverImage == null;
    }
}
