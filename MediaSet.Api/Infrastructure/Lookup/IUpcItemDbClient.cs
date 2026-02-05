using MediaSet.Api.Models;

namespace MediaSet.Api.Infrastructure.Lookup;

public interface IUpcItemDbClient
{
    Task<UpcItemResponse?> GetItemByCodeAsync(string code, CancellationToken cancellationToken);
}
