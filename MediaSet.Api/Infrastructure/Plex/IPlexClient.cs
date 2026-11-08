namespace MediaSet.Api.Infrastructure.Plex;

/// <summary>Read-only access to a Plex server. There are deliberately no write methods.</summary>
public interface IPlexClient
{
    /// <summary>All library sections of the server.</summary>
    Task<IReadOnlyList<PlexLibrary>> GetLibrariesAsync(CancellationToken cancellationToken);

    /// <summary>
    /// The libraries to read for an entity type: its configured section ids when set, otherwise all
    /// video libraries of the matching kind (movies read only "movie" libraries).
    /// </summary>
    Task<IReadOnlyList<PlexLibrary>> GetLibrariesForEntityTypeAsync(string entityType, CancellationToken cancellationToken);

    /// <summary>
    /// Reads every item of a library in pages, optionally filtered by a title contains match. With details
    /// the result has deep links (one identity request) and audio, subtitle and HDR data from one extra
    /// metadata request per item with files, because section listings carry no streams. Pass false
    /// when they are not needed (search).
    /// </summary>
    Task<IReadOnlyList<PlexItem>> GetItemsAsync(PlexLibrary library, string? titleContains, bool includeDetails, CancellationToken cancellationToken);
}
