const UNITS = ['B', 'KB', 'MB', 'GB', 'TB', 'PB'];

/** A byte count in decimal units with one decimal, such as "4.5 GB"; "0 B" for zero or nothing. */
export function formatBytes(bytes: number): string {
  if (!Number.isFinite(bytes) || bytes <= 0) return '0 B';
  const index = Math.min(Math.floor(Math.log10(bytes) / 3), UNITS.length - 1);
  const value = bytes / 1000 ** index;
  return `${index === 0 ? value : value.toFixed(1)} ${UNITS[index]}`;
}

/** A duration in seconds as its two largest units: "2d 4h", "1h 52m", "45m"; "0m" for zero or nothing. */
export function formatDuration(seconds: number): string {
  if (!Number.isFinite(seconds) || seconds < 60) return '0m';
  const minutes = Math.floor(seconds / 60);
  const days = Math.floor(minutes / 1440);
  const hours = Math.floor((minutes % 1440) / 60);
  const mins = minutes % 60;
  if (days > 0) return `${days}d ${hours}h`;
  if (hours > 0) return `${hours}h ${mins}m`;
  return `${mins}m`;
}

/** "unknown" is the API's name for a missing value; resolutions and codecs read better in upper case ("4K", "HEVC"). */
export function statisticLabel(name: string, upper = false): string {
  if (name === 'unknown') return 'Unknown';
  return upper ? name.toUpperCase() : name;
}
