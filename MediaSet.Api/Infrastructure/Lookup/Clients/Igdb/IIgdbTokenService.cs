namespace MediaSet.Api.Infrastructure.Lookup.Clients.Igdb;

public interface IIgdbTokenService
{
    Task<string> GetAccessTokenAsync(CancellationToken cancellationToken);
}
