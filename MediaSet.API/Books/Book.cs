using System.Collections.ObjectModel;
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
    public Genre? Genre { get; set; }
    public ICollection<Publisher> Publishers { get; set; } = new Collection<Publisher>();
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
    public Genre? Genre { get; set; }
    public ICollection<Publisher> Publishers { get; set; } = new Collection<Publisher>();
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
            Format = book.Format,
            Genre = book.Genre,
            Publishers = book.Publishers,
        };
    }

    public static Book AsBook(this UpdateBook book)
    { 
        return new()
        {
            Id = book.Id,
            Title = book.Title,
            ISBN = book.ISBN,
            Plot = book.Plot,
            PublicationYear = book.PublicationYear,
            NumberOfPages = book.NumberOfPages,
            Format = book.Format,
            Genre = book.Genre,
            Publishers = book.Publishers,
        };
    }
}