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

/// <summary>The Plex statistics page data: movies and TV entries (which share the movies entity type) apart.</summary>
public record PlexStatisticsDto(PlexEntityStatisticsDto Movies, PlexEntityStatisticsDto Tv);
