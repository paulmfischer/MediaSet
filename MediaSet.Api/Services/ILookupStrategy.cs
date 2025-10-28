using MediaSet.Api.Models;

namespace MediaSet.Api.Services;

public interface ILookupStrategy<TResponse> where TResponse : class
{
    Task<TResponse?> LookupAsync(IdentifierType identifierType, string identifierValue, CancellationToken cancellationToken);
    bool CanHandle(MediaTypes entityType, IdentifierType identifierType);
}
