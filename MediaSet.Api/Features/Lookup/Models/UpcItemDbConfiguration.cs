namespace MediaSet.Api.Features.Lookup.Models;

public class UpcItemDbConfiguration
{
    public string BaseUrl { get; set; } = string.Empty;
    public int Timeout { get; set; } = 10;
}
