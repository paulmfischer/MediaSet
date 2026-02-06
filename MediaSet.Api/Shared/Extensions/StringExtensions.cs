using System.Reflection;
using MediaSet.Api.Infrastructure.Serialization;
using MediaSet.Api.Shared.Attributes;

namespace MediaSet.Api.Shared.Extensions;

public static class StringExtensions
{
    /// <summary>
    /// Convert string to the type either defined with the <see cref="UploadAttribute.Converter"/> if one provided
    /// or what the <see cref="PropertyInfo.PropertyType"/> is defined as on the property.
    /// </summary>
    /// <param name="value">Value to convert from.</param>
    /// <param name="propertyInfo"><see cref="PropertyInfo"/> about the property</param>
    /// <returns>Converted value if it can be converted, otherwise the initial value.</returns>
    public static object? CastTo(this string value, PropertyInfo propertyInfo)
    {
        UploadAttribute? uploadAttribute = (UploadAttribute?)Attribute.GetCustomAttribute(propertyInfo, typeof(UploadAttribute));
        if (uploadAttribute != null && uploadAttribute.Converter != null)
        {
            var converterType = uploadAttribute.Converter;
            if (Activator.CreateInstance(converterType) is IConverter converter)
            {
                var convertedValue = converter.Convert(value);
                return convertedValue;
            }
        }
        else
        {
            if (propertyInfo.PropertyType == typeof(List<string>))
            {
                return value.Split("|").Select(val => val.Trim()).Where(val => !string.IsNullOrWhiteSpace(val)).ToList();
            }
            else if (propertyInfo.PropertyType == typeof(int?))
            {
                return int.TryParse(value, out int result) ? result : null;
            }
        }

        return value;
    }
}
