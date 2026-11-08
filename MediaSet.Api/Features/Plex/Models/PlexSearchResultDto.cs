namespace MediaSet.Api.Features.Plex.Models;

using MediaSet.Api.Infrastructure.Plex;

/// <summary>One match returned by the title search endpoint. Carries no file details or secrets.</summary>
public record PlexSearchResultDto(
    string Title,
    string PlexItemId,
    int? Year,
    string Library,
    PlexGuids Guids);
