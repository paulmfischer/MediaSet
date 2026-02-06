using MediaSet.Api.Infrastructure.Lookup.Models;

namespace MediaSet.Api.Infrastructure.Lookup.Clients.UpcItemDb;

public interface IUpcItemDbClient
{
    Task<UpcItemResponse?> GetItemByCodeAsync(string code, CancellationToken cancellationToken);
}
