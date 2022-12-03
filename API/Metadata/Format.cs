using System.ComponentModel.DataAnnotations;

namespace API;

public class Format
{
    public int Id { get; set; }
    [Required]
    public string Name { get; set; } = default!;
    [Required]
    public MediaType MediaType { get; set; }
}