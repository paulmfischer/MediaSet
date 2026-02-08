using System.Reflection;
using MediaSet.Api.Shared.Attributes;

namespace MediaSet.Api.Shared.Extensions;

internal static class StringArrayExtensions
{
    public static string? GetValueByHeader<TEntity>(this string[] fields, IList<string> headerFields, PropertyInfo propertyInfo)
    {
        UploadAttribute? uploadAttribute = (UploadAttribute?)Attribute.GetCustomAttribute(propertyInfo, typeof(UploadAttribute));
        string headerName = uploadAttribute != null && !string.IsNullOrWhiteSpace(uploadAttribute.HeaderName) ? uploadAttribute.HeaderName : propertyInfo.Name;

        var headerIndex = headerFields.IndexOf(headerName);
        if (headerIndex < 0)
        {
            return null;
        }
        var fieldData = fields.ElementAt(headerIndex);

        return fieldData;
    }
}
