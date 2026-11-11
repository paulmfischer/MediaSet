import type { PlexItem } from '../models.ts';

/** How the Plex icon and card present a movie's link, decided from the stored data. */
export type PlexLinkState = 'onPlex' | 'offPlex' | 'broken';

export function plexLinkState(plex: PlexItem): PlexLinkState {
  if (plex.broken) return 'broken';
  return plex.onPlex ? 'onPlex' : 'offPlex';
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
  if (seconds < 365 * DAY) return plural(Math.floor(seconds / (30 * DAY)), 'month');
  return plural(Math.floor(seconds / (365 * DAY)), 'year');
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
