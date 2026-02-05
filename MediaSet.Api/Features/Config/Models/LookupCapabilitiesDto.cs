namespace MediaSet.Api.Features.Config.Models;

public class LookupCapabilitiesDto
{
    public bool SupportsBookLookup { get; set; }
    public bool SupportsMovieLookup { get; set; }
    public bool SupportsGameLookup { get; set; }
    public bool SupportsMusicLookup { get; set; }
}
