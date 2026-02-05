using MediaSet.Api.Features.Entities.Models;
using MediaSet.Api.Models;

namespace MediaSet.Api.Services;

public interface IStatsService
{
    Task<Stats> GetMediaStatsAsync(CancellationToken cancellationToken = default);
}
