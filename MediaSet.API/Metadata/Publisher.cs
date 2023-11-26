using System.ComponentModel.DataAnnotations;

namespace MediaSet.Api.Metadata;

public class Publisher : IMetadata
{
    public int Id { get; set; }
    [Required]
    public required string Name { get; set; } = string.Empty;
}