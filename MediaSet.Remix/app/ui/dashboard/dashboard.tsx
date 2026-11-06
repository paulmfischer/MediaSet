import type { Handle } from 'remix/component';

import { Icon } from '../icon.tsx';
import { EMPTY_STATE_ICON } from './config.ts';
import { DashboardTabs } from './dashboard-tabs.tsx';
import { totalItems, type DashboardStats } from './stats.ts';

interface DashboardProps {
  stats: DashboardStats;
}

// The collection overview: a heading with the item count and the per-type tabs, or an empty state.
export function Dashboard(handle: Handle<DashboardProps>) {
  return () => {
    const { stats } = handle.props;
    const total = totalItems(stats);

    if (total === 0) {
      return (
        <div class="rounded-lg border border-zinc-700 bg-zinc-800/30 p-12 text-center">
          <div class="mx-auto flex h-16 w-16 items-center justify-center rounded-full bg-zinc-700/50">
            <Icon icon={EMPTY_STATE_ICON} size={32} class="text-zinc-400" />
          </div>
          <h2 class="mt-4 text-xl font-semibold text-white">No media items yet</h2>
          <p class="mt-2 text-zinc-400">Start building your collection by adding books, movies, games, or music.</p>
        </div>
      );
    }

    return (
      <div>
        <div class="mb-6">
          <h2 class="text-xl font-semibold text-white">
            Collection Overview
            <span class="ml-3 rounded-lg bg-blue-500/10 px-3 py-1 text-base font-normal text-blue-400">
              {total.toLocaleString('en-US')} items
            </span>
          </h2>
        </div>
        <DashboardTabs stats={stats} />
      </div>
    );
  };
}
