namespace MediaSet.Api.Shared.Attributes;

/// <summary>
/// Marks a property as a searchable field for entity-based lookup.
/// The property value will be used when searching for entities via external APIs.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class LookupPropertyAttribute : Attribute
{
    public string? Name { get; }

    public LookupPropertyAttribute() { }

    public LookupPropertyAttribute(string name)
    {
        Name = name;
    }
}
