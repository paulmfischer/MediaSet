using System.ComponentModel.DataAnnotations;

namespace MediaSet.BookApi;
public class Book
{
    public int Id { get; set; }
    [Required]
    public required string Title { get; set; }
    public string? ISBN { get; set; }
    public string? Plot { get; set; }
    public string? PublicationYear { get; set; }
    public int? NumberOfPages { get; set; }
}
