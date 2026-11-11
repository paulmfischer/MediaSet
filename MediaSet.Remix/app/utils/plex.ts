import { PLEX_FILTERS, type PlexFilter, type PlexItem, type PlexRollup, type PlexScope } from '../models.ts';

/** How the Plex icon and card present a movie's link, decided from the stored data. */
export type PlexLinkState = 'onPlex' | 'offPlex' | 'broken';

export function plexLinkState(plex: Pick<PlexItem, 'onPlex' | 'broken'>): PlexLinkState {
  if (plex.broken) return 'broken';
  return plex.onPlex ? 'onPlex' : 'offPlex';
}

/** The labels of the movie list's Plex filter, in menu order. */
export const PLEX_FILTER_LABELS: Record<PlexFilter, string> = {
  onPlex: 'On Plex',
  notOnPlex: 'Not on Plex',
  unwatched: 'Unwatched on Plex',
  broken: 'Broken link',
};

/** The filter named by a query value, or null when it is missing or not one of the four. */
export function parsePlexFilter(value: string | null | undefined): PlexFilter | null {
  return (PLEX_FILTERS as readonly string[]).includes(value ?? '') ? (value as PlexFilter) : null;
}

const MINUTE = 60;
const HOUR = 60 * MINUTE;
const DAY = 24 * HOUR;

/** "just now", "5 minutes ago", "2 days ago"; falls back to the raw text for an unparseable date. */
export function relativeTime(value: string, now: Date = new Date()): string {
  const then = new Date(value);
  if (Number.isNaN(then.getTime())) return value;
  const seconds = Math.max(0, Math.round((now.getTime() - then.getTime()) / 1000));
  const plural = (count: number, unit: string) => `${count} ${unit}${count === 1 ? '' : 's'} ago`;
  if (seconds < MINUTE) return 'just now';
  if (seconds < HOUR) return plural(Math.floor(seconds / MINUTE), 'minute');
  if (seconds < DAY) return plural(Math.floor(seconds / HOUR), 'hour');
  if (seconds < 30 * DAY) return plural(Math.floor(seconds / DAY), 'day');
  const months = Math.floor(seconds / (30 * DAY));
  if (months < 12) return plural(months, 'month');
  return plural(Math.max(1, Math.floor(seconds / (365 * DAY))), 'year');
}

/** "1h 52m", "45m"; empty when there is no duration. */
export function formatDuration(totalSeconds: number | null | undefined): string {
  if (!totalSeconds || totalSeconds <= 0) return '';
  const minutes = Math.round(totalSeconds / 60);
  const hours = Math.floor(minutes / 60);
  return hours > 0 ? `${hours}h ${minutes % 60}m` : `${minutes}m`;
}

/** "4.2 GB", "850 MB"; empty when there is no size. */
export function formatSize(bytes: number | null | undefined): string {
  if (!bytes || bytes <= 0) return '';
  const units = ['B', 'KB', 'MB', 'GB', 'TB'];
  let size = bytes;
  let unit = 0;
  while (size >= 1024 && unit < units.length - 1) {
    size /= 1024;
    unit++;
  }
  return `${unit === 0 ? size : size.toFixed(size >= 100 ? 0 : 1)} ${units[unit]}`;
}

/** "Jan 5, 2025"; empty for a missing value, the raw text when unparseable. */
export function formatPlexDate(value: string | null | undefined): string {
  if (!value) return '';
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return value;
  return date.toLocaleDateString('en-US', { year: 'numeric', month: 'short', day: 'numeric', timeZone: 'UTC' });
}

/** "5.1 EAC3 English"-style text for the best audio track. */
export function describeAudioTrack(track: PlexItem['bestAudioTrack']): string {
  if (!track) return '';
  const channels =
    track.channels === 6 ? '5.1' : track.channels === 8 ? '7.1' : track.channels ? `${track.channels}.0` : '';
  return [channels, track.codec?.toUpperCase(), track.language].filter(Boolean).join(' ');
}

/** "Season 2", "Seasons 1-3", "Complete series"; empty when there is no scope. */
export function describeScope(scope: PlexScope | null | undefined): string {
  if (!scope) return '';
  if (scope.kind === 'all') return 'Complete series';
  const from = scope.from ?? scope.to;
  const to = scope.to ?? scope.from;
  if (from == null || to == null) return '';
  return from === to ? `Season ${from}` : `Seasons ${from}-${to}`;
}

/** "14/16 episodes | 12 watched". */
export function describeProgress(rollup: PlexRollup): string {
  return `${rollup.episodesPresent}/${rollup.episodesExpected} episodes | ${rollup.watchedCount} watched`;
}

/** The rollup's resolution: "Mixed" when episodes differ, plus " HDR" when any episode has it. */
export function describeRollupResolution(rollup: PlexRollup): string {
  const resolution = rollup.resolutionMixed ? 'Mixed' : (rollup.resolution ?? '');
  return rollup.hdr ? [resolution, 'HDR'].filter(Boolean).join(' ') : resolution;
}

/** A language list with "(differs per episode)" when the rollup flags it; empty without languages. */
export function describeLanguages(languages: string[], differ: boolean): string {
  if (languages.length === 0) return '';
  return `${languages.join(', ')}${differ ? ' (differs per episode)' : ''}`;
}

/** "Specials" for season 0, otherwise "Season N". */
export function seasonLabel(index: number): string {
  return index === 0 ? 'Specials' : `Season ${index}`;
}

/** "S02E03". */
export function episodeCode(season: number, episode: number): string {
  return `S${String(season).padStart(2, '0')}E${String(episode).padStart(2, '0')}`;
}

/** True for the Plex data of a TV entry: the API sends a scope only for those, never for a movie. */
export function isTvEntry(plex: PlexItem): boolean {
  return plex.scope != null;
}
