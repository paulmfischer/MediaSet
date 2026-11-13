using System.Text.RegularExpressions;

namespace MediaSet.Api.Infrastructure.Lookup.Strategies;

/// <summary>
/// Retail noise shared by movie and game titles: barcodes, marketing tags and cut-off brackets.
/// </summary>
public static class RetailTitle
{
    private const RegexOptions Options = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;

    /// <summary>
    /// Removes the noise, or returns the trimmed input unchanged when only noise would remain,
    /// so a title that is nothing but a tag ("As Seen On TV") is still searched as written.
    /// </summary>
    public static string StripNoise(string rawTitle)
    {
        // A trailing UPC-A (12), EAN-13 (13) or GTIN-14 (14) code is catalog data, never part of a title.
        // EAN-8/UPC-E (8 digits) is not stripped: an 8-digit number can be a real title token.
        var title = Regex.Replace(rawTitle, @"\s+(?:(?:UPC|EAN)\s*)?\d{12,14}\s*$", "", Options);
        title = Regex.Replace(title, @"^\s*As Seen On(?: TV)?\b\s*[-:|]?\s*", "", Options);
        title = Regex.Replace(title, @"\s*[-:|]?\s*\bAs Seen On\b.*$", "", Options);
        title = StripUnbalancedBrackets(title).Trim();
        return string.IsNullOrWhiteSpace(title) ? rawTitle.Trim() : title;
    }

    /// <summary>
    /// Removes a bracket that is opened but never closed (a truncated retail suffix such as "(DVD")
    /// and any closing bracket without an opener. Balanced brackets are preserved.
    /// </summary>
    public static string StripUnbalancedBrackets(string title)
    {
        // Each closer must match the most recent opener of its own kind, so "(Bar]" is unbalanced.
        var open = new Stack<(int Index, char Closer)>();
        var unmatchedClose = new List<int>();
        for (var i = 0; i < title.Length; i++)
        {
            switch (title[i])
            {
                case '(':
                    open.Push((i, ')'));
                    break;
                case '[':
                    open.Push((i, ']'));
                    break;
                case ')' or ']' when open.Count > 0 && open.Peek().Closer == title[i]:
                    open.Pop();
                    break;
                case ')' or ']':
                    unmatchedClose.Add(i);
                    break;
            }
        }
        if (open.Count > 0)
        {
            // Everything after the first unclosed bracket is the truncated fragment.
            var cut = open.Min(o => o.Index);
            unmatchedClose.RemoveAll(i => i >= cut);
            title = title[..cut];
        }
        for (var i = unmatchedClose.Count - 1; i >= 0; i--)
        {
            title = title.Remove(unmatchedClose[i], 1);
        }
        return Regex.Replace(title, @"\s+", " ").Trim();
    }
}
