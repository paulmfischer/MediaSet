namespace MediaSet.Api.Services.Lookup;

/// <summary>
/// Base interface for lookup strategies. Used for type-safe strategy selection.
/// </summary>
public interface ILookupStrategy
{
    /// <summary>
    /// Gets the entity type this strategy handles (e.g., "books", "movies").
    /// </summary>
    string EntityType { get; }

    /// <summary>
    /// Determines if this strategy supports the given identifier type.
    /// </summary>
    bool SupportsIdentifierType(string identifierType);
}

/// <summary>
/// Generic lookup strategy interface with type-safe response.
/// </summary>
public interface ILookupStrategy<TResponse> : ILookupStrategy
{
    /// <summary>
    /// Performs a lookup for the given identifier and returns a typed response.
    /// </summary>
    Task<TResponse?> LookupAsync(string identifierType, string identifierValue, CancellationToken cancellationToken = default);
}
