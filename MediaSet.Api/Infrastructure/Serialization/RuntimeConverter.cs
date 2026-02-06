namespace MediaSet.Api.Infrastructure.Serialization;

public class RuntimeConverter : IConverter
{
    /// <summary>
    /// Convert values of "01:38" to minutes, e.g. "01:38" -> 98
    /// </summary>
    /// <param name="value">Value to convert</param>
    /// <returns>Minutes from the string, null if the string is null or whitespace.</returns>
    public object? Convert(string value)
    {
        var timeSplit = value.Split(":");
        if (timeSplit.Length > 1)
        {
            var hoursByMinutes = int.Parse(timeSplit[0]) * 60;
            var minutes = int.Parse(timeSplit[1]);
            return hoursByMinutes + minutes;
        }
        else if (timeSplit.Length == 1)
        {
            var minutes = int.Parse(timeSplit[0]);
            return minutes;
        }

        return null;
    }
}
