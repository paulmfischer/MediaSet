using MediaSet.Api.Features.Lookup.Models;
using MediaSet.Api.Features.Entities.Models;
using MediaSet.Api.Features.Entities.Models;

namespace MediaSet.Api.Infrastructure.Lookup;

public interface IUpcItemDbClient
{
    Task<UpcItemResponse?> GetItemByCodeAsync(string code, CancellationToken cancellationToken);
}
