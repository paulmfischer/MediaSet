using MediaSet.Api.Features.Entities.Models;
using System.ComponentModel;
using MediaSet.Api.Models;

namespace MediaSet.Api.Shared.Extensions;

public static class IdentifierTypeExtensions
{
    public static string ToApiString(this IdentifierType identifierType)
    {
        var field = identifierType.GetType().GetField(identifierType.ToString());
        var attribute = (DescriptionAttribute?)Attribute.GetCustomAttribute(field!, typeof(DescriptionAttribute));
        return attribute?.Description ?? identifierType.ToString().ToLowerInvariant();
    }

    public static bool TryParseIdentifierType(string identifierTypeString, out IdentifierType identifierType)
    {
        identifierType = default;

        foreach (IdentifierType enumValue in Enum.GetValues<IdentifierType>())
        {
            if (string.Equals(enumValue.ToApiString(), identifierTypeString, StringComparison.OrdinalIgnoreCase))
            {
                identifierType = enumValue;
                return true;
            }
        }

        return false;
    }

    public static string GetValidTypesString()
    {
        var types = Enum.GetValues<IdentifierType>()
          .Select(x => x.ToApiString())
          .ToArray();
        return string.Join(", ", types);
    }
}
