using System.ComponentModel.DataAnnotations;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace MediaSet.Api.Models;

public class Movie : IEntity
{
  [BsonId]
  [BsonRepresentation(BsonType.ObjectId)]
  public string? Id { get; set; }

  [Required]
  public string Title { get; set; } = null!;

  public string ISBN { get; set; } = null!;

  public string Format { get; set; } = null!;

  public string ReleaseDate { get; set; } = null!;

  public string Rating { get; set; } = null!;
  public int? Runtime { get; set; } = null!;

  public List<string> Studio { get; set; } = [];

  public List<string> Genres { get; set; } = [];

  public string Plot { get; set; } = null!;
}