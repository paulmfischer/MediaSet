namespace MediaSet.Api.Helpers;
internal static class StringArrayExtensions
{
  public static string GetByHeader(this string[]? fields, string[]? headers, string titleField)
  {
    if (fields is null || headers is null)
    {
      return string.Empty;
    }
    var headerIndex = Array.FindIndex(headers, hf => hf.Equals(titleField)); // newBookFields[Array.FindIndex(headerFields, hf => hf.Equals(nameof(Book.Title)))]
    var fieldData = fields[headerIndex];
    return fieldData; // string.IsNullOrWhiteSpace(fieldData) ? null : fieldData;
  }
}