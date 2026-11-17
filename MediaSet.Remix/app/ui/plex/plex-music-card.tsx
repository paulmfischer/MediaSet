import type { Handle } from 'remix/component';

import type { PlexMusicItem, PlexMusicSummary } from '../../models.ts';
import {
  describeAudioQuality,
  describePlays,
  describeTracks,
  formatPlexDate,
  formatSize,
  isPartialAlbum,
  plexLinkState,
  relativeTime,
} from '../../utils/plex.ts';
import { Icon } from '../icon.tsx';
import { AlertTriangle, ExternalLink } from '../icons.ts';

interface PlexMusicNotesProps {
  /** The album's Plex summary from the list. */
  plex: PlexMusicSummary;
}

// What the list says beside the Plex icon: "Never played" and a "Partial" marker with the tracks present. They stay visible
// text so they do not rely on the icon's tooltip.
export function PlexMusicNotes(handle: Handle<PlexMusicNotesProps>) {
  return () => {
    const { plex } = handle.props;
    if (plexLinkState(plex) !== 'onPlex') return null;
    const never = plex.playCount <= 0;
    const partial = isPartialAlbum(plex);
    if (!never && !partial) return null;
    return (
      <span class="flex flex-col items-center text-xs leading-tight" data-testid="plex-music-notes">
        {never ? <span class="dark:text-slate-300">Never played</span> : null}
        {partial ? (
          <span class="text-amber-400" data-plex-partial>
            Partial {plex.tracksPresent}/{plex.tracksExpected}
          </span>
        ) : null}
      </span>
    );
  };
}

interface PlexMusicCardProps {
  /** The album's stored Plex data; undefined for an unlinked album, which shows no card. */
  plex: PlexMusicItem | undefined;
  /** The reference time for "Synced ...", so tests are stable; defaults to now. */
  now?: Date;
}

// The "Open in Plex" target: the API stores a Plex deep link; only web addresses are linked.
function safeHref(deepLink: string | null | undefined): string | undefined {
  if (!deepLink) return undefined;
  return /^https?:\/\//i.test(deepLink) ? deepLink : undefined;
}

function facts(plex: PlexMusicItem): Array<[string, string]> {
  const partial = isPartialAlbum(plex);
  const rows: Array<[string, string]> = [
    ['Plays', describePlays(plex)],
    ['Last played', plex.playCount > 0 ? formatPlexDate(plex.lastPlayedAt) : ''],
    ['Tracks', partial ? `${describeTracks(plex)} (partial)` : describeTracks(plex)],
    ['Quality', describeAudioQuality(plex)],
    ['Size', formatSize(plex.sizeBytes)],
    ['Date added', formatPlexDate(plex.dateAdded)],
  ];
  return rows.filter(([, value]) => value !== '');
}

// The Plex card of a linked album, read-only: plays or never played, last played, tracks with a partial marker, quality,
// size and a link to open it in Plex. An unlinked album shows nothing.
export function PlexMusicCard(handle: Handle<PlexMusicCardProps>) {
  return () => {
    const { plex, now } = handle.props;
    if (!plex) return null;

    const state = plexLinkState(plex);
    const href = safeHref(plex.deepLink);

    return (
      <section
        class={`mt-10 flex flex-col gap-3 rounded-lg bg-zinc-100 dark:bg-zinc-800 p-4 shadow-md ${state === 'onPlex' ? '' : 'opacity-70'}`}
        aria-label="Plex"
        data-testid="plex-card"
        data-plex-state={state}
      >
        <h2 class="text-xl font-semibold">Plex</h2>
        {state === 'offPlex' ? (
          <p class="text-sm text-amber-400" role="status" data-testid="plex-notice">
            No longer on Plex. This is the last data MediaSet synced.
          </p>
        ) : null}
        {state === 'broken' ? (
          <p class="flex items-center gap-2 text-sm text-amber-400" data-testid="plex-notice">
            <Icon icon={AlertTriangle} size={18} />
            The Plex link for this album is broken: the linked album is gone from Plex.
          </p>
        ) : null}
        <div class="grid grid-cols-[max-content_1fr] gap-x-6 gap-y-1 text-sm">
          {facts(plex).map(([name, value]) => (
            <div key={name} class="contents">
              <span class="dark:text-slate-300">{name}</span>
              <div class="min-w-0 wrap-break-word" data-plex-field={name}>
                {value}
              </div>
            </div>
          ))}
        </div>
        <p class="text-xs dark:text-slate-300" data-testid="plex-synced">
          Synced {relativeTime(plex.lastSyncedAt, now)}
        </p>
        {href ? (
          <div class="flex flex-wrap items-center gap-3">
            <a href={href} target="_blank" rel="noopener noreferrer" class="inline-flex items-center gap-1">
              Open in Plex
              <Icon icon={ExternalLink} size={14} />
              <span class="sr-only"> (opens in a new tab)</span>
            </a>
          </div>
        ) : null}
      </section>
    );
  };
}
