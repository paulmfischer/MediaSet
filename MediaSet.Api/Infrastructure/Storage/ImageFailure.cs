namespace MediaSet.Api.Infrastructure.Storage;

/// <summary>
/// Why an image could not be stored. Source-independent: the caller decides whether it came
/// from an upload or a URL download.
/// </summary>
public enum ImageFailureReason
{
    /// <summary>The URL is invalid, blocked, unresolvable or the remote server returned an error.</summary>
    Unreachable,
    /// <summary>The data is not a supported (JPEG or PNG), complete image.</summary>
    UnsupportedFormat,
    /// <summary>The file or download exceeds the configured size limit.</summary>
    TooLarge,
    /// <summary>The image dimensions exceed the configured pixel limit.</summary>
    PixelLimit
}

/// <summary>
/// Tags <see cref="ArgumentException"/>s thrown by the image pipeline with a machine-readable
/// reason, so callers do not have to parse messages. The exception type stays
/// <see cref="ArgumentException"/> so existing handlers keep working.
/// </summary>
public static class ImageFailure
{
    private const string DataKey = "ImageFailureReason";

    public static ArgumentException Create(ImageFailureReason reason, string message, Exception? inner = null, string? paramName = null)
    {
        var ex = paramName is null
            ? new ArgumentException(message, inner)
            : new ArgumentException(message, paramName, inner);
        ex.Data[DataKey] = reason;
        return ex;
    }

    public static ImageFailureReason? GetReason(Exception ex) =>
        ex.Data[DataKey] is ImageFailureReason reason ? reason : null;
}
