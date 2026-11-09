using System.Globalization;
using System.Text.RegularExpressions;
using MediaSet.Api.Features.Plex.Models;

namespace MediaSet.Api.Features.Plex.Services;

/// <summary>
/// Reads which seasons a TV entry covers from its title, and gives the title without that season text.
/// Pure: no I/O, never throws.
///
/// Recognized, case-insensitively and with any whitespace or dash style:
/// - one season: "Season 2", "Series 2" (UK), "S2", "Season Two", "Second Season", "2nd Season",
///   "The Complete Season 2", "The Complete Second Season";
/// - a range: "Seasons 1-3", "Season 1 to 3", "Seasons 1 through 3", "S1-S3", and a consecutive list
///   such as "Seasons 1 &amp; 2", "Seasons 1, 2 and 3";
/// - the whole show: "Complete Series", "The Complete Collection", "Complete Seasons", "All Seasons".
///
/// Everything else is unreadable, so the entry stays unmatched until linked by hand: no season text at all,
/// more than one season phrase ("Season 1 Season 2", "Complete Series (Seasons 1-5)"), a reversed or
/// non-consecutive list ("Seasons 3-1", "Seasons 1 &amp; 3"), season 0 or numbers above 99, and a season
/// phrase joined to something else ("Seasons 1-3 + Movie", "Season 2 and the Specials") or followed by another number ("Season Twenty One"). A season keyword is
/// required for numbers, so digits in a title ("24: Season 3") are never read as a season.
/// </summary>
public static partial class PlexTvTitleParser
{
    private const int MaxSeason = 99;
    // Longest first where one word starts another ("eighteen" before "eight"), so alternation never stops short
    private const string Words = "eighteen|eight|sixteen|six|seventeen|seven|nineteen|nine|fourteen|four|fifteen|five|thirteen|three|twelve|twenty|eleven|one|two|ten";
    private const string Ordinals = "first|second|third|fourth|fifth|sixth|seventh|eighth|ninth|tenth|eleventh|twelfth|thirteenth|fourteenth|fifteenth|sixteenth|seventeenth|eighteenth|nineteenth|twentieth";
    private const string Num = @"(?:\d{1,3}|" + Words + ")";
    private const string RangeSeparator = @"(?:\s*[-–—]\s*|\s+(?:to|through|thru)\s+)";
    private const string ListSeparator = @"(?:\s*[&,+]\s*|\s+and\s+|\s*,\s*and\s+)";
    private const string Edge = @"(?<![\p{L}\p{N}])";
    private const string EndEdge = @"(?![\p{L}\p{N}])";

    private static readonly string[] OrdinalWords = [.. Ordinals.Split('|')];
    private static readonly string[] NumberWords =
        ["one", "two", "three", "four", "five", "six", "seven", "eight", "nine", "ten", "eleven", "twelve", "thirteen", "fourteen", "fifteen", "sixteen", "seventeen", "eighteen", "nineteen", "twenty"];

    private const RegexOptions Options = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;

    // "Seasons 1-3", "Series 2", "Season Two", "Seasons 1 & 2"; the lead "(the) complete" and a trailing "complete" are part of the phrase
    [GeneratedRegex(Edge + @"(?:(?:the\s+)?complete\s+)?(?:seasons?|series)\s+(?<first>" + Num + @")(?<more>(?:(?:" + RangeSeparator + "|" + ListSeparator + @")" + Num + @")*)(?:\s+complete)?" + EndEdge, Options)]
    private static partial Regex NumberedPhrase();

    // "Second Season", "2nd Season", "The Complete Second Season"
    [GeneratedRegex(Edge + @"(?:(?:the\s+)?complete\s+)?(?<ordinal>" + Ordinals + @"|\d{1,3}(?:st|nd|rd|th))\s+(?:season|series)" + EndEdge, Options)]
    private static partial Regex OrdinalPhrase();

    [GeneratedRegex(Edge + @"(?:(?:the\s+)?complete\s+(?:series|collection|seasons?)|all\s+seasons)" + EndEdge, Options)]
    private static partial Regex CompletePhrase();

    // "S2", "S1-S3"
    [GeneratedRegex(Edge + @"S(?<a>\d{1,2})(?:\s*-\s*S?(?<b>\d{1,2}))?" + EndEdge, Options)]
    private static partial Regex ShortPhrase();

    // A season phrase joined to other content, before or after it, or followed by another number
    // ("Season Twenty One" is not season 20)
    [GeneratedRegex(@"^\s*(?:[&+]|and\b|plus\b|with\b)|^\s+(?:\d|(?:" + Words + @")\b)", Options)]
    private static partial Regex LeadingConnector();

    [GeneratedRegex(@"(?:[&+]|\band|\bplus|\bwith)\s*$", Options)]
    private static partial Regex TrailingConnector();

    [GeneratedRegex(@"[\(\[][^\)\]]*(?:\b(?:19|20)\d{2}\b|dvd|blu-?ray|\bbd\b|\bhd\b|uhd|4k|disc|remaster)[^\)\]]*[\)\]]", Options)]
    private static partial Regex FormatNote();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Spaces();

    // Separators left side by side where a phrase was cut out: "Show: : The Dark Years"
    [GeneratedRegex(@"(?:\s*[:,\-\u2013\u2014]\s*){2,}")]
    private static partial Regex DoubleSeparator();

