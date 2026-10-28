import type { IconNode } from 'lucide';

import { Clapperboard, Gamepad2, HardDrive, Home, LibraryBig, Music } from './icons.ts';

export type Accent = 'books' | 'movies' | 'games' | 'musics';

export interface NavItem {
  href: string;
  label: string;
  icon: IconNode;
  /** Per-media-type accent: the icon takes the entity colour. */
  accent?: Accent;
}

// Targets for pages that other migration tickets add; they are plain paths until those routes exist.
export const mainNav: NavItem[] = [
  { href: '/', label: 'Home', icon: Home },
  { href: '/books', label: 'Books', icon: LibraryBig, accent: 'books' },
  { href: '/movies', label: 'Movies', icon: Clapperboard, accent: 'movies' },
  { href: '/games', label: 'Games', icon: Gamepad2, accent: 'games' },
  { href: '/musics', label: 'Music', icon: Music, accent: 'musics' },
];

export const toolsNav: NavItem[] = [{ href: '/image-stats', label: 'Image Statistics', icon: HardDrive }];

/** A link is active on its own path and below it; Home only matches the root. */
export function isActivePath(path: string, href: string): boolean {
  if (href === '/') return path === '/';
  return path === href || path.startsWith(`${href}/`);
}
