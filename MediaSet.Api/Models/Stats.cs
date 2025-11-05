namespace MediaSet.Api.Models;

public record Stats(BookStats BookStats, MovieStats MovieStats, GameStats GameStats, MusicStats MusicStats);

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

public record MusicStats(
  int Total,
  int TotalFormats,
  IEnumerable<string> Formats,
  int UniqueArtists,
  int TotalTracks
);
