using System.ComponentModel.DataAnnotations;

namespace API;

public class Studio
{
    public int Id { get; set; }
    [Required]
    public string Name { get; set; } = default!;
}