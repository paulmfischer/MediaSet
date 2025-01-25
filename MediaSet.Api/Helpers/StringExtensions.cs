namespace MediaSet.Api.Helpers;

public static class StringExtensions
{
  public static object? CastTo(this string value, Type type)
  {
    if (type == typeof(List<string>))
    {
      return value.Split("|").Select(val => val.Trim()).Where(val => !string.IsNullOrWhiteSpace(val)).ToList();
    }
    else if (type == typeof(int?))
    {
      return int.TryParse(value, out int result) ? result : null;
    }
    
    return value;
  }
}