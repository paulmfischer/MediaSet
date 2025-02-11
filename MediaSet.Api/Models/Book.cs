using System.ComponentModel.DataAnnotations;
using MediaSet.Api.Attributes;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace MediaSet.Api.Models;

public class Book : IEntity
{
  [BsonId]
  [BsonRepresentation(BsonType.ObjectId)]
  public string? Id { get; set; }
  
  [BsonIgnore]
  public MediaTypes Type { get; set; }

  [Required]
  public string Title { get; set; } = string.Empty;

  public string ISBN { get; set; } = string.Empty;

  public string Format { get; set; } = string.Empty;

  public int? Pages { get; set; }

  [Upload(HeaderName = "Publication Date")]
  public string PublicationDate { get; set; } = string.Empty;

  [Upload(HeaderName = "Author")]
  public List<string> Authors { get; set; } = [];

  public string Publisher { get; set; } = string.Empty;

  [Upload(HeaderName = "Genre")]
  public List<string> Genres { get; set; } = [];

  public string Plot { get; set; } = string.Empty;

  public string Subtitle { get; set; } = string.Empty;
}