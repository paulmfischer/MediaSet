using MediaSet.Api.Infrastructure.Plex;

namespace MediaSet.Api.Features.Plex.Models;

/// <summary>One match returned by the title search endpoint. Carries no file details or secrets.</summary>
public record PlexSearchResultDto(
    string Title,
    string PlexItemId,
    int? Year,
    string Library,
    PlexGuids Guids);
