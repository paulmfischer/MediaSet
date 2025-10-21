using System.ComponentModel.DataAnnotations;
using MediaSet.Api.Attributes;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace MediaSet.Api.Models;

public class Music : IEntity
{
  [BsonId]
  [BsonRepresentation(BsonType.ObjectId)]
  public string? Id { get; set; }

  [BsonIgnore]
  public MediaTypes Type { get; set; }

  [Required]
  public string Title { get; set; } = string.Empty;

  public string Format { get; set; } = string.Empty;

  [Required]
  public string Artist { get; set; } = string.Empty;

  [Upload(HeaderName = "Release Date")]
  public string ReleaseDate { get; set; } = string.Empty;

  [Upload(HeaderName = "Genre")]
  public List<string> Genres { get; set; } = [];

  public int? Duration { get; set; }

  public string Label { get; set; } = string.Empty;

  public string Barcode { get; set; } = string.Empty;

  public int? Tracks { get; set; }
  public int? Discs { get; set; }
  public List<Disc> DiscList { get; set; } = [];
}

public class Disc
{
  public int TrackNumber { get; set; }
  public string Title { get; set; } = string.Empty;
  public string Duration { get; set; } = string.Empty;
}
