namespace MediaSet.Api.Shared.Attributes;

/// <summary>
/// Marks a property as the lookup identifier for background image lookup.
/// The property value will be used to search for cover images via external APIs.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public class LookupIdentifierAttribute : Attribute
{
}
