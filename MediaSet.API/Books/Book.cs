namespace MediaSet.BookApi;
public class Book
{
    public int Id { get; set; }
    public required string Title { get; set; }
    public required string ISBN { get; set; }
    public required string Plot { get; set; }
    public required string PublicationYear { get; set; }
    public required int NumberOfPages { get; set; }
}