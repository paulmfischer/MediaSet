namespace MediaSet.Api.Models;

public class MusicBrainzConfiguration
{
    public string BaseUrl { get; set; } = "https://musicbrainz.org/";
    public int Timeout { get; set; } = 10;
    public string UserAgent { get; set; } = "MediaSet/1.0";
}
