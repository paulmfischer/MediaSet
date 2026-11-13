namespace MediaSet.Api.Infrastructure.Lookup.Models;

/// <summary>
/// Google Books volumes API, used only as a keyless fallback book cover source after Open Library has no cover.
/// On by default; set <c>GoogleBooksConfiguration__Enabled=false</c> to turn it off.
/// </summary>
public class GoogleBooksConfiguration
{
    public bool Enabled { get; set; } = true;
    public string BaseUrl { get; set; } = "https://www.googleapis.com/books/v1/";
    public int Timeout { get; set; } = 30;
}
