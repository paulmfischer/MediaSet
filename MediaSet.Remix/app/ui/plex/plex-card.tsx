import type { Handle } from 'remix/component';

import type { PlexItem } from '../../models.ts';
import {
  describeAudioTrack,
  describeLanguages,
  describeProgress,
  describeRollupResolution,
  describeScope,
  formatDuration,
  formatPlexDate,
  formatSize,
  isTvEntry,
  plexLinkState,
  relativeTime,
} from '../../utils/plex.ts';
import { Icon } from '../icon.tsx';
import { AlertTriangle, ExternalLink, Settings } from '../icons.ts';
import { PlexSeasonList } from './plex-tv-section.tsx';
import { PlexMenuBehavior } from './public/plex-menu-behavior.tsx';
import { PlexLinkDialog } from './public/plex-link-dialog.tsx';
import { PlexUnlinkDialog } from './public/plex-unlink-dialog.tsx';

interface PlexCardProps {
  /** The movie's stored Plex data; undefined for an unlinked movie. */
  plex: PlexItem | undefined;
  /** POST target of the Refresh button (the action redirects back to the detail page). */
  refreshHref: string;
  /** The link flow's page (Link to Plex and Relink open it, or a dialog showing the same flow). */
  linkHref: string;
  /** The unlink confirmation page (the Unlink button opens it, or a dialog). */
  unlinkHref: string;
  /** The movie's detail page, where a finished link lands. */
  detailHref: string;
  /** The movie's title, named in the unlink dialog. */
  title: string;
  /** The reference time for "Synced ...", so tests are stable; defaults to now. */
  now?: Date;
}

// The "Open in Plex" target: the API stores a Plex deep link; only web addresses are linked.
function safeHref(deepLink: string | null | undefined): string | undefined {
  if (!deepLink) return undefined;
  return /^https?:\/\//i.test(deepLink) ? deepLink : undefined;
}

// Never shown: the file path (the API does not send it) and any attribution.
function facts(plex: PlexItem): Array<[string, string]> {
  const resolution = plex.resolution ? `${plex.resolution}${plex.hdr ? ' HDR' : ''}` : plex.hdr ? 'HDR' : '';
  const subtitles = plex.subtitleLanguages.join(', ');
  const rows: Array<[string, string]> = [
    ['Resolution', resolution],
    ['Video codec', plex.videoCodec?.toUpperCase() ?? ''],
    ['Audio', describeAudioTrack(plex.bestAudioTrack)],
    ['Subtitles', subtitles],
    ['Watched', plex.watched ? 'Yes' : 'No'],
    ['Last watched', formatPlexDate(plex.lastWatchedAt)],
    ['Views', String(plex.viewCount)],
    ['Date added', formatPlexDate(plex.dateAdded)],
    ['Duration', formatDuration(plex.durationSeconds)],
    ['Container', plex.container ?? ''],
    ['Size', formatSize(plex.sizeBytes)],
  ];
  return rows.filter(([, value]) => value !== '');
}

// A TV entry shows progress, scope and the rollup instead of the single-file facts.
function tvFacts(plex: PlexItem): Array<[string, string]> {
  const rollup = plex.rollup;
  const rows: Array<[string, string]> = [
    ['Episodes', rollup ? describeProgress(rollup) : ''],
    ['Scope', describeScope(plex.scope)],
    ['Resolution', rollup ? describeRollupResolution(rollup) : ''],
    ['Audio', rollup ? describeLanguages(rollup.audioLanguages, rollup.audioLanguagesDiffer) : ''],
    ['Subtitles', rollup ? describeLanguages(rollup.subtitleLanguages, rollup.subtitleLanguagesDiffer) : ''],
    ['Last watched', formatPlexDate(rollup?.lastWatchedAt)],
    ['Date added', formatPlexDate(plex.dateAdded)],
  ];
  return rows.filter(([, value]) => value !== '');
}

interface PlexMenuProps {
  refreshHref: string;
  linkHref: string;
  unlinkHref: string;
  detailHref: string;
  title: string;
}

// The gear menu beside the heading: Refresh, Relink and Unlink. A native <details>, so it opens and closes without
// JavaScript; the three actions keep their own no-JavaScript targets and dialogs.
function PlexMenu(handle: Handle<PlexMenuProps>) {
  return () => {
    const { refreshHref, linkHref, unlinkHref, detailHref, title } = handle.props;
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
          <form method="post" action={refreshHref}>
            <button type="submit" class="secondary">
              Refresh
            </button>
          </form>
          <PlexLinkDialog href={linkHref} detailHref={detailHref} label="Relink" />
          <PlexUnlinkDialog title={title} href={unlinkHref} />
        </div>
        <PlexMenuBehavior />
      </details>
    );
  };
}

export function PlexCard(handle: Handle<PlexCardProps>) {
  return () => {
    const { plex, refreshHref, linkHref, unlinkHref, detailHref, title, now } = handle.props;

    // Unlinked: only the button, with no "Not on Plex" text.
    if (!plex) {
      return (
        <section class="mt-10" aria-label="Plex" data-testid="plex-card" data-plex-state="unlinked">
          <PlexLinkDialog href={linkHref} detailHref={detailHref} label="Link to Plex" auto />
        </section>
      );
    }

    const state = plexLinkState(plex);
    const href = safeHref(plex.deepLink);

    return (
      <section
        class={`mt-10 flex flex-col gap-3 rounded-lg bg-zinc-100 dark:bg-zinc-800 p-4 shadow-md ${state === 'onPlex' ? '' : 'opacity-70'}`}
        aria-label="Plex"
        data-testid="plex-card"
        data-plex-state={state}
      >
        <div class="flex items-center gap-2">
          <h2 class="text-xl font-semibold">Plex</h2>
          <PlexMenu
            refreshHref={refreshHref}
            linkHref={linkHref}
            unlinkHref={unlinkHref}
            detailHref={detailHref}
            title={title}
          />
        </div>
        {state === 'offPlex' ? (
          <p class="text-sm text-amber-400" role="status" data-testid="plex-notice">
            No longer on Plex. This is the last data MediaSet synced.
          </p>
        ) : null}
        {state === 'broken' ? (
          <div class="flex flex-col items-start gap-2 text-sm text-amber-400" data-testid="plex-notice">
            <p class="flex items-center gap-2">
              <Icon icon={AlertTriangle} size={18} />
              The Plex link for this movie is broken: the linked item is gone from Plex.
            </p>
          </div>
        ) : null}
        <div class="grid grid-cols-[max-content_1fr] gap-x-6 gap-y-1 text-sm">
          {(isTvEntry(plex) ? tvFacts(plex) : facts(plex)).map(([name, value]) => (
            <div key={name} class="contents">
              <span class="dark:text-slate-300">{name}</span>
              <div class="min-w-0 wrap-break-word" data-plex-field={name}>
                {value}
              </div>
            </div>
          ))}
        </div>
        {isTvEntry(plex) ? <PlexSeasonList plex={plex} /> : null}
        <p class="text-xs dark:text-slate-300" data-testid="plex-synced">
          Synced {relativeTime(plex.lastSyncedAt, now)}
        </p>
        <div class="flex flex-wrap items-center gap-3">
          {href ? (
            <a href={href} target="_blank" rel="noopener noreferrer" class="inline-flex items-center gap-1">
              Open in Plex
              <Icon icon={ExternalLink} size={14} />
              <span class="sr-only"> (opens in a new tab)</span>
            </a>
          ) : null}
        </div>
      </section>
    );
  };
}
