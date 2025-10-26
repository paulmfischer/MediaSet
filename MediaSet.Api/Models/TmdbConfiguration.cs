namespace MediaSet.Api.Models;

public class TmdbConfiguration
{
    public string BaseUrl { get; set; } = "https://api.themoviedb.org/3/";
    public string ApiKey { get; set; } = string.Empty;
    public int Timeout { get; set; } = 30;
}
