import type { Handle } from 'remix/component';

import { Icon } from '../icon.tsx';
import { DASHBOARD_TABS, DEFAULT_TAB_ID, type DashboardTabId } from './config.ts';
import { BooksPanel, GamesPanel, MoviesPanel, MusicsPanel } from './panels.tsx';
import { totalItems, type DashboardStats } from './stats.ts';

interface DashboardTabsProps {
  stats: DashboardStats;
}

const PANELS = {
  books: BooksPanel,
  movies: MoviesPanel,
  games: GamesPanel,
  musics: MusicsPanel,
};

function typeTotal(stats: DashboardStats, id: DashboardTabId): number {
  return {
    books: stats.bookStats.total,
    movies: stats.movieStats.total,
    games: stats.gameStats.total,
    musics: stats.musicStats.total,
  }[id];
}

// The full ARIA tab pattern (role=tablist/tab, roving tabindex, arrow-key handling) needs script, which this
// component deliberately avoids. Native radios already give arrow-key selection and a checked state, so the
// group is a fieldset named by its legend, each radio points at its panel with aria-controls, and each panel
// is a region named by its tab. Do not add role=tab to the labels: it would promise behaviour that is not there.
// Tabs for the media types that have items. Selection is CSS only: a hidden radio per tab picks the
// visible panel, so every panel is in the HTML and nothing needs script. Books is selected first,
// or the first type that has items.
export function DashboardTabs(handle: Handle<DashboardTabsProps>) {
  return () => {
    const { stats } = handle.props;
    const all = totalItems(stats);
    const tabs = DASHBOARD_TABS.filter((tab) => typeTotal(stats, tab.id) > 0);
    if (tabs.length === 0) return null;
    const selected = tabs.find((tab) => tab.id === DEFAULT_TAB_ID) ?? tabs[0];

    return (
      <fieldset class="group/tabs min-w-0">
        <legend class="sr-only">Media type</legend>
        {tabs.map((tab) => (
          <input
            type="radio"
            name="dashboard-tab"
            id={tab.inputId}
            aria-controls={`dashboard-panel-${tab.id}`}
            class="sr-only"
            checked={tab.id === selected.id}
          />
        ))}
        <div class="mb-1 grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
          {tabs.map((tab) => {
            const total = typeTotal(stats, tab.id);
            return (
              <label
                for={tab.inputId}
                id={`dashboard-label-${tab.id}`}
                class={`${tab.entityClass} ${tab.activeLabelClass} ${tab.focusLabelClass} block w-full cursor-pointer rounded-sm border border-gray-500 bg-transparent p-6 text-left text-slate-300 transition-colors hover:bg-gray-800`}
              >
                <div class="flex items-center gap-2">
                  <Icon icon={tab.icon} size={16} class="text-entity" />
                  <span class="font-semibold text-white">{tab.label}</span>
                </div>
                <div class="mt-2 text-2xl font-bold text-entity">{total.toLocaleString('en-US')}</div>
                <div class="text-xs text-zinc-400">{((total / all) * 100).toFixed(1)}% of collection</div>
              </label>
            );
          })}
        </div>
        {tabs.map((tab) => {
          const Panel = PANELS[tab.id];
          return (
            <div
              id={`dashboard-panel-${tab.id}`}
              role="region"
              aria-labelledby={`dashboard-label-${tab.id}`}
              class={`${tab.entityClass} ${tab.activePanelClass} hidden rounded-lg rounded-tl-none rounded-tr-none border border-zinc-700`}
            >
              <Panel stats={stats} />
            </div>
          );
        })}
      </fieldset>
    );
  };
}
