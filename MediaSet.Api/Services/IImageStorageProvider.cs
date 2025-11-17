namespace MediaSet.Api.Services;

/// <summary>
/// Abstraction for image storage backend (filesystem, S3, Azure Blob, etc.)
/// </summary>
public interface IImageStorageProvider
{
    /// <summary>
    /// Save image data to storage
    /// </summary>
    /// <param name="imageData">The binary image data</param>
    /// <param name="relativePath">The relative path within the storage root (e.g., "books/entityId-guid.jpg")</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task that completes when image is saved</returns>
    Task SaveImageAsync(byte[] imageData, string relativePath, CancellationToken cancellationToken);

    /// <summary>
    /// Retrieve image data from storage as a stream
    /// </summary>
    /// <param name="relativePath">The relative path within the storage root</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Stream containing image data, or null if not found</returns>
    Task<Stream?> GetImageAsync(string relativePath, CancellationToken cancellationToken);

    /// <summary>
    /// Delete image from storage
    /// </summary>
    /// <param name="relativePath">The relative path within the storage root</param>
    /// <exception cref="ArgumentException">Thrown if relativePath attempts path traversal</exception>
    void DeleteImage(string relativePath);

    /// <summary>
    /// Check if image exists in storage
    /// </summary>
    /// <param name="relativePath">The relative path within the storage root</param>
    /// <returns>True if image exists, false otherwise</returns>
    /// <exception cref="ArgumentException">Thrown if relativePath attempts path traversal</exception>
    bool Exists(string relativePath);
}
