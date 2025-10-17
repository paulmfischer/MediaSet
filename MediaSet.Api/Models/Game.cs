using System.ComponentModel.DataAnnotations;
using MediaSet.Api.Attributes;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace MediaSet.Api.Models;

public class Game : IEntity
{
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

    // Publisher
    public string Publisher { get; set; } = string.Empty;

    // Genres
    [Upload(HeaderName = "Genre")]
    public List<string> Genres { get; set; } = [];

    // Game description/summary
    public string Description { get; set; } = string.Empty;
}
