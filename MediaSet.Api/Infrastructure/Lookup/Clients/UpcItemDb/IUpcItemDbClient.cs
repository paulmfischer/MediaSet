using MediaSet.Api.Features.Lookup.Models;
using MediaSet.Api.Features.Entities.Models;

namespace MediaSet.Api.Infrastructure.Lookup.Clients.UpcItemDb;

public interface IUpcItemDbClient
{
    Task<UpcItemResponse?> GetItemByCodeAsync(string code, CancellationToken cancellationToken);
}
