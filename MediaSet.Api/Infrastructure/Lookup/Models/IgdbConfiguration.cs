namespace MediaSet.Api.Infrastructure.Lookup.Models;

public class IgdbConfiguration
{
    public string BaseUrl { get; set; } = "https://api.igdb.com/v4/";
    public string TokenUrl { get; set; } = "https://id.twitch.tv/oauth2/token";
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public int Timeout { get; set; } = 30;
}
