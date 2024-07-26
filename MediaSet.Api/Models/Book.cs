using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace MediaSet.Api.Models;

public class Book
{
  [BsonId]
  [BsonRepresentation(BsonType.ObjectId)]
  public string? Id { get; set; }

  public string Title { get; set; } = null!;

  public string Genre { get; set; } = null!;

  public string Author { get; set; } = null!;

  public string Format { get; set; } = null!;
}