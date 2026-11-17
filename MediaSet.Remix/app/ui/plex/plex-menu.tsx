import type { Handle } from 'remix/component';

import { Icon } from '../icon.tsx';
import { Settings } from '../icons.ts';
import { PlexLinkDialog } from './public/plex-link-dialog.tsx';
import { PlexMenuBehavior } from './public/plex-menu-behavior.tsx';
import { PlexUnlinkDialog } from './public/plex-unlink-dialog.tsx';

interface PlexMenuProps {
  /** POST target of Refresh; omitted for an entity type with no per-item refresh (music), which then has no button. */
  refreshHref?: string;
  linkHref: string;
  unlinkHref: string;
  detailHref: string;
  title: string;
  /** What the entry is, named in the unlink dialog: "movie" (the default) or "album". */
  noun?: 'movie' | 'album';
}

// The gear menu beside the heading: Refresh, Relink and Unlink. A native <details>, so it opens and closes without
// JavaScript; the actions keep their own no-JavaScript targets and dialogs.
export function PlexMenu(handle: Handle<PlexMenuProps>) {
  return () => {
    const { refreshHref, linkHref, unlinkHref, detailHref, title, noun } = handle.props;
    return (
      <details class="relative" data-testid="plex-menu">
        <summary
          class="flex cursor-pointer list-none items-center rounded-md p-1 hover:bg-zinc-300 dark:hover:bg-zinc-700 [&::-webkit-details-marker]:hidden"
          aria-label="Plex actions"
          title="Plex actions"
        >
          <Icon icon={Settings} size={18} />
        </summary>
        <div class="absolute left-0 top-full z-20 mt-1 flex w-36 flex-col [&>form>button]:w-full gap-2 rounded-lg border border-zinc-300 dark:border-zinc-600 bg-white dark:bg-zinc-800 p-2 shadow-lg">
          {refreshHref ? (
            <form method="post" action={refreshHref}>
              <button type="submit" class="secondary">
                Refresh
              </button>
            </form>
          ) : null}
          <PlexLinkDialog href={linkHref} detailHref={detailHref} label="Relink" />
          <PlexUnlinkDialog title={title} href={unlinkHref} noun={noun} />
        </div>
        <PlexMenuBehavior />
      </details>
    );
  };
}
