namespace MediaSet.Api.Models;

public class GiantBombConfiguration
{
    public string BaseUrl { get; set; } = "https://www.giantbomb.com/api/";
    public string ApiKey { get; set; } = string.Empty;
    public int Timeout { get; set; } = 30;
}
