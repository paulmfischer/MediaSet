namespace MediaSet.Api.Features.Plex.Models;

public record PlexEntityStatusDto(string EntityType, bool Enabled, DateTimeOffset? LastRun, DateTimeOffset? NextRun);
