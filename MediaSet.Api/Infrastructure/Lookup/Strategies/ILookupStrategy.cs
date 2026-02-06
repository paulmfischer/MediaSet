using MediaSet.Api.Shared.Models;

namespace MediaSet.Api.Infrastructure.Lookup.Strategies;

public interface ILookupStrategyBase
{
    bool CanHandle(MediaTypes entityType, IdentifierType identifierType);
}

public interface ILookupStrategy<TResponse> : ILookupStrategyBase where TResponse : class
{
    Task<TResponse?> LookupAsync(IdentifierType identifierType, string identifierValue, CancellationToken cancellationToken);
}
