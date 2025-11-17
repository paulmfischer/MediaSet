using System.Net;
using MediaSet.Api.Models;

namespace MediaSet.Api.Services;

/// <summary>
/// Implementation of IImageService providing image validation, storage, and retrieval operations.
/// Handles uploaded files and URL-based image downloads with comprehensive error handling.
/// </summary>
public class ImageService : IImageService
{
    private readonly IImageStorageProvider _storageProvider;
    private readonly ImageConfiguration _config;
    private readonly HttpClient _httpClient;
    private readonly ILogger<ImageService> _logger;

    /// <summary>
    /// Initialize the ImageService with required dependencies.
    /// </summary>
    /// <param name="storageProvider">The storage backend for persisting images</param>
    /// <param name="config">Configuration settings for image validation and storage</param>
    /// <param name="httpClient">HTTP client for downloading images from URLs</param>
    /// <param name="logger">Logger for diagnostic information</param>
    public ImageService(
        IImageStorageProvider storageProvider,
        ImageConfiguration config,
        HttpClient httpClient,
        ILogger<ImageService> logger)
    {
        _storageProvider = storageProvider ?? throw new ArgumentNullException(nameof(storageProvider));
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Save an uploaded image file to storage with validation.
    /// </summary>
    public async Task<Image> SaveImageAsync(IFormFile file, string entityType, string entityId, CancellationToken cancellationToken)
    {
        if (file == null || file.Length == 0)
        {
            _logger.LogWarning("SaveImageAsync called with null or empty file for {EntityType}/{EntityId}", entityType, entityId);
            throw new ArgumentException("File cannot be null or empty", nameof(file));
        }

        if (string.IsNullOrWhiteSpace(entityType) || string.IsNullOrWhiteSpace(entityId))
        {
            throw new ArgumentException("EntityType and EntityId cannot be null or empty");
        }

        try
        {
            // Validate file size
            var maxSizeBytes = _config.GetMaxFileSizeBytes();
            if (file.Length > maxSizeBytes)
            {
                _logger.LogWarning("File size {FileSize} exceeds maximum {MaxSize} for {EntityType}/{EntityId}",
                    file.Length, maxSizeBytes, entityType, entityId);
                throw new ArgumentException($"File size exceeds maximum allowed size of {_config.MaxFileSizeMb}MB");
            }

            // Validate MIME type
            var allowedMimeTypes = _config.GetAllowedMimeTypes().ToList();
            if (!allowedMimeTypes.Contains(file.ContentType))
            {
                _logger.LogWarning("Unsupported MIME type {MimeType} for {EntityType}/{EntityId}",
                    file.ContentType, entityType, entityId);
                throw new ArgumentException($"Unsupported file type. Allowed types: {string.Join(", ", allowedMimeTypes)}");
            }

            // Read file data
            await using var memoryStream = new MemoryStream();
            await file.CopyToAsync(memoryStream, cancellationToken);
            var imageData = memoryStream.ToArray();

            // Generate relative path: {entityType}/{entityId}-{guid}.{ext}
            var extension = Path.GetExtension(file.FileName).TrimStart('.');
            if (string.IsNullOrEmpty(extension))
            {
                extension = file.ContentType switch
                {
                    "image/jpeg" => "jpg",
                    "image/png" => "png",
                    _ => "img"
                };
            }

            var uniqueFileName = $"{entityId}-{Guid.NewGuid()}.{extension}";
            var relativePath = Path.Combine(entityType, uniqueFileName);

            // Save to storage
            await _storageProvider.SaveImageAsync(imageData, relativePath, cancellationToken);

            _logger.LogInformation("Image saved successfully: {RelativePath} ({SizeBytes} bytes)", relativePath, imageData.Length);

            // Return image metadata
            return new Image
            {
                FileName = file.FileName,
                FilePath = relativePath,
                ContentType = file.ContentType,
                FileSize = imageData.Length,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("SaveImageAsync operation was cancelled for {EntityType}/{EntityId}", entityType, entityId);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving image for {EntityType}/{EntityId}", entityType, entityId);
            throw;
        }
    }

    /// <summary>
    /// Download an image from URL and save it to storage with validation.
    /// </summary>
    public async Task<Image> DownloadAndSaveImageAsync(string imageUrl, string entityType, string entityId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(imageUrl))
        {
            throw new ArgumentException("Image URL cannot be null or empty", nameof(imageUrl));
        }

        if (string.IsNullOrWhiteSpace(entityType) || string.IsNullOrWhiteSpace(entityId))
        {
            throw new ArgumentException("EntityType and EntityId cannot be null or empty");
        }

        try
            {
                // Validate URL format
                if (!Uri.TryCreate(imageUrl, UriKind.Absolute, out var uri) ||
                    (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
                {
                    _logger.LogWarning("Invalid image URL format: {ImageUrl} for {EntityType}/{EntityId}",
                        imageUrl, entityType, entityId);
                    throw new ArgumentException("Image URL must be a valid HTTP or HTTPS URL");
                }

                // Download image with single HTTP call, retrieving both data and content type
                var (imageData, contentType) = await DownloadImageDataWithMetadataAsync(imageUrl, cancellationToken);

                // Validate MIME type from Content-Type header
                var allowedMimeTypes = _config.GetAllowedMimeTypes().ToList();
                if (!allowedMimeTypes.Contains(contentType))
                {
                    _logger.LogWarning("Unsupported MIME type {MimeType} from {ImageUrl}", contentType, imageUrl);
                    throw new ArgumentException($"Unsupported image type. Allowed types: {string.Join(", ", allowedMimeTypes)}");
                }

                // Validate file size
                var maxSizeBytes = _config.GetMaxDownloadSizeBytes();
                if (imageData.Length > maxSizeBytes)
                {
                    _logger.LogWarning("Downloaded image size {FileSize} exceeds maximum {MaxSize} for {ImageUrl}",
                        imageData.Length, maxSizeBytes, imageUrl);
                    throw new ArgumentException($"Downloaded image size exceeds maximum allowed size of {_config.MaxDownloadSizeMb}MB");
                }            // Generate relative path: {entityType}/{entityId}-{guid}.{ext}
            var extension = contentType switch
            {
                "image/jpeg" => "jpg",
                "image/png" => "png",
                _ => "img"
            };

            var uniqueFileName = $"{entityId}-{Guid.NewGuid()}.{extension}";
            var relativePath = Path.Combine(entityType, uniqueFileName);

            // Save to storage
            await _storageProvider.SaveImageAsync(imageData, relativePath, cancellationToken);

            _logger.LogInformation("Image downloaded and saved successfully from {ImageUrl} to {RelativePath} ({SizeBytes} bytes)",
                imageUrl, relativePath, imageData.Length);

            // Return image metadata
            return new Image
            {
                FileName = Path.GetFileName(new Uri(imageUrl).LocalPath),
                FilePath = relativePath,
                ContentType = contentType,
                FileSize = imageData.Length,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("DownloadAndSaveImageAsync operation was cancelled for {ImageUrl}", imageUrl);
            throw;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error downloading image from {ImageUrl}", imageUrl);
            throw new ArgumentException($"Failed to download image from URL: {ex.Message}", nameof(imageUrl), ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error downloading and saving image from {ImageUrl}", imageUrl);
            throw;
        }
    }

    /// <summary>
    /// Retrieve image stream for serving to clients.
    /// </summary>
    public async Task<Stream?> GetImageStreamAsync(string imagePath, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(imagePath))
        {
            throw new ArgumentException("Image path cannot be null or empty", nameof(imagePath));
        }

        try
        {
            var stream = await _storageProvider.GetImageAsync(imagePath, cancellationToken);
            if (stream == null)
            {
                _logger.LogDebug("Image not found: {ImagePath}", imagePath);
            }
            return stream;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving image stream for path: {ImagePath}", imagePath);
            throw;
        }
    }

    /// <summary>
    /// Delete image from storage.
    /// </summary>
    public void DeleteImageAsync(string imagePath)
    {
        if (string.IsNullOrWhiteSpace(imagePath))
        {
            throw new ArgumentException("Image path cannot be null or empty", nameof(imagePath));
        }

        try
        {
            _storageProvider.DeleteImage(imagePath);
            _logger.LogInformation("Image deleted successfully: {ImagePath}", imagePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting image at path: {ImagePath}", imagePath);
            throw;
        }
    }

    /// <summary>
    /// Download image data from URL with metadata retrieval in a single HTTP call.
    /// Returns both the image data and content type to avoid multiple requests.
    /// </summary>
    private async Task<(byte[] imageData, string contentType)> DownloadImageDataWithMetadataAsync(string imageUrl, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await _httpClient.GetAsync(imageUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();

            // Extract content type from response headers
            var contentType = response.Content.Headers.ContentType?.MediaType ?? "application/octet-stream";
            _logger.LogDebug("Retrieved content type {ContentType} for {ImageUrl}", contentType, imageUrl);

            // Check Content-Length header if available
            if (response.Content.Headers.ContentLength.HasValue)
            {
                var maxSizeBytes = _config.GetMaxDownloadSizeBytes();
                if (response.Content.Headers.ContentLength > maxSizeBytes)
                {
                    _logger.LogWarning("Content-Length {ContentLength} exceeds maximum {MaxSize} for {ImageUrl}",
                        response.Content.Headers.ContentLength, maxSizeBytes, imageUrl);
                    throw new ArgumentException($"Image size exceeds maximum allowed size of {_config.MaxDownloadSizeMb}MB");
                }
            }

            // Read response stream with size limit
            var maxSizeBytes2 = _config.GetMaxDownloadSizeBytes();
            await using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
            await using var limitedStream = new SizeLimitedStream(contentStream, maxSizeBytes2);
            await using var memoryStream = new MemoryStream();
            
            try
            {
                await limitedStream.CopyToAsync(memoryStream, cancellationToken);
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("size limit"))
            {
                _logger.LogWarning("Downloaded image exceeded size limit for {ImageUrl}", imageUrl);
                throw new ArgumentException($"Downloaded image exceeds maximum allowed size of {_config.MaxDownloadSizeMb}MB", ex);
            }

            return (memoryStream.ToArray(), contentType);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error downloading image from {ImageUrl}", imageUrl);
            throw;
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Download operation cancelled for {ImageUrl}", imageUrl);
            throw;
        }
    }
}

/// <summary>
/// Helper stream that enforces a maximum read size to prevent unbounded downloads.
/// </summary>
internal class SizeLimitedStream : Stream
{
    private readonly Stream _innerStream;
    private readonly long _maxSize;
    private long _bytesRead;

    public SizeLimitedStream(Stream innerStream, long maxSize)
    {
        _innerStream = innerStream ?? throw new ArgumentNullException(nameof(innerStream));
        _maxSize = maxSize;
        _bytesRead = 0;
    }

    public override bool CanRead => _innerStream.CanRead;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => throw new NotSupportedException();
    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        if (_bytesRead >= _maxSize)
        {
            throw new InvalidOperationException("Maximum download size limit exceeded");
        }

        var toRead = Math.Min(count, (int)(_maxSize - _bytesRead));
        var bytesRead = _innerStream.Read(buffer, offset, toRead);
        _bytesRead += bytesRead;

        return bytesRead;
    }

    public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        if (_bytesRead >= _maxSize)
        {
            throw new InvalidOperationException("Maximum download size limit exceeded");
        }

        var toRead = Math.Min(count, (int)(_maxSize - _bytesRead));
        var bytesRead = await _innerStream.ReadAsync(buffer, offset, toRead, cancellationToken);
        _bytesRead += bytesRead;

        return bytesRead;
    }

    public override void Flush() => _innerStream.Flush();
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _innerStream?.Dispose();
        }
        base.Dispose(disposing);
    }
}
