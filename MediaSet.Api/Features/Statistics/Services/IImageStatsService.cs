using MediaSet.Api.Features.Statistics.Models;

namespace MediaSet.Api.Features.Statistics.Services;

public interface IImageStatsService
{
    Task<ImageStats?> GetImageStatsAsync(CancellationToken cancellationToken = default);
}
