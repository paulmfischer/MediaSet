namespace MediaSet.Api.Bindings;

public interface IParameter
{ }

public class Parameter<T> : IParameter
{
    private string? stringValue;
    private T value = default!;

    public bool IsValid { get; private set; }

    public static bool TryParse(string value, out Parameter<T> result)
    {
        return TryParse(value, null!, out result);
    }

    public static bool TryParse(string value, IFormatProvider provider, out Parameter<T> result)
    {
        result = new Parameter<T>
        {
            stringValue = value
        };

        var success = TryParseParameter(value, provider, out var resultValue);
        if (!success)
        {
            return true;
        }

        result.value = resultValue;
        result.IsValid = true;
        return true;
    }

    private static bool TryParseParameter(string value, IFormatProvider provider, out T result)
    {
        var success = false;
        result = default!;

        if (typeof(T) == typeof(bool))
        {
            success = bool.TryParse(value, out var parsedValue);
            result = (T)(object)parsedValue;
            return success;
        }

        if (typeof(T).IsEnum)
        {
            success = Enum.TryParse(typeof(T), value, true, out var parsedValue);
            success = success && Enum.IsDefined(typeof(T), parsedValue!);
            if (success && parsedValue != null)
            {
                result = (T)parsedValue;
            }

            return success;
        }

        if (typeof(T) == typeof(DateTime))
        {
            success = DateTime.TryParse(value, provider, out var parsedValue);
            result = (T)(object)parsedValue;
            return success;
        }

        return success;
    }

    public static implicit operator T(Parameter<T> p)
    {
        return p.value;
    }

    public override string? ToString()
    {
        return stringValue;
    }
}
