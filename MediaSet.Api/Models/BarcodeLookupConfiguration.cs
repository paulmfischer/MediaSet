namespace MediaSet.Api.Models;

public class BarcodeLookupConfiguration
{
    public string BaseUrl { get; set; } = "https://api.barcodelookup.com/v3/";
    public string ApiKey { get; set; } = string.Empty;
    public int Timeout { get; set; } = 30;
}
