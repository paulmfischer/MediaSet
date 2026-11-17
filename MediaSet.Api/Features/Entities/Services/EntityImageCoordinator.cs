using MediaSet.Api.Infrastructure.Storage;
using MediaSet.Api.Shared.Models;

namespace MediaSet.Api.Features.Entities.Services;

/// <summary>
/// Stores, swaps and deletes entity cover images. A failed store never fails the entity write: it is
/// mapped to a stable <see cref="ImageErrorCodes"/> value so the entity can still be saved.
/// </summary>
public sealed class EntityImageCoordinator(IImageService imageService, ILogger<EntityImageCoordinator> logger)
{
    /// <summary>
    /// Stores the file or URL of <paramref name="input"/> (a file wins). Returns the stored image,
    /// or an error code when validation, download or storage failed.
    /// </summary>
    public async Task<(Image? Image, string? Error)> TryStoreAsync(
        ImageInput input, string entityType, string entityId, CancellationToken cancellationToken)
    {
        if (input.File is { } file)
        {
            logger.LogInformation("Processing image file upload for {entityType}/{id}", entityType, entityId);
            return await TryStoreAsync(
                () => imageService.SaveImageAsync(file, entityType.ToLower(), entityId, cancellationToken),
                ImageSource.Upload, entityType, entityId);
        }

        var url = input.Url!;
        logger.LogInformation("Processing image URL download for {entityType}/{id}: {url}", entityType, entityId, url);
        return await TryStoreAsync(
            () => imageService.DownloadAndSaveImageAsync(url, entityType, entityId, cancellationToken),
            ImageSource.Url, entityType, entityId);
    }

    private async Task<(Image? Image, string? Error)> TryStoreAsync(
        Func<Task<Image>> store, ImageSource source, string entityType, string entityId)
    {
        try
        {
            var image = await store();
            logger.LogInformation("Cover image stored for {entityType}/{id}", entityType, entityId);
            return (image, null);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Validation, download and storage failures (e.g. IOException) all leave the saved entity in place.
            var code = ImageErrorMapper.ToCode(ex, source);
            logger.LogWarning("Cover image from {source} not stored for {entityType}/{id} ({code}): {error}",
                source, entityType, entityId, code, ex.Message);
            return (null, code);
        }
    }

    /// <summary>
    /// Applies the outcome of storing a replacement image on an update. When the new image was stored
    /// it is set on the entity and the replaced image is returned so the caller can delete it once the
    /// entity is saved (null when unchanged or the same file). When storing failed the previous cover
    /// and its ImageUrl are kept so the entity still references the old image.
    /// </summary>
    public Image? ApplyStoredImage(IEntity updatedEntity, IEntity existingEntity, Image? newImage)
    {
        if (newImage is null)
        {
            updatedEntity.CoverImage = existingEntity.CoverImage;
            updatedEntity.ImageUrl = existingEntity.ImageUrl;
            return null;
        }

        updatedEntity.CoverImage = newImage;
        return existingEntity.CoverImage is not null && existingEntity.CoverImage.FilePath != newImage.FilePath
            ? existingEntity.CoverImage
            : null;
    }

    /// <summary>
    /// Deletes an image file, logging instead of throwing. Swallowing every exception is intentional: file deletion
    /// is best effort and must never fail the entity operation.
    /// </summary>
    public void DeleteFile(Image? image)
    {
        if (image is null)
        {
            return;
        }

        try
        {
            imageService.DeleteImageAsync(image.FilePath);
            logger.LogInformation("Deleted image file: {imagePath}", image.FilePath);
        }
        catch (Exception ex)
        {
            logger.LogWarning("Failed to delete image file {imagePath}: {error}", image.FilePath, ex.Message);
        }
    }
}
