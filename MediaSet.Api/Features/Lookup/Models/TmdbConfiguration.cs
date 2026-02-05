namespace MediaSet.Api.Features.Lookup.Models;

public class TmdbConfiguration
{
    public string BaseUrl { get; set; } = string.Empty;
    public string BearerToken { get; set; } = string.Empty;
    public int Timeout { get; set; } = 10;
}
