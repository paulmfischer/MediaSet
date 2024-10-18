using System.ComponentModel.DataAnnotations;

namespace MediaSet.Api.Models;

public class User
{
  [Required]
  public string Name { get; set; } = default!;

  [Required]
  public string Password { get; set; } = default!;
}