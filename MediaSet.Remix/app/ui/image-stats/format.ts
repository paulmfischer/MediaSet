import type { BrokenImageLink, ImageLookupFailure, OrphanedImageFile } from '../../api/image-stats-data.ts';

export const ENTITY_TYPE_ORDER = ['books', 'movies', 'games', 'musics'] as const;

export const ENTITY_LABELS: Record<string, string> = {
  books: 'Books',
  movies: 'Movies',
  games: 'Games',
  musics: 'Music',
};

export function formatBytes(bytes: number): string {
  if (bytes === 0) return '0 B';
  const units = ['B', 'KB', 'MB', 'GB', 'TB'];
  const i = Math.min(Math.floor(Math.log(bytes) / Math.log(1024)), units.length - 1);
  return `${(bytes / Math.pow(1024, i)).toFixed(1)} ${units[i]}`;
}

export function groupByEntityType<T extends { entityType: string }>(items: T[]): Record<string, T[]> {
  return items.reduce<Record<string, T[]>>((acc, item) => {
    (acc[item.entityType] ??= []).push(item);
    return acc;
  }, {});
}

// Orphaned files carry no entity type; it is the first segment of their path.
export function groupOrphanedByEntityType(files: OrphanedImageFile[]): Record<string, OrphanedImageFile[]> {
  return files.reduce<Record<string, OrphanedImageFile[]>>((acc, file) => {
    const type = file.relativePath.split('/')[0];
    if (type) (acc[type] ??= []).push(file);
    return acc;
  }, {});
}

export type { BrokenImageLink, ImageLookupFailure };
