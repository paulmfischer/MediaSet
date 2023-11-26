using System.ComponentModel.DataAnnotations;

namespace MediaSet.Data.Entities;

public class Genre : IMetadata
{
    public int Id { get; set; }
    [Required]
    public required string Name { get; set; } = string.Empty;
}