using MediaSet.Api.Features.Entities.Models;
using MediaSet.Api.Models;

namespace MediaSet.Api.Infrastructure.Lookup;

public interface IGiantBombClient
{
    Task<List<GiantBombSearchResult>?> SearchGameAsync(string title, CancellationToken cancellationToken);
    Task<GiantBombGameDetails?> GetGameDetailsAsync(string apiDetailUrlOrGuid, CancellationToken cancellationToken);
}
