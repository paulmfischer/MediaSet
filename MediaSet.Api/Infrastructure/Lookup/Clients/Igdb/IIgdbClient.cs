using MediaSet.Api.Infrastructure.Lookup.Models;

namespace MediaSet.Api.Infrastructure.Lookup.Clients.Igdb;

public interface IIgdbClient
{
    Task<List<IgdbGame>?> SearchGameAsync(string title, CancellationToken cancellationToken);
    Task<IgdbGame?> GetGameDetailsAsync(int igdbId, CancellationToken cancellationToken);
}
