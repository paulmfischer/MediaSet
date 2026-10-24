using System.Text.RegularExpressions;

namespace MediaSet.Api.Infrastructure.Lookup.Strategies;

public static class RetailVideoTitle
{
    private const RegexOptions Options = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;
    private const string Manufacturers =
        @"Sony Pictures Entertainment|Sony Pictures|Warner Home Video|Warner Bros(?:\.)?|Paramount(?: Home Entertainment)?|Lions Gate|Lionsgate|Universal Studios(?: Home Entertainment)?|Walt Disney Home Entertainment";
    private const string Format = @"DVD|Blu-?ray|Blue-?ray|DIGITAL VIDEO DISC|4K(?: UHD)?|BD|UHD|Digital|HD";

    public static string Clean(string rawTitle, string? brand = null)
    {
        var title = rawTitle.Trim();
        // Only known distributors are safe to strip when product brand metadata is inaccurate.
        title = Regex.Replace(title, @"^(?:" + Manufacturers + @")(?:\s*[-:]\s*|\s+|$)", "", Options);
        if (!string.IsNullOrWhiteSpace(brand) && Regex.IsMatch(brand.Trim(), "^(?:" + Manufacturers + ")$", Options))
        {
            title = Regex.Replace(title, @"\s+" + Regex.Escape(brand.Trim()) + "$", "", Options);
        }
        title = Regex.Replace(title, @"\s*\|\s*(?:" + Format + @")\b.*$", "", Options);
        // An HD label before an episode marker is interior metadata, not the end of the title.
        title = Regex.Replace(title, @"\s*\[HD\](?=\s+-\s+S\d+E\d+\s+-\s+)", "", Options);
        // A recognized retail annotation can delimit trailing catalog junk; meaningful brackets survive.
        title = Regex.Replace(title,
            @"\s*[\[(](?:" + Format + @"|WS|Widescreen|Full\s*screen|(?:\d+|Two|Three|Four|Five|Six)[ -]?Discs?|Collector'?s? Edition|Special Edition)\b[^\])]*[\])].*$",
            "", Options);
        title = Regex.Replace(title, @"\s*:\s*\d+(?:st|nd|rd|th)\s+Anniversary\s+Edition\s*$", "", Options);
        title = Regex.Replace(title, @",\s+(?:\d+|Two|Three|Four|Five|Six)\s+Discs?\b.*$", "", Options);
        title = Regex.Replace(title, @"\s+(?:\d+|Two|Three|Four|Five|Six)[ -]?Discs?\b.*Edition.*$", "", Options);
        title = Regex.Replace(title, @"\s+(?:" + Format + @")\s*\+\s*(?:" + Format + @").*$", "", Options);
        title = Regex.Replace(title, @"\s+-\s*(?:" + Format + @")\b.*$", "", Options);
        title = Regex.Replace(title, @"\s+(?:" + Format + @")\s*$", "", Options);
        if (!string.Equals(title, rawTitle.Trim(), StringComparison.Ordinal))
        {
            title = Regex.Replace(title, @"\s+(?:LIKE NEW|NEW|USED|SEALED|MINT|OPENED|UNOPENED|TESTED)\s*$", "", Options);
        }
        // Preserve collection intent except when an explicit season already identifies the TV release.
        if (Regex.IsMatch(title, @"\bSeason\s+\d+\s+Complete Collection$", Options))
        {
            title = Regex.Replace(title, @"\s+Complete Collection$", "", Options);
        }
        title = Regex.Replace(title, @"\s+", " ").Trim();
        var article = Regex.Match(title, @"^(.+?)\s+(A|The)$", Options);
        return article.Success ? $"{article.Groups[2].Value} {article.Groups[1].Value}" : title;
    }

    public static bool IsCollection(string title) => !string.IsNullOrWhiteSpace(CollectionKey(title))
        && !Regex.IsMatch(title, @"\bBook\s+\d+\s+Collection\b", Options)
        && Regex.IsMatch(title,
            @"\s+(?:trilogy|quadrilogy|collection|box\s*set|bundle|double feature|\d+[ -](?:movie|film)\s*(?:set|pack))\s*$|\s[+/]\s", Options);

    public static string CollectionKey(string title) => VideoTitle.Normalize(Regex.Replace(title,
        @"\s*[:\-]?\s*(?:the\s+)?(?:complete\s+)?(?:trilogy|quadrilogy|collection)\s*$", "", Options));
}
