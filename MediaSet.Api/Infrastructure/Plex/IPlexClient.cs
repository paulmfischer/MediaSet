namespace MediaSet.Api.Infrastructure.Plex;

/// <summary>Read-only access to a Plex server. There are deliberately no write methods.</summary>
public interface IPlexClient
{
    /// <summary>All library sections of the server.</summary>
    Task<IReadOnlyList<PlexLibrary>> GetLibrariesAsync(CancellationToken cancellationToken);

    /// <summary>
    /// The libraries to read for an entity type: its configured section ids when set, otherwise all
    /// video libraries of the matching kinds (movies read "movie" libraries and, for TV series entries,
    /// "show" libraries).
    /// </summary>
    Task<IReadOnlyList<PlexLibrary>> GetLibrariesForEntityTypeAsync(string entityType, CancellationToken cancellationToken);

    /// <summary>
    /// Reads every item of a library in pages, optionally filtered by a title contains match. With details
    /// the result has deep links (one identity request) and audio, subtitle and HDR data from one extra
    /// metadata request per item with files, because section listings carry no streams. Pass false
    /// when they are not needed (search).
    /// </summary>
    Task<IReadOnlyList<PlexItem>> GetItemsAsync(PlexLibrary library, string? titleContains, bool includeDetails, CancellationToken cancellationToken);

    /// <summary>
    /// Reads one item by rating key, with its streams and deep link when details are requested. Null when
    /// Plex has no such item.
    /// </summary>
    Task<PlexItem?> GetItemAsync(string ratingKey, bool includeDetails, CancellationToken cancellationToken);

    /// <summary>The seasons of a show by its rating key (one request), with the episode count Plex reports. Empty when Plex has none.</summary>
    Task<IReadOnlyList<PlexSeason>> GetSeasonsAsync(string showRatingKey, CancellationToken cancellationToken);

    /// <summary>
    /// The episodes of one season by the season's rating key, read in pages, with audio, subtitle and HDR data from one
    /// extra metadata request per episode with files (listings carry no streams). Empty when the season has none.
    /// Throws PlexClientException when Plex cannot be reached or does not know the season.
    /// </summary>
    Task<IReadOnlyList<PlexEpisode>> GetEpisodesAsync(string seasonRatingKey, CancellationToken cancellationToken);
}
