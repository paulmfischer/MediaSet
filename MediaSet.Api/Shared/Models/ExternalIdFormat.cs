namespace MediaSet.Api.Shared.Models;

/// <summary>
/// Rules for the <c>source:value</c> external identifier carried by a Movie.
/// </summary>
public static class ExternalIdFormat
{
    public const string ErrorMessage =
        "ExternalId must be in 'source:value' form with a non-empty source and value and no whitespace (for example 'tmdbid:1234').";

    public static string ForTmdb(int id) => $"tmdbid:{id}";

    /// <summary>
    /// Validates <paramref name="input"/>. Null or empty yields a null result. Otherwise the source
    /// is lowercased and the value is left exactly as entered.
    /// </summary>
    public static bool TryNormalize(string? input, out string? normalized)
    {
        normalized = null;
        if (string.IsNullOrEmpty(input))
        {
            return true;
        }

        if (input.Any(char.IsWhiteSpace))
        {
            return false;
        }

        var parts = input.Split(':');
        if (parts.Length != 2 || parts[0].Length == 0 || parts[1].Length == 0)
        {
            return false;
        }

        normalized = $"{parts[0].ToLowerInvariant()}:{parts[1]}";
        return true;
    }
}
