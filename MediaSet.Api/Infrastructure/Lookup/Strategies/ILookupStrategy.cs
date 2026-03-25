using MediaSet.Api.Shared.Models;

namespace MediaSet.Api.Infrastructure.Lookup.Strategies;

public interface ILookupStrategyBase
{
    bool CanHandle(MediaTypes entityType, IdentifierType identifierType);
    IReadOnlySet<string> SupportedProperties { get; }
}

public interface ILookupStrategy<TResponse> : ILookupStrategyBase where TResponse : class
{
    Task<IReadOnlyList<TResponse>> LookupAsync(
        IdentifierType identifierType,
        IReadOnlyDictionary<string, string> searchParams,
        CancellationToken cancellationToken);
}
