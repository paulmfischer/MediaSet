namespace MediaSet.Api.Models;

public class IntegrationAttributionDto
{
    public string Key { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public bool Enabled { get; set; }
    public string? AttributionUrl { get; set; }
    public string? AttributionText { get; set; }
    public string? LogoPath { get; set; }
}
