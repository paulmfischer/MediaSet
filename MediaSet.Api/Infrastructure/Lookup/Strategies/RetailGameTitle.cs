using System.Text.RegularExpressions;

namespace MediaSet.Api.Infrastructure.Lookup.Strategies;

/// <summary>
/// Conservative cleaning for stored game titles: retail noise that is never part of a game name.
/// Platforms, editions and bundles are kept because they identify the release.
/// </summary>
public static class RetailGameTitle
{
    private const RegexOptions Options = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;

    // Only multi-word or unambiguous publisher names; "Capcom" or "2K" can end a real title.
    private const string Publishers =
        @"Rockstar Games|Electronic Arts|Activision(?: Blizzard)?|Ubisoft(?: Entertainment)?|Bethesda Softworks|"
        + @"Square Enix|Bandai Namco(?: Entertainment| Games)?|2K Games|Take-Two Interactive|"
        + @"Warner Bros\.? (?:Games|Interactive(?: Entertainment)?)|Sony (?:Computer|Interactive) Entertainment|"
        + @"Microsoft (?:Studios|Game Studios)|Nintendo of America|Sega of America|THQ(?: Nordic)?";

    public static string Clean(string rawTitle)
    {
        var title = RetailTitle.StripNoise(rawTitle.Trim());
        title = Regex.Replace(title, @"\s*[-:|]?\s+(?:" + Publishers + @")\s*$", "", Options);
        // A publisher also appears as its own " - " segment before other metadata ("- Rockstar Games - PS4").
        title = Regex.Replace(title, @"\s+-\s+(?:" + Publishers + @")(?=\s+-\s+)", "", Options);
        title = Regex.Replace(title, @"\s+", " ").Trim();
        return string.IsNullOrWhiteSpace(title) ? rawTitle.Trim() : title;
    }
}
