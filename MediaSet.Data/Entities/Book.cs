using System.ComponentModel.DataAnnotations;

namespace MediaSet.Data.Entities;

public class Book
{
    public int Id { get; set; }
    [Required]
    public string Title { get; set; } = string.Empty;
    public string? ISBN { get; set; }
    public string? Plot { get; set; }
    public string? PublicationYear { get; set; }
    public int? NumberOfPages { get; set; }

    public int? FormatId { get; set; }
    public Format? Format { get; set; }
}