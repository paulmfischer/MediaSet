using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace MediaSet.Api.Features.Plex.Models;

/// <summary>States of a Plex sync run.</summary>
public static class PlexSyncRunStates
{
    /// <summary>Accepted but waiting for the shared lock, because another run holds it.</summary>
    public const string Queued = "queued";
    public const string Running = "running";
    public const string Succeeded = "succeeded";
    public const string Failed = "failed";
    /// <summary>Stopped by application shutdown, or left running by a restart.</summary>
    public const string Interrupted = "interrupted";

    public static bool IsInProgress(string state) => state is Queued or Running;
}

public static class PlexSyncTriggers
{
    public const string Manual = "manual";
    public const string Scheduled = "scheduled";
}

/// <summary>One sync run, stored in the PlexSyncRuns collection.</summary>
public class PlexSyncRun
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    public string EntityType { get; set; } = string.Empty;
    public string Trigger { get; set; } = PlexSyncTriggers.Manual;
    public string State { get; set; } = PlexSyncRunStates.Running;

    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime StartedAt { get; set; }

    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime? EndedAt { get; set; }

    /// <summary>MediaSet entries the run processed. Null until the run finishes successfully.</summary>
    public int? ItemsScanned { get; set; }

    /// <summary>Movies linked to a Plex item by the run. Null until the run finishes successfully.</summary>
    public int? MatchedCount { get; set; }

    /// <summary>Movies with no Plex item. Null until the run finishes successfully.</summary>
    public int? UnmatchedCount { get; set; }

    /// <summary>Movies that errored while being processed and were skipped. Null until the run finishes successfully.</summary>
    public int? ErroredCount { get; set; }

    /// <summary>Movies that got an ExternalId from TMDB at the start of the run. Null until the run finishes successfully.</summary>
    public int? ExternalIdsBackfilled { get; set; }

    /// <summary>Movies the backfill searched but left blank (no confident match, or a TMDB error). Null until the run finishes successfully.</summary>
    public int? BackfillSkipped { get; set; }

    /// <summary>Failure or interruption reason. Never contains the Plex token.</summary>
    public string? Message { get; set; }

    /// <summary>
    /// Plex movies the run found linked by more than one MediaSet movie (allowed, but usually a duplicate entry or a
    /// wrong ExternalId). Null until the run finishes successfully; empty when there were none.
    /// </summary>
    public List<PlexDuplicateLink>? DuplicateLinks { get; set; }

    public PlexSyncRun Clone() => (PlexSyncRun)MemberwiseClone();
}

/// <summary>One Plex item linked by several movies in a run, with those movies in the order the run met them.</summary>
public class PlexDuplicateLink
{
    public string PlexItemId { get; set; } = string.Empty;
    public string PlexTitle { get; set; } = string.Empty;
    public List<PlexLinkedMovie> Movies { get; set; } = [];
}

public class PlexLinkedMovie
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
}

public record PlexLinkedMovieDto(string Id, string Title);

public record PlexDuplicateLinkDto(string PlexItemId, string PlexTitle, IReadOnlyList<PlexLinkedMovieDto> Movies);

public record PlexSyncRunDto(
    string? Id,
    string EntityType,
    string Trigger,
    string State,
    DateTimeOffset StartedAt,
    DateTimeOffset? EndedAt,
    int? ItemsScanned,
    int? MatchedCount,
    int? UnmatchedCount,
    int? ErroredCount,
    int? ExternalIdsBackfilled,
    int? BackfillSkipped,
    string? Message,
    IReadOnlyList<PlexDuplicateLinkDto>? DuplicateLinks)
{
    public static PlexSyncRunDto From(PlexSyncRun run) => new(
        run.Id,
        run.EntityType,
        run.Trigger,
        run.State,
        new DateTimeOffset(DateTime.SpecifyKind(run.StartedAt, DateTimeKind.Utc)),
        run.EndedAt.HasValue ? new DateTimeOffset(DateTime.SpecifyKind(run.EndedAt.Value, DateTimeKind.Utc)) : null,
        run.ItemsScanned,
        run.MatchedCount,
        run.UnmatchedCount,
        run.ErroredCount,
        run.ExternalIdsBackfilled,
        run.BackfillSkipped,
        run.Message,
        run.DuplicateLinks?.Select(d => new PlexDuplicateLinkDto(
            d.PlexItemId, d.PlexTitle, d.Movies.Select(m => new PlexLinkedMovieDto(m.Id, m.Title)).ToList())).ToList());
}
