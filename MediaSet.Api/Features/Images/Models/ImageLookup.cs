namespace MediaSet.Api.Features.Images.Models;

/// <summary>
/// Tracks the state of background image lookup attempts for an entity.
/// </summary>
public class ImageLookup
{
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
    /// Indicates that lookup should never be retried for this entity.
    /// Set to true when the entity lacks required identifiers (e.g., no ISBN, no Barcode).
    /// </summary>
    public bool PermanentFailure { get; set; }
}
