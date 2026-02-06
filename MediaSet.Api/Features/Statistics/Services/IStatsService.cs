using MediaSet.Api.Features.Statistics.Models;
using MediaSet.Api.Shared.Models;

namespace MediaSet.Api.Features.Statistics.Services;

public interface IStatsService
{
    Task<Stats> GetMediaStatsAsync(CancellationToken cancellationToken = default);
}
