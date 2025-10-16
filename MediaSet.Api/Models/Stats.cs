namespace MediaSet.Api.Models;

public record Stats(BookStats BookStats, MovieStats MovieStats, GameStats GameStats);

public record BookStats(
  int Total,
  int TotalFormats,
  IEnumerable<string> Formats,
  int UniqueAuthors,
  int TotalPages
);

public record MovieStats(
  int Total,
  int TotalFormats,
  IEnumerable<string> Formats,
  int TotalTvSeries
);

public record GameStats(
  int Total,
  int TotalFormats,
  IEnumerable<string> Formats,
  int TotalPlatforms,
  IEnumerable<string> Platforms
);
