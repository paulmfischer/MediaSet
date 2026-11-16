namespace MediaSet.Api.Features.Plex.Models;

/// <summary>One labelled count of a breakdown, in display order. A list (not a dictionary) so the order survives JSON.</summary>
public record PlexCountDto(string Name, int Count);

/// <summary>How much of the collection is on Plex. <c>NotOnPlex</c> is Total minus OnPlex; <c>Broken</c> counts manual links whose item is gone.</summary>
public record PlexCoverageDto(int Total, int OnPlex, int NotOnPlex, int Broken);

/// <summary>Size of the on-Plex files. The average is over the units that report a size.</summary>
public record PlexStorageDto(long TotalBytes, long AverageBytes, int UnitsWithSize);

/// <summary>Runtime of the on-Plex units. The average is over the units that report a duration.</summary>
public record PlexRuntimeDto(long TotalSeconds, long AverageSeconds, int UnitsWithRuntime);

/// <summary>TV only: episodes on Plex against the episodes Plex reports for the covered seasons.</summary>
public record PlexCompletenessDto(int EpisodesPresent, int EpisodesExpected, int EntriesComplete, int EntriesIncomplete);

/// <summary>One most-watched entry. <c>ViewCount</c> is the play count for a movie and the number of watched episodes for a TV entry.</summary>
public record PlexMostWatchedDto(string Id, string Title, int ViewCount);

/// <summary>
/// Statistics of one kind of entry. A unit is an on-Plex movie, or an on-Plex episode for TV, so the breakdowns of a TV
/// section count episodes. No field carries a file path.
/// </summary>
public record PlexEntityStatisticsDto(
    string Kind,
    PlexCoverageDto Coverage,
    int Units,
    IReadOnlyList<PlexCountDto> Resolution,
    IReadOnlyList<PlexCountDto> VideoCodec,
    IReadOnlyList<PlexCountDto> Hdr,
    IReadOnlyList<PlexCountDto> Subtitles,
    IReadOnlyList<PlexCountDto> Watched,
    IReadOnlyList<PlexCountDto> AudioCodec,
    IReadOnlyList<PlexCountDto> AudioChannels,
    PlexStorageDto Storage,
    PlexRuntimeDto Runtime,
    IReadOnlyList<PlexMostWatchedDto> MostWatched,
    PlexCompletenessDto? Completeness);

/// <summary>
/// One album in a music ranking. <c>PlayCount</c> is the sum of its tracks' plays; the dates are null when unknown (never
/// played, or no date from Plex).
/// </summary>
public record PlexMusicAlbumDto(string Id, string Title, string Artist, int PlayCount, DateTime? LastPlayedAt, DateTime? DateAdded);

/// <summary>Music only: tracks on Plex against the tracks the entries expect. A partial album has fewer tracks present than expected.</summary>
public record PlexMusicCompletenessDto(int TracksPresent, int TracksExpected, int AlbumsComplete, int AlbumsPartial);

/// <summary>
/// Statistics of the music collection. A unit is an on-Plex album: the size, codec and bitrate breakdowns count albums.
/// No field carries a file path.
/// </summary>
public record PlexMusicStatisticsDto(
    PlexCoverageDto Coverage,
    int Albums,
    int TotalPlays,
    int NeverPlayed,
    IReadOnlyList<PlexMusicAlbumDto> MostPlayed,
    IReadOnlyList<PlexMusicAlbumDto> RecentlyPlayed,
    IReadOnlyList<PlexMusicAlbumDto> RecentlyAdded,
    PlexStorageDto Storage,
    IReadOnlyList<PlexCountDto> AudioCodec,
    IReadOnlyList<PlexCountDto> Bitrate,
    PlexMusicCompletenessDto Completeness);

/// <summary>
/// The Plex statistics page data. Each section is present only when its entity type is enabled: movies and TV entries
/// (which share the movies entity type) together, music on its own.
/// </summary>
public record PlexStatisticsDto(PlexEntityStatisticsDto? Movies, PlexEntityStatisticsDto? Tv, PlexMusicStatisticsDto? Music = null);
