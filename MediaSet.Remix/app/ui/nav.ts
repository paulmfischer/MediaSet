import type { IconNode } from 'lucide';

import { routes } from '../routes.ts';
import { Clapperboard, Gamepad2, HardDrive, Home, LibraryBig, Music, RefreshCw } from './icons.ts';

export type Accent = 'books' | 'movies' | 'games' | 'musics';

export interface NavItem {
  href: string;
  label: string;
  icon: IconNode;
  /** Per-media-type accent: the icon takes the entity colour. */
  accent?: Accent;
}

export const mainNav: NavItem[] = [
  { href: routes.home.href(), label: 'Home', icon: Home },
  { href: routes.entityLists.books.href(), label: 'Books', icon: LibraryBig, accent: 'books' },
  { href: routes.entityLists.movies.href(), label: 'Movies', icon: Clapperboard, accent: 'movies' },
  { href: routes.entityLists.games.href(), label: 'Games', icon: Gamepad2, accent: 'games' },
  { href: routes.entityLists.musics.href(), label: 'Music', icon: Music, accent: 'musics' },
];

export const toolsNav: NavItem[] = [
  { href: routes.imageStats.index.href(), label: 'Image Statistics', icon: HardDrive },
];

/** The Plex sync page's link; the gear menu shows it only when Plex is enabled for movies. */
export const plexNavItem: NavItem = { href: routes.plex.index.href(), label: 'Plex', icon: RefreshCw };

/** A link is active on its own path and below it; Home only matches the root. */
export function isActivePath(path: string, href: string): boolean {
  if (href === '/') return path === '/';
  return path === href || path.startsWith(`${href}/`);
}
