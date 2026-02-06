namespace MediaSet.Api.Infrastructure.Storage;

/// <summary>
/// Service for managing image operations including validation, saving, downloading, and deletion.
/// </summary>
public interface IImageService
{
    /// <summary>
    /// Save an uploaded image file to storage.
    /// </summary>
    /// <param name="file">The uploaded image file from multipart form data</param>
    /// <param name="entityType">The type of entity the image belongs to (e.g., "books", "movies")</param>
    /// <param name="entityId">The ID of the entity</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Image metadata object containing file info and path</returns>
    /// <exception cref="ArgumentException">Thrown if file is invalid, unsupported format, or exceeds size limit</exception>
    /// <exception cref="IOException">Thrown if file storage fails</exception>
    Task<Image> SaveImageAsync(IFormFile file, string entityType, string entityId, CancellationToken cancellationToken);

    /// <summary>
    /// Download an image from a URL and save it to storage.
    /// </summary>
    /// <param name="imageUrl">The URL to download the image from</param>
    /// <param name="entityType">The type of entity the image belongs to</param>
    /// <param name="entityId">The ID of the entity</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Image metadata object containing file info and path</returns>
    /// <exception cref="ArgumentException">Thrown if URL is invalid, unsupported format, or exceeds size limit</exception>
    /// <exception cref="HttpRequestException">Thrown if download fails</exception>
    /// <exception cref="IOException">Thrown if file storage fails</exception>
    Task<Image> DownloadAndSaveImageAsync(string imageUrl, string entityType, string entityId, CancellationToken cancellationToken);

    /// <summary>
    /// Retrieve image file stream for serving to clients.
    /// </summary>
    /// <param name="imagePath">The relative path to the image file (e.g., "books/book-id-guid.jpg")</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Stream containing image data, or null if image not found</returns>
    /// <exception cref="ArgumentException">Thrown if image path is null or empty</exception>
    /// <exception cref="IOException">Thrown if file read fails</exception>
    Task<Stream?> GetImageStreamAsync(string imagePath, CancellationToken cancellationToken);

    /// <summary>
    /// Delete an image from storage.
    /// </summary>
    /// <param name="imagePath">The relative path to the image file (e.g., "books/book-id-guid.jpg")</param>
    /// <exception cref="ArgumentException">Thrown if image path is null or empty</exception>
    /// <exception cref="IOException">Thrown if file deletion fails</exception>
    void DeleteImageAsync(string imagePath);
}
