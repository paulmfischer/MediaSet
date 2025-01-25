namespace MediaSet.Api.Attributes;

[AttributeUsage(AttributeTargets.Property)]
public class UploadAttribute : Attribute
{
  public string? HeaderName { get; set; }
  /// <summary>
  /// Needs to be a type of <see cref="IConverter"/>
  /// </summary>
  public Type? Converter { get; set; }
}