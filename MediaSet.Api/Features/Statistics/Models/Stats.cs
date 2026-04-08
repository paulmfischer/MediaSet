namespace MediaSet.Api.Features.Statistics.Models;

public record Stats(BookStats BookStats, MovieStats MovieStats, GameStats GameStats, MusicStats MusicStats);

public record NameCount(string Name, int Count);

public record BookStats(
  int Total,
  int TotalFormats,
  IEnumerable<string> Formats,
  int UniqueAuthors,
  int TotalPages,
  int AvgPages,
  IReadOnlyDictionary<string, int> FormatBreakdown,
  IReadOnlyDictionary<string, int> DecadeBreakdown,
  IReadOnlyDictionary<string, int> PageCountBuckets,
  IEnumerable<NameCount> TopAuthors,
  IEnumerable<NameCount> TopGenres
);

public record MovieStats(
  int Total,
  int TotalFormats,
  IEnumerable<string> Formats,
  int TotalTvSeries,
  IReadOnlyDictionary<string, int> FormatBreakdown,
  IReadOnlyDictionary<string, int> DecadeBreakdown,
  IReadOnlyDictionary<string, int> GenreBreakdown,
  IEnumerable<NameCount> TopStudios
);

public record GameStats(
  int Total,
  int TotalFormats,
  IEnumerable<string> Formats,
  int TotalPlatforms,
  IEnumerable<string> Platforms,
  IReadOnlyDictionary<string, int> FormatBreakdown,
  IReadOnlyDictionary<string, int> PlatformBreakdown,
  IReadOnlyDictionary<string, int> DecadeBreakdown,
  IReadOnlyDictionary<string, int> GenreBreakdown,
  IEnumerable<NameCount> TopPublishers,
  IEnumerable<NameCount> TopDevelopers
);

public record MusicStats(
  int Total,
  int TotalFormats,
  IEnumerable<string> Formats,
  int UniqueArtists,
  int TotalTracks,
  int AvgTracks,
  int UniqueLabels,
  int TotalDiscs,
  IReadOnlyDictionary<string, int> FormatBreakdown,
  IReadOnlyDictionary<string, int> DecadeBreakdown,
  IReadOnlyDictionary<string, int> GenreBreakdown,
  IEnumerable<NameCount> TopArtists,
  IEnumerable<NameCount> TopLabels
);
