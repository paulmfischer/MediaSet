namespace MediaSet.Api.Models;

public class UpcItemDbConfiguration
{
    public string BaseUrl { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public int Timeout { get; set; } = 10;
}
