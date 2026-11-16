using System.Text.RegularExpressions;

namespace MediaSet.Api.Infrastructure.Lookup.Strategies;

public record VideoTitle(string Title, string SeriesTitle, int? Season = null, string? Special = null,
    bool IsSeries = false, bool SeasonFallback = false, bool IsVolume = false)
{
    private const RegexOptions Options = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;
    private static readonly string[] _ordinals =
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
            @"^(.*?)\s*[:\-]?\s*(?:(?:the\s+)?complete\s+)?(?:season|series)\s+(\d+)(\.\d+)?(?:\s+Comp(?:lete)?)?(?:\s*\([^)]*\))?\s*$", Options);
        if (!match.Success)
        {
            // Retail shorthand: "Gilmore Girls S4 Comp".
            match = Regex.Match(title,
                @"^(.+?)\s*[:\-]?\s+S(\d{1,2})(?:\s+(?:Comp(?:lete)?(?:\s+(?:Series|Season|Collection|Set))?|Collection|Set))?\s*$",
                Options);
        }
        if (!match.Success)
        {
            match = Regex.Match(title,
                @"^(.*?)\s*[:\-]?\s*(?:the\s+)?(?:complete\s+)?(" + string.Join("|", _ordinals)
                + @")\s+(?:season|series)(?:\s*\([^)]*\))?\s*$", Options);
        }
        if (match.Success)
        {
            var number = int.TryParse(match.Groups[2].Value, out var numeric)
                ? numeric : Array.IndexOf(_ordinals, match.Groups[2].Value.ToLowerInvariant()) + 1;
            // A split season ("Season 3.5") has no TMDB season of its own: try the whole season,
            // then the series.
            return new(title, match.Groups[1].Value.Trim().TrimEnd(':', '-').Trim(), number, IsSeries: true,
                SeasonFallback: match.Groups[3].Success);
        }
        // A multi-season set or a numbered volume of a series has no TMDB season of its own:
        // the verified series artwork represents it. Only the comma form is a volume ("Kill Bill: Vol. 1" is a film).
        match = Regex.Match(title,
            @"^(.+?)\s*[,:\-]?\s*(?:complete\s+)?seasons?\s+\d+\s*(?:-|\u2013|&|and|to|,)\s*\d+(?:\s+Comp(?:lete)?)?\s*$", Options);
        if (!match.Success)
        {
            match = Regex.Match(title, @"^(.+?),\s*(?:Vol(?:ume|\.)?)\s*\d+\s*$", Options);
        }
        if (match.Success)
        {
            // A comma volume can also be a film ("Kill Bill, Vol. 1"): the lookup searches films first and uses the series only when no film matches.
            return new(title, match.Groups[1].Value.Trim().TrimEnd(',', ':', '-').Trim(), IsSeries: true,
                IsVolume: !Regex.IsMatch(title, @"seasons?\s+\d", Options));
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
