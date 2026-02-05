namespace MediaSet.Api.Infrastructure.Serialization;

public class BoolConverter : IConverter
{
    /// <summary>
    /// Convert from 0/1/false/true to false/true
    /// </summary>
    /// <param name="value">Value to convert</param>
    /// <returns>Boolean value from a string</returns>
    public object Convert(string value)
    {
        if (bool.TryParse(value, out bool result))
        {
            return result;
        }
        else
        {
            return value.Equals("1");
        }
    }
}
