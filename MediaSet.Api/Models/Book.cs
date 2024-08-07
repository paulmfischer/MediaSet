using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace MediaSet.Api.Models;

public class Book
{
  [BsonId]
  [BsonRepresentation(BsonType.ObjectId)]
  public string? Id { get; set; }

  public string Title { get; set; } = null!;

  public string ISBN { get; set; } = null!;

  public string Format { get; set; } = null!;

  public int? Pages { get; set; }

  public string PublicationDate { get; set; } = null!;

  public List<string> Author { get; set; } = [];

  public List<string> Publisher { get; set; } = [];

  public List<string> Genre { get; set; } = [];

  public string Plot { get; set; } = null!;

  public string Subtitle { get; set; } = null!;
}