    private sealed record Found(int Index, int Length, PlexScope Scope);

    /// <summary>The scope the title names, or <see cref="PlexScope.Unreadable"/>.</summary>
    public static PlexScope ParseScope(string? title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return PlexScope.Unreadable;
        }

        var text = Spaces().Replace(title.Trim(), " ");
        var found = FindPhrases(text);
        if (found.Count != 1 || !found[0].Scope.IsReadable)
        {
            return PlexScope.Unreadable;
        }

        var phrase = found[0];
        var before = text[..phrase.Index];
        var after = text[(phrase.Index + phrase.Length)..];
        return LeadingConnector().IsMatch(after) || TrailingConnector().IsMatch(before)
            ? PlexScope.Unreadable
            : phrase.Scope;
    }

    /// <summary>
    /// The title with the season phrase(s) removed, for searching a metadata source: "Breaking Bad: Season 2"
    /// gives "Breaking Bad". Separators left at the seam, and year or format notes in brackets ("(2008)",
    /// "[Blu-ray]"), are dropped too. A title with no season text comes back trimmed.
    /// </summary>
    public static string CleanTitle(string? title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return string.Empty;
        }

        var text = Spaces().Replace(title.Trim(), " ");
        foreach (var phrase in FindPhrases(text).OrderByDescending(p => p.Index))
        {
            text = text.Remove(phrase.Index, phrase.Length).Insert(phrase.Index, " ");
        }

        text = FormatNote().Replace(text, " ");
        text = DoubleSeparator().Replace(Spaces().Replace(text, " ").Trim(), ": ");
        return text.Trim(' ', ':', '-', '–', '—', ',', '&', '+', '/').Trim();
    }

    /// <summary>Every season phrase in the text, left to right, without overlaps. Numbered phrases win over the others.</summary>
    private static List<Found> FindPhrases(string text)
    {
        var accepted = new List<Found>();

        void Add(Found candidate)
        {
            if (!accepted.Any(a => candidate.Index < a.Index + a.Length && a.Index < candidate.Index + candidate.Length))
            {
                accepted.Add(candidate);
            }
        }

        foreach (Match m in NumberedPhrase().Matches(text))
        {
            Add(new Found(m.Index, m.Length, NumberedScope(m)));
        }
        foreach (Match m in OrdinalPhrase().Matches(text))
        {
            var word = m.Groups["ordinal"].Value.ToLowerInvariant();
            var n = Array.IndexOf(OrdinalWords, word) + 1;
            if (n == 0)
            {
                n = ToNumber(word.TrimEnd('s', 't', 'n', 'd', 'r', 'h'));
            }
            Add(new Found(m.Index, m.Length, Single(n)));
        }
        foreach (Match m in CompletePhrase().Matches(text))
        {
            Add(new Found(m.Index, m.Length, PlexScope.AllSeasons));
        }
        foreach (Match m in ShortPhrase().Matches(text))
        {
            var a = ToNumber(m.Groups["a"].Value);
            var scope = m.Groups["b"].Success ? Between2(a, ToNumber(m.Groups["b"].Value)) : Single(a);
            Add(new Found(m.Index, m.Length, scope));
        }

        return [.. accepted.OrderBy(a => a.Index)];
    }

    private static PlexScope NumberedScope(Match m)
    {
        var first = ToNumber(m.Groups["first"].Value);
        var more = m.Groups["more"].Value;
        if (more.Length == 0)
        {
            return Single(first);
        }

        var numbers = new List<int> { first };
        var rangeSeparators = 0;
        foreach (Match part in NumberTokens().Matches(more))
        {
            if (part.Groups["num"].Success)
            {
                numbers.Add(ToNumber(part.Groups["num"].Value));
            }
            else if (part.Groups["range"].Success)
            {
                rangeSeparators++;
            }
        }

        if (numbers.Any(n => n == 0))
        {
            return PlexScope.Unreadable;
        }
        if (numbers.Count == 2)
        {
            // "1-3" is a range of any width; "1 & 2" is a list and must be consecutive
            return rangeSeparators == 1 ? Between2(numbers[0], numbers[1]) : (numbers[1] == numbers[0] + 1 ? Between2(numbers[0], numbers[1]) : PlexScope.Unreadable);
        }

        var consecutive = rangeSeparators == 0 && numbers.Zip(numbers.Skip(1), (x, y) => y == x + 1).All(b => b);
        return consecutive ? Between2(numbers[0], numbers[^1]) : PlexScope.Unreadable;
    }

    // Splits the tail of a numbered phrase into numbers and range separators
    [GeneratedRegex(@"(?<range>[-–—]|\b(?:to|through|thru)\b)|(?<num>" + Num + ")", Options)]
    private static partial Regex NumberTokens();

    private static PlexScope Single(int season) => season is >= 1 and <= MaxSeason ? PlexScope.Single(season) : PlexScope.Unreadable;

    private static PlexScope Between2(int from, int to) =>
        from is < 1 or > MaxSeason || to is < 1 or > MaxSeason ? PlexScope.Unreadable : PlexScope.Between(from, to);

    private static int ToNumber(string token)
    {
        if (int.TryParse(token, NumberStyles.None, CultureInfo.InvariantCulture, out var n))
        {
            return n;
        }
        var i = Array.IndexOf(NumberWords, token.ToLowerInvariant());
        return i < 0 ? 0 : i + 1;
    }
}
