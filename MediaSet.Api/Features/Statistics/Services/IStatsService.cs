using MediaSet.Api.Features.Statistics.Models;

namespace MediaSet.Api.Features.Statistics.Services;

public interface IStatsService
{
    Task<Stats> GetMediaStatsAsync(CancellationToken cancellationToken = default);
}
