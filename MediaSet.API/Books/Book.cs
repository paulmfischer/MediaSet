using System.ComponentModel.DataAnnotations;

namespace MediaSet.API.Books;

public class Book
{
    public int Id { get; set; }

    [Required]
    public string Title { get; set; } = default!;

    public DateTime PublishDate { get; set; }
    public int NumberOfPages { get; set; }
    public string ISBN { get; set; } = default!;
}

public class BookItem
{
    public int Id { get; set; }

    [Required]
    public string Title { get; set; } = default!;

    public DateTime PublishDate { get; set; }
    public int NumberOfPages { get; set; }
    public string ISBN { get; set; } = default!;
}

public static class BookMappingExtensions
{
    public static BookItem AsBookItem(this Book book)
    {
        return new()
        {
            Id = book.Id,
            Title = book.Title,
            PublishDate = book.PublishDate,
            NumberOfPages = book.NumberOfPages,
            ISBN = book.ISBN,
        };
    }
}