namespace MediaSet.Api.Features.Statistics.Models;

public record Stats(BookStats BookStats, MovieStats MovieStats, GameStats GameStats, MusicStats MusicStats);

public record BookStats(
  int Total,
  int TotalFormats,
  IEnumerable<string> Formats,
  int UniqueAuthors,
  int TotalPages,
  IReadOnlyDictionary<string, int> FormatBreakdown
);

public record MovieStats(
  int Total,
  int TotalFormats,
  IEnumerable<string> Formats,
  int TotalTvSeries,
  IReadOnlyDictionary<string, int> FormatBreakdown
);

public record GameStats(
  int Total,
  int TotalFormats,
  IEnumerable<string> Formats,
  int TotalPlatforms,
  IEnumerable<string> Platforms,
  IReadOnlyDictionary<string, int> FormatBreakdown,
  IReadOnlyDictionary<string, int> PlatformBreakdown
);

public record MusicStats(
  int Total,
  int TotalFormats,
  IEnumerable<string> Formats,
  int UniqueArtists,
  int TotalTracks,
  IReadOnlyDictionary<string, int> FormatBreakdown
);
