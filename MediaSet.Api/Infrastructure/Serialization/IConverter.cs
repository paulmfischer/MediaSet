namespace MediaSet.Api.Infrastructure.Serialization;

public interface IConverter
{
    object? Convert(string value);
}
