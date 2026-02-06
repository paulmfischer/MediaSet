using MediaSet.Api.Features.Entities.Models;

namespace MediaSet.Api.Features.Lookup.Models;

public class OpenLibraryConfiguration
{
    public string BaseUrl { get; set; } = "https://openlibrary.org/";
    public int Timeout { get; set; } = 30;
    public string ContactEmail { get; set; } = string.Empty;
}
