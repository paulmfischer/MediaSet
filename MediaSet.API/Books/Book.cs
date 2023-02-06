using System.ComponentModel.DataAnnotations;

namespace MediaSet.Books;

public class Book
{
    public int Id { get; set; }

    [Required]
    public string Title { get; set; } = default!;

    public DateTime PublishDate { get; set; }
    public int NumberOfPages { get; set; }
    public string ISBN { get; set; } = default!;
}