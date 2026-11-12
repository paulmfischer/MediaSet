import type { IconNode } from 'lucide';

import { Clapperboard, Gamepad2, LibraryBig, Music } from '../icons.ts';

export type DashboardTabId = 'books' | 'movies' | 'games' | 'musics';

export const CHART_HEX: Record<DashboardTabId, string> = {
  books: '#22c55e',
  movies: '#ef4444',
  games: '#a855f7',
  musics: '#ec4899',
};

export const FORMAT_PALETTES: Record<DashboardTabId, string[]> = {
  books: ['#16a34a', '#22c55e', '#4ade80', '#86efac', '#bbf7d0'],
  movies: ['#dc2626', '#ef4444', '#f87171', '#fca5a5', '#fecaca'],
  games: ['#9333ea', '#a855f7', '#c084fc', '#d8b4fe', '#e9d5ff'],
  musics: ['#db2777', '#ec4899', '#f472b6', '#f9a8d4', '#fbcfe8'],
};

// Plex amber shades for the resolution pie.
export const PLEX_RESOLUTION_COLORS = ['#e5a00d', '#f5c04a', '#b87f0a', '#fbd98a', '#8a5f08'];

// Distinct hues for the Plex statistics pies, whose slices are unrelated categories (Plex amber first).
export const PLEX_STATISTICS_COLORS = ['#e5a00d', '#38bdf8', '#a78bfa', '#34d399', '#f87171', '#94a3b8'];

export const MOVIE_TYPE_COLORS = ['#ef4444', '#f97316'];

// Shared by every stat card: the accent colour comes from the surrounding entity-* class.
export const STAT_CARD_COLOR = 'bg-entity/10 text-entity border-entity/20';

export interface DashboardTabConfig {
  id: DashboardTabId;
  /** Sets --entity-color for the tab and its panel. */
  entityClass: string;
  label: string;
  icon: IconNode;
  /** Id of the hidden radio that selects the tab. */
  inputId: string;
  /**
   * Tailwind only generates classes it finds as complete strings, so the state variants for each
   * tab are written out in full. They key off the tab's checked radio through the wrapper's `tabs` group.
   */
  activeLabelClass: string;
  focusLabelClass: string;
  activePanelClass: string;
}

// Tab order is the display order. Each tab shows when its media type has items.
export const DASHBOARD_TABS: DashboardTabConfig[] = [
  {
    id: 'books',
    entityClass: 'entity-books',
    label: 'Books',
    icon: LibraryBig,
    inputId: 'dashboard-tab-books',
    activeLabelClass:
      'group-has-[#dashboard-tab-books:checked]/tabs:relative group-has-[#dashboard-tab-books:checked]/tabs:z-10 group-has-[#dashboard-tab-books:checked]/tabs:-mb-[5px] group-has-[#dashboard-tab-books:checked]/tabs:rounded-b-none group-has-[#dashboard-tab-books:checked]/tabs:border-x-zinc-700 group-has-[#dashboard-tab-books:checked]/tabs:border-b-zinc-900 group-has-[#dashboard-tab-books:checked]/tabs:border-t-entity group-has-[#dashboard-tab-books:checked]/tabs:bg-zinc-800',
    focusLabelClass:
      'group-has-[#dashboard-tab-books:focus-visible]/tabs:ring-2 group-has-[#dashboard-tab-books:focus-visible]/tabs:ring-blue-400',
    activePanelClass: 'group-has-[#dashboard-tab-books:checked]/tabs:block',
  },
  {
    id: 'movies',
    entityClass: 'entity-movies',
    label: 'Movies & TV',
    icon: Clapperboard,
    inputId: 'dashboard-tab-movies',
    activeLabelClass:
      'group-has-[#dashboard-tab-movies:checked]/tabs:relative group-has-[#dashboard-tab-movies:checked]/tabs:z-10 group-has-[#dashboard-tab-movies:checked]/tabs:-mb-[5px] group-has-[#dashboard-tab-movies:checked]/tabs:rounded-b-none group-has-[#dashboard-tab-movies:checked]/tabs:border-x-zinc-700 group-has-[#dashboard-tab-movies:checked]/tabs:border-b-zinc-900 group-has-[#dashboard-tab-movies:checked]/tabs:border-t-entity group-has-[#dashboard-tab-movies:checked]/tabs:bg-zinc-800',
    focusLabelClass:
      'group-has-[#dashboard-tab-movies:focus-visible]/tabs:ring-2 group-has-[#dashboard-tab-movies:focus-visible]/tabs:ring-blue-400',
    activePanelClass: 'group-has-[#dashboard-tab-movies:checked]/tabs:block',
  },
  {
    id: 'games',
    entityClass: 'entity-games',
    label: 'Games',
    icon: Gamepad2,
    inputId: 'dashboard-tab-games',
    activeLabelClass:
      'group-has-[#dashboard-tab-games:checked]/tabs:relative group-has-[#dashboard-tab-games:checked]/tabs:z-10 group-has-[#dashboard-tab-games:checked]/tabs:-mb-[5px] group-has-[#dashboard-tab-games:checked]/tabs:rounded-b-none group-has-[#dashboard-tab-games:checked]/tabs:border-x-zinc-700 group-has-[#dashboard-tab-games:checked]/tabs:border-b-zinc-900 group-has-[#dashboard-tab-games:checked]/tabs:border-t-entity group-has-[#dashboard-tab-games:checked]/tabs:bg-zinc-800',
    focusLabelClass:
      'group-has-[#dashboard-tab-games:focus-visible]/tabs:ring-2 group-has-[#dashboard-tab-games:focus-visible]/tabs:ring-blue-400',
    activePanelClass: 'group-has-[#dashboard-tab-games:checked]/tabs:block',
  },
  {
    id: 'musics',
    entityClass: 'entity-musics',
    label: 'Music',
    icon: Music,
    inputId: 'dashboard-tab-musics',
    activeLabelClass:
      'group-has-[#dashboard-tab-musics:checked]/tabs:relative group-has-[#dashboard-tab-musics:checked]/tabs:z-10 group-has-[#dashboard-tab-musics:checked]/tabs:-mb-[5px] group-has-[#dashboard-tab-musics:checked]/tabs:rounded-b-none group-has-[#dashboard-tab-musics:checked]/tabs:border-x-zinc-700 group-has-[#dashboard-tab-musics:checked]/tabs:border-b-zinc-900 group-has-[#dashboard-tab-musics:checked]/tabs:border-t-entity group-has-[#dashboard-tab-musics:checked]/tabs:bg-zinc-800',
    focusLabelClass:
      'group-has-[#dashboard-tab-musics:focus-visible]/tabs:ring-2 group-has-[#dashboard-tab-musics:focus-visible]/tabs:ring-blue-400',
    activePanelClass: 'group-has-[#dashboard-tab-musics:checked]/tabs:block',
  },
];

export const EMPTY_STATE_ICON = LibraryBig;
export const DEFAULT_TAB_ID: DashboardTabId = 'books';
