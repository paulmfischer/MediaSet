namespace MediaSet.Api.Features.Plex.Models;

public record PlexEntityStatusDto(string EntityType, bool Enabled, PlexSyncRunDto? LastRun, DateTimeOffset? NextRun);
