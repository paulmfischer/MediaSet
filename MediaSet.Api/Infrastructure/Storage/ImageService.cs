using System.Net;
using System.Buffers.Binary;
using Microsoft.Extensions.Options;
using SkiaSharp;
using CoverImage = MediaSet.Api.Shared.Models.Image;

namespace MediaSet.Api.Infrastructure.Storage;

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
    /// <param name="configOptions">Configuration settings for image validation and storage</param>
    /// <param name="httpClient">HTTP client for downloading images from URLs</param>
    /// <param name="logger">Logger for diagnostic information</param>
    public ImageService(
        IImageStorageProvider storageProvider,
        IOptions<ImageConfiguration> configOptions,
        HttpClient httpClient,
        ILogger<ImageService> logger)
    {
        _storageProvider = storageProvider ?? throw new ArgumentNullException(nameof(storageProvider));
        _config = configOptions?.Value ?? throw new ArgumentNullException(nameof(configOptions));
        if (_config.MaxPixelCount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(configOptions), "MaxPixelCount must be positive");
        }
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Save an uploaded image file to storage with validation and EXIF data stripping.
    /// </summary>
    public async Task<CoverImage> SaveImageAsync(IFormFile file, string entityType, string entityId, CancellationToken cancellationToken)
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

            await using var inputStream = file.OpenReadStream();
            return await ProcessAndSaveImageAsync(inputStream, entityType, entityId, cancellationToken);
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
    /// Download an image from URL and save it to storage with validation and EXIF data stripping.
    /// </summary>
    public async Task<CoverImage> DownloadAndSaveImageAsync(string imageUrl, string entityType, string entityId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(imageUrl))
        {
            throw new ArgumentException("Image URL cannot be null or empty", nameof(imageUrl));
        }

        if (string.IsNullOrWhiteSpace(entityType) || string.IsNullOrWhiteSpace(entityId))
        {
            throw new ArgumentException("EntityType and EntityId cannot be null or empty");
        }

        entityType = entityType.ToLower();

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

            // Validate the initial URL does not target a private/reserved address (SSRF protection)
            await SsrfGuard.ValidateUrlAsync(uri, cancellationToken);

            // Download image, following redirects manually so each hop is SSRF-validated
            var imageData = await DownloadImageDataAsync(imageUrl, cancellationToken);
            await using var inputStream = new MemoryStream(imageData);
            return await ProcessAndSaveImageAsync(inputStream, entityType, entityId, cancellationToken);
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

    private async Task<CoverImage> ProcessAndSaveImageAsync(
        Stream input, string entityType, string entityId, CancellationToken cancellationToken)
    {
        await using var limitedStream = new SizeLimitedStream(input, _config.GetMaxFileSizeBytes());
        await using var buffer = new MemoryStream();
        try
        {
            await limitedStream.CopyToAsync(buffer, cancellationToken);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("size limit"))
        {
            throw new ArgumentException(
                $"Image size exceeds maximum allowed size of {_config.MaxFileSizeMb}MB", ex);
        }

        var imageData = buffer.ToArray();
        cancellationToken.ThrowIfCancellationRequested();
        using var encodedData = SKData.CreateCopy(imageData);
        using var codec = SKCodec.Create(encodedData);
        if (codec == null)
        {
            throw new ArgumentException("Unsupported file type or invalid image data. Only JPEG and PNG are supported.");
        }

        var mimeType = codec.EncodedFormat switch
        {
            SKEncodedImageFormat.Jpeg => "image/jpeg",
            SKEncodedImageFormat.Png => "image/png",
            _ => throw new ArgumentException("Unsupported file type. Only JPEG and PNG are supported.")
        };
        if (!_config.GetAllowedMimeTypes().Contains(mimeType))
        {
            throw new ArgumentException($"Unsupported file type. Allowed types: {string.Join(", ", _config.GetAllowedMimeTypes())}");
        }

        // Codecs may return successfully after decoding all scanlines even when the container is truncated.
        if (!HasCompleteContainer(imageData, codec.EncodedFormat))
        {
            throw new ArgumentException("Image data is corrupt or incomplete");
        }

        var info = codec.Info;
        if (info.Width <= 0 || info.Height <= 0 || (long)info.Width * info.Height > _config.MaxPixelCount)
        {
            throw new ArgumentException($"Image dimensions exceed maximum allowed pixel count of {_config.MaxPixelCount}");
        }

        cancellationToken.ThrowIfCancellationRequested();
        // Decode even when retaining metadata: successful header detection does not validate the pixel payload.
        var bitmapInfo = new SKImageInfo(info.Width, info.Height, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var bitmap = new SKBitmap(bitmapInfo);
        if (bitmap.GetPixels() == IntPtr.Zero ||
            codec.GetPixels(bitmapInfo, bitmap.GetPixels()) != SKCodecResult.Success)
        {
            throw new ArgumentException("Image data is corrupt or incomplete");
        }
        cancellationToken.ThrowIfCancellationRequested();

        if (_config.StripExifData)
        {
            using var image = SKImage.FromBitmap(bitmap);
            using var output = image.Encode(codec.EncodedFormat, 75);
            if (output == null)
            {
                throw new ArgumentException("Failed to encode image");
            }
            imageData = output.ToArray();
            cancellationToken.ThrowIfCancellationRequested();
        }

        var imageId = Guid.NewGuid().ToString();
        var extension = _config.GetExtensionForMimeType(mimeType);
        var fileName = $"{entityId}-{imageId}.{extension}";
        var relativePath = Path.Combine(entityType, fileName);
        await _storageProvider.SaveImageAsync(imageData, relativePath, cancellationToken);
        _logger.LogInformation("Image saved successfully: {RelativePath} ({SizeBytes} bytes)", relativePath, imageData.Length);
        return new CoverImage
        {
            Id = imageId,
            FileName = fileName,
            FilePath = relativePath,
            ContentType = mimeType,
            FileSize = imageData.Length,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    private static bool HasCompleteContainer(ReadOnlySpan<byte> data, SKEncodedImageFormat format)
    {
        if (format == SKEncodedImageFormat.Jpeg)
        {
            return data.Length >= 2 && data[^2] == 0xff && data[^1] == 0xd9;
        }

        var offset = 8; // PNG signature, already identified by SKCodec.
        while (data.Length - offset >= 12)
        {
            var length = BinaryPrimitives.ReadUInt32BigEndian(data.Slice(offset, 4));
            if (length > data.Length - offset - 12)
            {
                return false;
            }
            if (data.Slice(offset + 4, 4).SequenceEqual("IEND"u8))
            {
                return length == 0 && data.Slice(offset + 8, 4).SequenceEqual(new byte[] { 0xae, 0x42, 0x60, 0x82 });
            }
            offset += (int)length + 12;
        }
        return false;
    }

    /// <summary>
    /// Download image data from URL with size limit enforcement.
    /// Follows redirects manually, SSRF-validating each redirect target before following it.
    /// Returns image bytes; the processing pipeline determines their actual format.
    /// </summary>
    private async Task<byte[]> DownloadImageDataAsync(string imageUrl, CancellationToken cancellationToken)
    {
        const int maxRedirects = 10;
        var currentUrl = imageUrl;
        var redirectCount = 0;

        try
        {
            while (true)
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, currentUrl);
                using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

                // Handle redirects manually so each hop is SSRF-validated
                if (response.StatusCode is HttpStatusCode.MovedPermanently
                    or HttpStatusCode.Found
                    or HttpStatusCode.SeeOther
                    or HttpStatusCode.TemporaryRedirect
                    or HttpStatusCode.PermanentRedirect)
                {
                    if (redirectCount >= maxRedirects)
                    {
                        throw new ArgumentException($"Image URL exceeded the maximum redirect limit of {maxRedirects}.");
                    }

                    var location = response.Headers.Location;
                    if (location == null)
                    {
                        throw new ArgumentException("Redirect response is missing a Location header.");
                    }

                    // Resolve relative redirect URIs against the current URL
                    if (!location.IsAbsoluteUri)
                    {
                        location = new Uri(new Uri(currentUrl), location);
                    }

                    // Validate the redirect target is not a private/reserved address
                    await SsrfGuard.ValidateUrlAsync(location, cancellationToken);

                    currentUrl = location.ToString();
                    redirectCount++;
                    continue;
                }

                response.EnsureSuccessStatusCode();

                // Check Content-Length header if available
                if (response.Content.Headers.ContentLength.HasValue)
                {
                    var maxSizeBytes = _config.GetMaxFileSizeBytes();
                    if (response.Content.Headers.ContentLength > maxSizeBytes)
                    {
                        _logger.LogWarning("Content-Length {ContentLength} exceeds maximum {MaxSize} for {ImageUrl}",
                            response.Content.Headers.ContentLength, maxSizeBytes, imageUrl);
                        throw new ArgumentException($"Image size exceeds maximum allowed size of {_config.MaxFileSizeMb}MB");
                    }
                }

                // Read response stream with size limit
                var maxSizeBytes2 = _config.GetMaxFileSizeBytes();
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
                    throw new ArgumentException($"Downloaded image exceeds maximum allowed size of {_config.MaxFileSizeMb}MB", ex);
                }

                return memoryStream.ToArray();
            }
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
        var toRead = (int)Math.Min(count, _maxSize - _bytesRead);
        if (toRead == 0 && count > 0)
        {
            toRead = 1; // At the limit, distinguish EOF from one additional byte.
        }
        var bytesRead = _innerStream.Read(buffer, offset, toRead);
        if (bytesRead > _maxSize - _bytesRead)
        {
            throw new InvalidOperationException("Maximum download size limit exceeded");
        }
        _bytesRead += bytesRead;

        return bytesRead;
    }

    public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        var toRead = (int)Math.Min(count, _maxSize - _bytesRead);
        if (toRead == 0 && count > 0)
        {
            toRead = 1;
        }
        var bytesRead = await _innerStream.ReadAsync(buffer, offset, toRead, cancellationToken);
        if (bytesRead > _maxSize - _bytesRead)
        {
            throw new InvalidOperationException("Maximum download size limit exceeded");
        }
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
