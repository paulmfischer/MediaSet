namespace MediaSet.Api.Shared.Models;

/// <summary>
/// Tracks the state of background image lookup attempts for an entity.
/// </summary>
public class ImageLookup
{
    public int AttemptCount { get; set; }
    public ImageLookupOutcome? LastOutcome { get; set; }
    public DateTime? NextEligibleAt { get; set; }
    public string? MetadataFingerprint { get; set; }
    public int StrategyVersion { get; set; }
    public bool IsBacklogRecovery { get; set; }
    public string? RequeueOperationId { get; set; }
    /// <summary>
    /// The timestamp when the background lookup service attempted to retrieve an image.
    /// </summary>
    public DateTime LookupAttemptedAt { get; set; }

    /// <summary>
    /// If the lookup failed, contains the error message explaining why the image could not be retrieved.
    /// Null if lookup succeeded.
    /// </summary>
    public string? FailureReason { get; set; }

    /// <summary>
    /// Indicates a terminal result for the metadata and strategy used by this attempt.
    /// A selective requeue may reassess it after metadata or strategy changes.
    /// </summary>
    public bool PermanentFailure { get; set; }
}

public enum ImageLookupOutcome
{
    Success,
    TransientFailure,
    NoMatch,
    MissingArtwork,
    MissingMetadata,
    UnsupportedStrategy,
    InvalidArtwork,
    Queued
}
