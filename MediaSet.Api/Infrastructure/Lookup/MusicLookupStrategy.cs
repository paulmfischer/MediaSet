using MediaSet.Api.Features.Lookup.Models;
using MediaSet.Api.Features.Entities.Models;
using MediaSet.Api.Features.Entities.Models;
using Serilog;
using SerilogTracing;

namespace MediaSet.Api.Infrastructure.Lookup;

public class MusicLookupStrategy : ILookupStrategy<MusicResponse>
{
    private readonly IMusicBrainzClient _musicBrainzClient;
    private readonly ILogger<MusicLookupStrategy> _logger;

    private static readonly IdentifierType[] _supportedIdentifierTypes =
    [
        IdentifierType.Upc,
        IdentifierType.Ean
    ];

    public MusicLookupStrategy(
        IMusicBrainzClient musicBrainzClient,
        ILogger<MusicLookupStrategy> logger)
    {
        _musicBrainzClient = musicBrainzClient;
        _logger = logger;
    }

    public bool CanHandle(MediaTypes entityType, IdentifierType identifierType)
    {
        return entityType == MediaTypes.Musics && _supportedIdentifierTypes.Contains(identifierType);
    }

    public async Task<MusicResponse?> LookupAsync(
        IdentifierType identifierType,
        string identifierValue,
        CancellationToken cancellationToken)
    {
        using var activity = Log.Logger.StartActivity("MusicLookup {IdentifierType}", new { IdentifierType = identifierType, identifierValue });
        
        _logger.LogInformation("Looking up music with {IdentifierType}: {IdentifierValue}",
            identifierType, identifierValue);

        // Step 1: Search by barcode to get the release ID
        var searchRelease = await _musicBrainzClient.GetReleaseByBarcodeAsync(identifierValue, cancellationToken);

        if (searchRelease == null)
        {
            _logger.LogWarning("No MusicBrainz release found for barcode: {Barcode}", identifierValue);
            return null;
        }

        // Step 2: Fetch full release details including tracks
        var release = await _musicBrainzClient.GetReleaseByIdAsync(searchRelease.Id, cancellationToken);

        if (release == null)
        {
            _logger.LogWarning("Failed to fetch full release details for ID: {ReleaseId}", searchRelease.Id);
            return null;
        }

        return MapToMusicResponse(release);
    }

    private MusicResponse MapToMusicResponse(MusicBrainzRelease release)
    {
        // Extract artist name
        var artist = release.ArtistCredit.Count > 0
            ? release.ArtistCredit[0].Name
            : string.Empty;

        // Extract genres from tags
        var genres = release.Tags
            .OrderByDescending(t => t.Count)
            .Take(5)
            .Select(t => CapitalizeGenre(t.Name))
            .ToList();

        // Extract label
        var label = release.LabelInfo.Count > 0 && release.LabelInfo[0].Label != null
            ? release.LabelInfo[0].Label!.Name
            : string.Empty;

        // Calculate total duration and track count
        int? totalDuration = null;
        var totalTracks = 0;
        var discList = new List<DiscResponse>();

        foreach (var media in release.Media)
        {
            totalTracks += media.TrackCount;

            foreach (var track in media.Tracks)
            {
                var trackDuration = track.Length ?? track.Recording?.Length;
                if (trackDuration.HasValue)
                {
                    totalDuration = (totalDuration ?? 0) + trackDuration.Value;
                }

                if (int.TryParse(track.Number, out var trackNumber))
                {
                    discList.Add(new DiscResponse(
                        TrackNumber: trackNumber,
                        Title: track.Title,
                        Duration: trackDuration
                    ));
                }
            }
        }

        // Duration is already in milliseconds, no conversion needed

        // Extract format
        var format = release.Media.Count > 0
            ? release.Media[0].Format
            : string.Empty;

        // Count number of discs
        var discs = release.Media.Count > 0 ? release.Media.Count : (int?)null;

        return new MusicResponse(
            Title: release.Title,
            Artist: artist,
            ReleaseDate: release.Date,
            Genres: genres,
            Duration: totalDuration,
            Label: label,
            Tracks: totalTracks > 0 ? totalTracks : null,
            Discs: discs,
            DiscList: discList,
            Format: format,
            ImageUrl: null
        );
    }

    private static string CapitalizeGenre(string genre)
    {
        if (string.IsNullOrWhiteSpace(genre))
        {
            return string.Empty;
        }

        // Split by space or hyphen and capitalize each word
        var words = genre.Split([' ', '-'], StringSplitOptions.RemoveEmptyEntries);
        var capitalizedWords = words.Select(word =>
            char.ToUpper(word[0]) + word[1..].ToLower()
        );

        return string.Join(" ", capitalizedWords);
    }
}
