namespace MediaSet.Api.Features.Entities.Services;

/// <summary>
/// The cover image a create or update request carries: an uploaded file, an image URL to download, or nothing.
/// The HTTP layer converts a multipart form or JSON body into this so the services never see HTTP types.
/// </summary>
/// <param name="File">Uploaded cover image; takes precedence over a URL.</param>
/// <param name="Url">Image URL to download.</param>
/// <param name="UrlFromEntity">
/// True when <paramref name="Url"/> is the entity's own <c>ImageUrl</c> (a JSON body) rather than a separate form field.
/// On update such a URL is only downloaded when it differs from the stored one.
/// </param>
public sealed record ImageInput(IFormFile? File = null, string? Url = null, bool UrlFromEntity = false)
{
    public static ImageInput None { get; } = new();

    public static ImageInput Upload(IFormFile file) => new(File: file);

    public static ImageInput Download(string url) => new(Url: url);

    /// <summary>Wraps the entity's own <c>ImageUrl</c>; blank means no image input.</summary>
    public static ImageInput FromEntityUrl(string? url) =>
        string.IsNullOrWhiteSpace(url) ? None : new(Url: url, UrlFromEntity: true);

    public bool HasFile => File is not null;

    public bool HasUrl => !string.IsNullOrWhiteSpace(Url);
}
