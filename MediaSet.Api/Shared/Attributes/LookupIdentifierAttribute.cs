using MediaSet.Api.Shared.Models;

namespace MediaSet.Api.Shared.Attributes;

/// <summary>
/// Marks a property as a lookup identifier for background image lookup.
/// Multiple properties can be marked with different orders; the service will try
/// them in ascending order and use the first one with a non-empty value.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public class LookupIdentifierAttribute : Attribute
{
    /// <summary>
    /// The priority order for this identifier. Lower values are tried first.
    /// </summary>
    public int Order { get; }

    /// <summary>
    /// The identifier type to use when searching via this property's value.
    /// </summary>
    public IdentifierType IdentifierType { get; }

    public LookupIdentifierAttribute(int order, IdentifierType identifierType)
    {
        Order = order;
        IdentifierType = identifierType;
    }
}
