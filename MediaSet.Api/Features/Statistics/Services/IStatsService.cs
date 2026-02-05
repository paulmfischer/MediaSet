using MediaSet.Api.Features.Statistics.Models;
using MediaSet.Api.Features.Entities.Models;
using MediaSet.Api.Models;

namespace MediaSet.Api.Features.Statistics.Services;

public interface IStatsService
{
    Task<Stats> GetMediaStatsAsync(CancellationToken cancellationToken = default);
}
