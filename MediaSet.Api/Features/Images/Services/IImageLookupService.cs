using MediaSet.Api.Shared.Models;

namespace MediaSet.Api.Features.Images.Services;

/// <summary>
/// Service that orchestrates image lookup for a single entity using lookup strategies.
/// </summary>
public interface IImageLookupService
{
    /// <summary>
    /// Attempts to look up and save an image for the given entity.
    /// First tries to download from existing ImageUrl, then falls back to external API lookup.
    /// </summary>
    /// <param name="entity">The entity to find an image for.</param>
    /// <param name="mediaType">The type of media entity.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result of the lookup operation.</returns>
    Task<ImageLookupResult> LookupAndSaveImageAsync(
        IEntity entity,
        MediaTypes mediaType,
        CancellationToken cancellationToken);
}

/// <summary>
/// Result of an image lookup operation.
/// </summary>
/// <param name="Success">Whether the image was successfully found and saved.</param>
/// <param name="ImageUrl">The URL the image was downloaded from, if successful.</param>
/// <param name="SavedImage">The saved image object, if successful.</param>
/// <param name="ErrorMessage">Error message if lookup failed.</param>
/// <param name="PermanentFailure">Whether this failure should prevent future retry attempts.</param>
public record ImageLookupResult(
    bool Success,
    string? ImageUrl,
    Image? SavedImage,
    string? ErrorMessage,
    bool PermanentFailure = false
);
