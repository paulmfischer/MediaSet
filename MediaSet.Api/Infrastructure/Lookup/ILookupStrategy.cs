using MediaSet.Api.Features.Lookup.Models;
using MediaSet.Api.Features.Entities.Models;
using MediaSet.Api.Features.Entities.Models;

namespace MediaSet.Api.Infrastructure.Lookup;

public interface ILookupStrategyBase
{
    bool CanHandle(MediaTypes entityType, IdentifierType identifierType);
}

public interface ILookupStrategy<TResponse> : ILookupStrategyBase where TResponse : class
{
    Task<TResponse?> LookupAsync(IdentifierType identifierType, string identifierValue, CancellationToken cancellationToken);
}
