using System.ComponentModel.DataAnnotations;

namespace MediaSet.Data.Entities;

public class Format
{
    public int Id { get; set; }
    [Required]
    public string Name { get; set; } = string.Empty;
}