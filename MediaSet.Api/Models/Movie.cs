using System.ComponentModel.DataAnnotations;
using MediaSet.Api.Attributes;
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

  [Upload(HeaderName = "Barcode")]
  public string ISBN { get; set; } = null!;

  public string Format { get; set; } = null!;

  [Upload(HeaderName = "Release Date")]
  public string ReleaseDate { get; set; } = null!;

  [Upload(HeaderName = "Audience Rating")]
  public string Rating { get; set; } = null!;

  // TODO: clz exports as hh:mm so using string for now to allow upload to work
  // Need to parse hh:mm to minutes instead on upload and can revert back to int?
  // public int? Runtime { get; set; } = null!;
  public string Runtime { get; set; } = null!;

  public List<string> Studios { get; set; } = [];

  public List<string> Genres { get; set; } = [];

  public string Plot { get; set; } = null!;
}