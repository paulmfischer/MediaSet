import type { getStats } from '../../api/stats-data.ts';

export type DashboardStats = Awaited<ReturnType<typeof getStats>>;

export function totalItems(stats: DashboardStats): number {
  return stats.bookStats.total + stats.movieStats.total + stats.gameStats.total + stats.musicStats.total;
}
