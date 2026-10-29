import type { IconNode } from 'lucide';

import { routes } from '../routes.ts';
import { Clapperboard, Gamepad2, HardDrive, Home, LibraryBig, Music } from './icons.ts';

export type Accent = 'books' | 'movies' | 'games' | 'musics';

export interface NavItem {
  href: string;
  label: string;
  icon: IconNode;
  /** Per-media-type accent: the icon takes the entity colour. */
  accent?: Accent;
}

// /image-stats stays a plain path until its route is added (#702).
export const mainNav: NavItem[] = [
  { href: routes.home.href(), label: 'Home', icon: Home },
  { href: routes.books.href(), label: 'Books', icon: LibraryBig, accent: 'books' },
  { href: routes.movies.href(), label: 'Movies', icon: Clapperboard, accent: 'movies' },
  { href: routes.games.href(), label: 'Games', icon: Gamepad2, accent: 'games' },
  { href: routes.musics.href(), label: 'Music', icon: Music, accent: 'musics' },
];

export const toolsNav: NavItem[] = [{ href: '/image-stats', label: 'Image Statistics', icon: HardDrive }];

/** A link is active on its own path and below it; Home only matches the root. */
export function isActivePath(path: string, href: string): boolean {
  if (href === '/') return path === '/';
  return path === href || path.startsWith(`${href}/`);
}
