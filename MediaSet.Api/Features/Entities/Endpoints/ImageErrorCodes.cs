using MediaSet.Api.Infrastructure.Storage;

namespace MediaSet.Api.Features.Entities.Endpoints;

/// <summary>
/// Stable codes returned as <c>imageError</c> when an entity was saved but its cover image was not.
/// Clients map these to their own messages; never rely on the logged reason text.
/// </summary>
public static class ImageErrorCodes
{
    /// <summary>The image URL is invalid, blocked, unreachable or answered with an error.</summary>
    public const string UrlUnreachable = "url_unreachable";
    /// <summary>The downloaded data is not a supported (JPEG or PNG), complete image.</summary>
    public const string NotAnImage = "not_an_image";
    /// <summary>The downloaded image exceeds the size limit.</summary>
    public const string DownloadTooLarge = "download_too_large";
    /// <summary>The uploaded file is not a supported (JPEG or PNG), complete image.</summary>
    public const string UnsupportedType = "unsupported_type";
    /// <summary>The uploaded file exceeds the size limit.</summary>
    public const string UploadTooLarge = "upload_too_large";
    /// <summary>The image dimensions exceed the pixel limit.</summary>
    public const string PixelLimitExceeded = "pixel_limit_exceeded";
    /// <summary>Any failure that does not fit a more specific code.</summary>
    public const string Unknown = "unknown";
}

internal enum ImageSource
{
    Upload,
    Url
}

/// <summary>Body of a successful PUT when the cover image could not be stored.</summary>
/// <param name="ImageError">One of <see cref="ImageErrorCodes"/>.</param>
public sealed record ImageErrorResponse(string ImageError);

internal static class ImageErrorMapper
{
    public static string ToCode(Exception ex, ImageSource source)
    {
        if (ex is HttpRequestException)
        {
            return ImageErrorCodes.UrlUnreachable;
        }

        return (ImageFailure.GetReason(ex), source) switch
        {
            (ImageFailureReason.Unreachable, _) => ImageErrorCodes.UrlUnreachable,
            (ImageFailureReason.UnsupportedFormat, ImageSource.Upload) => ImageErrorCodes.UnsupportedType,
            (ImageFailureReason.UnsupportedFormat, ImageSource.Url) => ImageErrorCodes.NotAnImage,
            (ImageFailureReason.TooLarge, ImageSource.Upload) => ImageErrorCodes.UploadTooLarge,
            (ImageFailureReason.TooLarge, ImageSource.Url) => ImageErrorCodes.DownloadTooLarge,
            (ImageFailureReason.PixelLimit, _) => ImageErrorCodes.PixelLimitExceeded,
            _ => ImageErrorCodes.Unknown
        };
    }
}
