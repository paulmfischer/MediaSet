namespace MediaSet.Api.Models;

public record Stats(BookStats BookStats, MovieStats MovieStats);

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
