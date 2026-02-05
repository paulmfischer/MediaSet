
using System.ComponentModel.DataAnnotations;
using MediaSet.Api.Attributes;
using MediaSet.Api.Infrastructure.Storage;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace MediaSet.Api.Models;

public class Game : IEntity
{
    public Game()
    {
        Type = MediaTypes.Games;
    }

    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    [BsonIgnore]
    public MediaTypes Type { get; set; }

    [Required]
    public string Title { get; set; } = string.Empty;

    // Physical/digital format (e.g., Disc, Cartridge, Digital)
    public string Format { get; set; } = string.Empty;

    // Common retail identifier
    [LookupIdentifier]
    public string Barcode { get; set; } = string.Empty;

    // Release date as free-form string to match existing pattern
    [Upload(HeaderName = "Release Date")]
    public string ReleaseDate { get; set; } = string.Empty;

    // Age rating, e.g., ESRB: E, T, M
    [Upload(HeaderName = "Audience Rating")]
    public string Rating { get; set; } = string.Empty;

    // Platform
    public string Platform { get; set; } = string.Empty;

    // Studios/Developers
    [Upload(HeaderName = "Developer")]
    public List<string> Developers { get; set; } = [];

    // Publishers
    [Upload(HeaderName = "Publisher")]
    public List<string> Publishers { get; set; } = [];

    // Genres
    [Upload(HeaderName = "Genre")]
    public List<string> Genres { get; set; } = [];

    // Game description/summary
    public string Description { get; set; } = string.Empty;

    public string? ImageUrl { get; set; }

    public Image? CoverImage { get; set; }

    public ImageLookup? ImageLookup { get; set; }

    public bool IsEmpty()
    {
        return string.IsNullOrWhiteSpace(Title)
            && string.IsNullOrWhiteSpace(Format)
            && string.IsNullOrWhiteSpace(Barcode)
            && string.IsNullOrWhiteSpace(ReleaseDate)
            && string.IsNullOrWhiteSpace(Rating)
            && Publishers.Count == 0
            && string.IsNullOrWhiteSpace(Platform)
            && Developers.Count == 0
            && Genres.Count == 0
            && string.IsNullOrWhiteSpace(Description)
            && CoverImage == null;
    }
}
