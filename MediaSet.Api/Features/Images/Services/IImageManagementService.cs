namespace MediaSet.Api.Features.Images.Services;

public interface IImageManagementService
{
    Task<int> DeleteOrphanedImagesAsync(CancellationToken cancellationToken = default);
    Task<int> ResetImageLookupAsync(IEnumerable<string> entityIds, string entityType, CancellationToken cancellationToken = default);
}
