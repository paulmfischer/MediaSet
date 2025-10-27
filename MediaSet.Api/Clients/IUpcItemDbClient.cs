using MediaSet.Api.Models;

namespace MediaSet.Api.Clients;

public interface IUpcItemDbClient
{
    Task<UpcItemResponse?> GetItemByCodeAsync(string code, CancellationToken cancellationToken);
}
