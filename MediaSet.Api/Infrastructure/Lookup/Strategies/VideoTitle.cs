using System.Text.RegularExpressions;

namespace MediaSet.Api.Infrastructure.Lookup.Strategies;

public record VideoTitle(string Title, string SeriesTitle, int? Season = null, string? Special = null,
    bool IsSeries = false)
{
    private const RegexOptions Options = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;
    private static readonly string[] Ordinals =
        ["first", "second", "third", "fourth", "fifth", "sixth", "seventh", "eighth", "ninth", "tenth",
         "eleventh", "twelfth", "thirteenth", "fourteenth", "fifteenth", "sixteenth", "seventeenth",
         "eighteenth", "nineteenth", "twentieth"];

    public static string Normalize(string title) =>
        Regex.Replace(title.ToLowerInvariant(), @"[^\p{L}\p{N}]", string.Empty);

    public static VideoTitle Parse(string title)
    {
        var special = Regex.Match(title,
            @"^(.*?)\s+-\s+.*?specials.*?\s+-\s+S\d+E\d+\s+-\s+(.+?)(?:\s*\[HD\])?\s*$", Options);
        if (special.Success)
        {
            return new(title, special.Groups[1].Value.Trim(), Special: special.Groups[2].Value.Trim());
        }
        var match = Regex.Match(title,
            @"^(.*?)\s*[:\-]?\s*(?:(?:the\s+)?complete\s+)?(?:season|series)\s+(\d+)(?:\s*\([^)]*\))?\s*$", Options);
        if (!match.Success)
        {
            match = Regex.Match(title,
                @"^(.*?)\s*[:\-]?\s*(?:the\s+)?(?:complete\s+)?(" + string.Join("|", Ordinals)
                + @")\s+(?:season|series)(?:\s*\([^)]*\))?\s*$", Options);
        }
        if (match.Success)
        {
            var number = int.TryParse(match.Groups[2].Value, out var numeric)
                ? numeric : Array.IndexOf(Ordinals, match.Groups[2].Value.ToLowerInvariant()) + 1;
            return new(title, match.Groups[1].Value.Trim().TrimEnd(':', '-').Trim(), number, IsSeries: true);
        }
        match = Regex.Match(title, @"^(.*?)\s*[:\-]?\s*(?:the\s+)?complete\s+(?:series|collection)\s*$", Options);
        if (match.Success)
        {
            return new(title, match.Groups[1].Value.Trim().TrimEnd(':', '-').Trim(), IsSeries: true);
        }
        match = Regex.Match(title, @"^(.*?)\s*[:\-]\s+(.+)$", Options);
        return match.Success
            ? new(title, match.Groups[1].Value.Trim(), Special: match.Groups[2].Value.Trim())
            : new(title, title);
    }
}
