namespace MediaSet.Api.Models;

/// <summary>
/// Configuration settings for image storage and validation.
/// </summary>
public class ImageConfiguration
{
    /// <summary>
    /// The root directory path where images will be stored.
    /// </summary>
    public string StoragePath { get; set; } = "/app/data/images";

    /// <summary>
    /// Maximum file size in megabytes. Default: 5 MB.
    /// </summary>
    public int MaxFileSizeMb { get; set; } = 5;

    /// <summary>
    /// Comma-separated list of allowed MIME types. Default: "image/jpeg,image/png".
    /// </summary>
    public string AllowedMimeTypes { get; set; } = "image/jpeg,image/png";

    /// <summary>
    /// HTTP timeout in seconds for downloading images from URLs. Default: 30 seconds.
    /// </summary>
    public int HttpTimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Maximum download size in megabytes when downloading from URLs. Default: 10 MB.
    /// </summary>
    public int MaxDownloadSizeMb { get; set; } = 10;

    /// <summary>
    /// Whether to strip EXIF data from uploaded images. Default: true.
    /// </summary>
    public bool StripExifData { get; set; } = true;

    /// <summary>
    /// Get the maximum file size in bytes.
    /// </summary>
    public long GetMaxFileSizeBytes() => MaxFileSizeMb * 1024L * 1024L;

    /// <summary>
    /// Get the maximum download size in bytes.
    /// </summary>
    public long GetMaxDownloadSizeBytes() => MaxDownloadSizeMb * 1024L * 1024L;

    /// <summary>
    /// Parse allowed MIME types into a collection.
    /// </summary>
    public IEnumerable<string> GetAllowedMimeTypes()
    {
        return AllowedMimeTypes
            .Split(',')
            .Select(x => x.Trim())
            .Where(x => !string.IsNullOrEmpty(x));
    }
}
