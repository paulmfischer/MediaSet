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
    /// metadata request per item with files, because section listings carry no streams; at most
    /// PlexConfiguration.DetailConcurrency of those run at once. Pass false when they are not needed (search).
    /// An item in <paramref name="known"/> (by rating key) whose listed updatedAt and file size still match skips the
    /// detail request and comes back with StreamsReused true and no stream data; the caller keeps what it stored.
    /// Items without files (shows) ignore it, as they never need a detail request.
    /// </summary>
    Task<IReadOnlyList<PlexItem>> GetItemsAsync(
        PlexLibrary library,
        string? titleContains,
        bool includeDetails,
        CancellationToken cancellationToken,
        IReadOnlyDictionary<string, PlexKnownMedia>? known = null);

    /// <summary>
    /// Reads every item of a library in pages for matching: deep links (one identity request) and guids, but no
    /// per-item metadata request, so items with files come back with the listed versions and no audio, subtitle or HDR
    /// data. The caller reads the detail of the items it links to with <see cref="GetItemAsync"/>. An item in
    /// <paramref name="known"/> (by rating key) whose listed updatedAt and file size still match comes back with
    /// StreamsReused true, as in <see cref="GetItemsAsync"/>. Throws PlexClientException when Plex cannot be read.
    /// </summary>
    Task<IReadOnlyList<PlexItem>> GetItemListingAsync(
        PlexLibrary library,
        CancellationToken cancellationToken,
        IReadOnlyDictionary<string, PlexKnownMedia>? known = null);

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
    /// An episode in <paramref name="known"/> (by rating key) whose listed updatedAt and file size still match skips the
    /// stream lookup and comes back with StreamsReused true and no stream data; the caller keeps what it stored.
    /// </summary>
    Task<IReadOnlyList<PlexEpisode>> GetEpisodesAsync(
        string seasonRatingKey,
        CancellationToken cancellationToken,
        IReadOnlyDictionary<string, PlexKnownMedia>? known = null);

    /// <summary>
    /// The poster of an item by rating key, at the size Plex stores it (not a transcoded thumbnail), as the raw image
    /// bytes Plex sends. Null when the item has no poster, or the poster cannot be read, so a missing poster never
    /// fails a caller. The size is capped by <paramref name="maxBytes"/>; a larger poster also reads as null.
    /// </summary>
    Task<byte[]?> GetPosterAsync(string ratingKey, long maxBytes, CancellationToken cancellationToken);
}
