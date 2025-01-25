namespace MediaSet.Api.Converters;

public interface IConverter
{
  object? Convert(string value);
}