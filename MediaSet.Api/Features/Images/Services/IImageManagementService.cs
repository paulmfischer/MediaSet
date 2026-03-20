namespace MediaSet.Api.Features.Images.Services;

public interface IImageManagementService
{
    Task<int> DeleteOrphanedImagesAsync(CancellationToken cancellationToken = default);
}
