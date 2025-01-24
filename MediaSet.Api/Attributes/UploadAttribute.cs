namespace MediaSet.Api.Attributes;

[AttributeUsage(AttributeTargets.Property)]
public class UploadAttribute : Attribute
{
  public string? HeaderName { get; set; }
}