namespace MediaSet.Api.Infrastructure.Storage;

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
    /// Maximum file size in megabytes for both uploads and downloads. Default: 5 MB.
    /// </summary>
    public int MaxFileSizeMb { get; set; } = 5;

    /// <summary>
    /// Comma-separated list of allowed image file extensions. Default: "jpg,jpeg,png".
    /// </summary>
    public string AllowedImageExtensions { get; set; } = "jpg,jpeg,png";

    /// <summary>
    /// HTTP timeout in seconds for downloading images from URLs. Default: 30 seconds.
    /// </summary>
    public int HttpTimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Whether EXIF and metadata should be stripped from images when saving. Default: true.
    /// </summary>
    public bool StripExifData { get; set; } = true;

    /// <summary>
    /// Get the maximum file size in bytes.
    /// </summary>
    public long GetMaxFileSizeBytes() => MaxFileSizeMb * 1024L * 1024L;

    /// <summary>
    /// Parse allowed image extensions into a collection.
    /// </summary>
    public IEnumerable<string> GetAllowedImageExtensions()
    {
        return AllowedImageExtensions
            .Split(',')
            .Select(x => x.Trim())
            .Where(x => !string.IsNullOrEmpty(x));
    }

    /// <summary>
    /// Derive the set of allowed MIME types from the configured image extensions.
    /// </summary>
    public IEnumerable<string> GetAllowedMimeTypes()
    {
        return GetAllowedImageExtensions()
            .Select(ext => ext switch
            {
                "jpg" or "jpeg" => "image/jpeg",
                "png" => "image/png",
                _ => null
            })
            .Where(mime => mime != null)
            .Distinct()!;
    }

    /// <summary>
    /// Returns the first configured file extension that corresponds to the given MIME type.
    /// Falls back to "jpg" if no match is found.
    /// </summary>
    public string GetExtensionForMimeType(string mimeType)
    {
        return GetAllowedImageExtensions()
            .FirstOrDefault(ext => (ext switch
            {
                "jpg" or "jpeg" => "image/jpeg",
                "png" => "image/png",
                _ => null
            }) == mimeType) ?? "jpg";
    }
}
