using MediaSet.Api.Infrastructure.Lookup.Models;
using MediaSet.Api.Features.Entities.Models;

namespace MediaSet.Api.Infrastructure.Lookup.Clients.GiantBomb;

public interface IGiantBombClient
{
    Task<List<GiantBombSearchResult>?> SearchGameAsync(string title, CancellationToken cancellationToken);
    Task<GiantBombGameDetails?> GetGameDetailsAsync(string apiDetailUrlOrGuid, CancellationToken cancellationToken);
}
