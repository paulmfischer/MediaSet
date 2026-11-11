namespace MediaSet.Api.Shared.Models;

/// <summary>
/// Plex statistics of the movie collection (movies and TV entries). Present on <c>Stats</c> only when Plex is
/// enabled for movies. It carries counts only, never a file path.
/// </summary>
/// <param name="OnPlex">Movies whose linked item is on Plex.</param>
/// <param name="NotOnPlex">Movies that are not on Plex (unlinked, or linked but gone); the same set as the list filter <c>plex=notOnPlex</c>.</param>
/// <param name="Watched">On-Plex movies that are watched.</param>
/// <param name="Unwatched">On-Plex movies that are not watched.</param>
/// <param name="ResolutionBreakdown">On-Plex movies by resolution; those with no resolution are counted as "unknown".</param>
/// <param name="NeedsLinking">Movies with no Plex link, plus those whose manual link is broken.</param>
public record PlexStats(
  int OnPlex,
  int NotOnPlex,
  int Watched,
  int Unwatched,
  IReadOnlyDictionary<string, int> ResolutionBreakdown,
  int NeedsLinking
);
