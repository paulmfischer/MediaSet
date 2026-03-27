using MediaSet.Api.Shared.Attributes;
using MediaSet.Api.Shared.Models;
using System.Reflection;

namespace MediaSet.Api.Infrastructure.Lookup.Strategies;

public abstract class LookupStrategyBase<TEntity, TResponse> : ILookupStrategy<TResponse>
    where TEntity : class
    where TResponse : class
{
    private static readonly IReadOnlySet<string> _supportedProperties = BuildSupportedProperties();

    public IReadOnlySet<string> SupportedProperties => _supportedProperties;

    private static IReadOnlySet<string> BuildSupportedProperties() =>
        typeof(TEntity)
            .GetProperties()
            .Select(p => (prop: p, attr: p.GetCustomAttribute<LookupPropertyAttribute>()))
            .Where(x => x.attr != null)
            .Select(x => x.attr!.Name ?? x.prop.Name.ToLowerInvariant())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

    public abstract bool CanHandle(MediaTypes entityType, IdentifierType identifierType);

    public abstract Task<IReadOnlyList<TResponse>> LookupAsync(
        IdentifierType identifierType,
        IReadOnlyDictionary<string, string> searchParams,
        CancellationToken cancellationToken);
}
