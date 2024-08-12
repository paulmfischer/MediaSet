using System.ComponentModel.DataAnnotations;
using MediaSet.Blazor.Validators;

namespace MediaSet.Blazor.Models;

public class Book
{
  public string? Id { get; set; }

  [Required]
  public string Title { get; set; } = null!;

  public string ISBN { get; set; } = null!;

  public string Format { get; set; } = null!;

  [Range(0, int.MaxValue, ErrorMessage = "Pages must be greater than 0, and less than integer max.")]
  public int? Pages { get; set; }

  [DateValidator]
  public string PublicationDate { get; set; } = null!;

  public List<string> Author { get; set; } = [];

  public List<string> Publisher { get; set; } = [];

  public List<string> Genre { get; set; } = [];

  [MaxLength(int.MaxValue, ErrorMessage = "The Plot is too long! Please shorten it.")]
  public string Plot { get; set; } = null!;

  public string Subtitle { get; set; } = null!;

  public override string ToString()
  {
    return System.Text.Json.JsonSerializer.Serialize(this);
  }
}