using System.ComponentModel.DataAnnotations;
using MediaSet.Data.Entities;

namespace MediaSet.Api.BookApi;

public class CreateBook
{
    [Required]
    public string Title { get; set; } = null!;
    public string? ISBN { get; set; }
    public string? Plot { get; set; }
    public string? PublicationYear { get; set; }
    public int? NumberOfPages { get; set; }
    public Format? Format { get; set; }
}

public class UpdateBook
{
    public int Id { get; set; }
    [Required]
    public string Title { get; set; } = null!;
    public string? ISBN { get; set; }
    public string? Plot { get; set; }
    public string? PublicationYear { get; set; }
    public int? NumberOfPages { get; set; }
    public Format? Format { get; set; }
}


public static class BookMappingExtensions
{
    public static Book AsBook(this CreateBook book)
    {
        return new()
        {
            Title = book.Title,
            ISBN = book.ISBN,
            Plot = book.Plot,
            PublicationYear = book.PublicationYear,
            NumberOfPages = book.NumberOfPages,
            // FormatId = book.FormatId,
            Format = book.Format,
        };
    }

    public static Book AsBook(this UpdateBook book)
    { 
        return new()
        {
            Title = book.Title,
            ISBN = book.ISBN,
            Plot = book.Plot,
            PublicationYear = book.PublicationYear,
            NumberOfPages = book.NumberOfPages,
            // FormatId = book.FormatId,
            Format = book.Format,
        };
    }
}