using System.Text.RegularExpressions;
using MediaSet.Api.Infrastructure.Lookup;
using MediaSet.Api.Infrastructure.Lookup.Models;
using MediaSet.Api.Shared.Models;

namespace MediaSet.Api.Features.Images.Services;

public record ImageCandidate(string Title, string? ImageUrl, string Date = "",
    IReadOnlyList<string>? Creators = null, IReadOnlyList<string>? Platforms = null,
    LookupProvenance? Provenance = null, string Format = "", string Subtitle = "",
    IReadOnlyList<string>? Publishers = null);

public static class ImageCandidateMatcher
{
    public static string Normalize(string value) => Regex.Replace(value.ToLowerInvariant(), @"[^\p{L}\p{N}]+", " ").Trim();

    private static string Platform(string value) => Normalize(value) switch
    {
        "ps1" or "psx" => "playstation",
        "ps2" => "playstation 2",
        "ps3" => "playstation 3",
        "ps4" => "playstation 4",
        "ps5" => "playstation 5",
        "ds" => "nintendo ds",
        "3ds" => "nintendo 3ds",
        "n64" => "nintendo 64",
        "switch" => "nintendo switch",
        var other => other
    };

    private static string Year(string value) => Regex.Match(value, @"\b(?:18|19|20)\d{2}\b").Value;

    public static Dictionary<string, string> SearchContext(IEntity entity)
    {
        var context = new Dictionary<string, string> { ["title"] = entity.Title };
        switch (entity)
        {
            case Game game:
                context["platform"] = game.Platform;
                context["releasedate"] = game.ReleaseDate;
                break;
            case Movie movie:
                context["releasedate"] = movie.ReleaseDate;
                if (movie.IsTvSeries) { context["isTvSeries"] = "true"; }
                break;
            case Book book:
                context["author"] = string.Join(" ", book.Authors);
                context["publisher"] = book.Publisher;
                context["publicationdate"] = book.PublicationDate;
                context["subtitle"] = book.Subtitle;
                break;
            case Music music:
                context["artist"] = music.Artist;
                context["releasedate"] = music.ReleaseDate;
                context["format"] = music.Format;
                break;
        }
        return context.Where(p => !string.IsNullOrWhiteSpace(p.Value)).ToDictionary();
    }

    public static IReadOnlyList<ImageCandidate> Select(IEntity entity, IEnumerable<ImageCandidate> candidates)
    {
        var context = SearchContext(entity);
        var date = context.GetValueOrDefault("releasedate") ?? context.GetValueOrDefault("publicationdate") ?? "";
        var creators = entity switch { Book b => b.Authors, Music m => new List<string> { m.Artist }, _ => [] };
        var matching = candidates.Where(c => Normalize(c.Provenance?.Title ?? c.Title) == Normalize(entity.Title))
            .Where(c => string.IsNullOrEmpty(Year(date)) || Year(date) == Year(c.Date)
                || c.Provenance?.MatchYears?.Contains(Year(date)) == true)
            .Where(c => entity is not Movie { IsTvSeries: true }
                || c.Provenance?.ProviderId.StartsWith("tv:", StringComparison.Ordinal) == true)
            .Where(c => creators.Count == 0 || creators.All(a => string.IsNullOrWhiteSpace(a)
                || c.Creators?.Any(n => Normalize(n) == Normalize(a)) == true))
            .Where(c => entity is not Game g || string.IsNullOrWhiteSpace(g.Platform)
                || c.Platforms?.Any(p => Platform(p) == Platform(g.Platform)) == true)
            .Where(c => entity is not Music music || string.IsNullOrWhiteSpace(music.Format)
                || Normalize(c.Format) == Normalize(music.Format))
            .Where(c => entity is not Book book ||
                (string.IsNullOrWhiteSpace(book.Subtitle) || Normalize(c.Subtitle) == Normalize(book.Subtitle))
                && (string.IsNullOrWhiteSpace(book.Publisher)
                    || c.Publishers?.Any(p => Normalize(p) == Normalize(book.Publisher)) == true))
            .ToList();

        // Different provider identities with equally good evidence remain ambiguous, even if
        // only one has artwork. Duplicate observations of the same identity may share artwork.
        var identities = matching.Select((candidate, index) => (candidate, index))
            .GroupBy(c => c.candidate.Provenance is { } p ? $"{p.Provider}:{p.ProviderId}" : $"unknown:{c.index}").ToList();
        if (identities.Count > 1)
        {
            throw new ProviderLookupException(entity.Type.ToString(), ImageLookupOutcome.AmbiguousMatch,
                "Equally matching provider identities cannot be distinguished by stored metadata");
        }
        return matching.OrderByDescending(c => !string.IsNullOrWhiteSpace(c.ImageUrl)).ToList();
    }
}
