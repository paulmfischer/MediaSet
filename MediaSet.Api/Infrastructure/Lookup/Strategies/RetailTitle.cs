using System.Text.RegularExpressions;

namespace MediaSet.Api.Infrastructure.Lookup.Strategies;

/// <summary>
/// Retail noise shared by movie and game titles: barcodes, marketing tags and cut-off brackets.
/// </summary>
public static class RetailTitle
{
    private const RegexOptions Options = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;

    public static string StripNoise(string title)
    {
        // A trailing UPC-A (12) or EAN-13 (13) code is catalog data, never part of a title.
        title = Regex.Replace(title, @"\s+(?:UPC|EAN)?\s*\d{12,13}\s*$", "", Options);
        title = Regex.Replace(title, @"^\s*As Seen On(?: TV)?\b\s*[-:|]?\s*", "", Options);
        title = Regex.Replace(title, @"\s*[-:|]?\s*\bAs Seen On\b.*$", "", Options);
        return StripUnbalancedBrackets(title).Trim();
    }

    /// <summary>
    /// Removes a bracket that is opened but never closed (a truncated retail suffix such as "(DVD")
    /// and any closing bracket without an opener. Balanced brackets are preserved.
    /// </summary>
    public static string StripUnbalancedBrackets(string title)
    {
        var open = new Stack<int>();
        var unmatchedClose = new List<int>();
        for (var i = 0; i < title.Length; i++)
        {
            switch (title[i])
            {
                case '(' or '[':
                    open.Push(i);
                    break;
                case ')' or ']' when open.Count > 0:
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
            var cut = open.Min();
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
