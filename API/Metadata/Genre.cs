using System.ComponentModel.DataAnnotations;

namespace API;

public class Genre
{
    public int Id { get; set; }
    [Required]
    public string Name { get; set; } = default!;
}