using System.ComponentModel.DataAnnotations;
using MediaSet.Api.Attributes;
using MediaSet.Api.Converters;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace MediaSet.Api.Models;

public class Movie : IEntity
{
  [BsonId]
  [BsonRepresentation(BsonType.ObjectId)]
  public string? Id { get; set; }

  [Required]
  public string Title { get; set; } = string.Empty;

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
}