import type { Handle } from 'remix/component';

import type { PlexItem, PlexListSummary } from '../../models.ts';
import { plexLinkState } from '../../utils/plex.ts';
import { Icon } from '../icon.tsx';
import { Check } from '../icons.ts';

/** Which of the two supplied Plex marks to show; change this one constant to switch every icon to the dark mark. */
export const PLEX_ICON_VARIANT: 'light' | 'dark' = 'light';

export const PLEX_ICON_SRC = `/integrations/plex-${PLEX_ICON_VARIANT}.svg`;

interface PlexIconProps {
  /** The movie's stored Plex data or list summary; an unlinked movie (undefined) shows nothing. */
  plex: Pick<PlexItem, 'onPlex' | 'broken' | 'watched'> | PlexListSummary | undefined;
  /** Width and height in pixels. */
  size?: number;
}

/** The tooltip and accessible label for a movie's Plex state. */
export function plexIconLabel(plex: Pick<PlexItem, 'onPlex' | 'broken' | 'watched'>): string {
  switch (plexLinkState(plex)) {
    case 'broken':
      return 'Plex link is broken';
    case 'offPlex':
      return 'No longer on Plex';
    default:
      return plex.watched ? 'On Plex, watched' : 'On Plex';
  }
}

// The Plex mark with its state: muted when off Plex, a warning dot when a manual link is broken and a check
// badge when watched. It is drawn only from the data, so an unlinked movie (and the gate being off, where the
// caller passes no data) renders nothing.
export function PlexIcon(handle: Handle<PlexIconProps>) {
  return () => {
    const { plex, size = 28 } = handle.props;
    if (!plex) return null;

    const state = plexLinkState(plex);
    const label = plexIconLabel(plex);
    const muted = state === 'offPlex' || state === 'broken';
    const badge = Math.max(10, Math.round(size * 0.45));
    const dot = Math.round(badge * 0.7);

    return (
      <span
        class="relative inline-flex shrink-0"
        role="img"
        aria-label={label}
        title={label}
        data-plex-icon={state}
        style={`width:${size}px;height:${size}px`}
      >
        <img
          src={PLEX_ICON_SRC}
          alt=""
          width={size}
          height={size}
          class={`rounded-md ${muted ? 'opacity-40 grayscale' : ''}`}
        />
        {state === 'broken' ? (
          <span
            data-plex-badge="warning"
            class="absolute -right-1 -top-1 rounded-full bg-amber-500 ring-2 ring-white dark:ring-zinc-800"
            style={`width:${dot}px;height:${dot}px`}
          />
        ) : null}
        {state === 'onPlex' && plex.watched ? (
          <span
            data-plex-badge="watched"
            class="absolute -bottom-1 -right-1 inline-flex items-center justify-center rounded-full bg-green-600 text-white ring-2 ring-white dark:ring-zinc-800"
            style={`width:${badge}px;height:${badge}px`}
          >
            <Icon icon={Check} size={badge - 4} />
          </span>
        ) : null}
      </span>
    );
  };
}
