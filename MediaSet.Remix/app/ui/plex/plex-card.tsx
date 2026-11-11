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
import { AlertTriangle } from '../icons.ts';
import { PlexSeasonList } from './plex-tv-section.tsx';
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

export function PlexCard(handle: Handle<PlexCardProps>) {
  return () => {
    const { plex, refreshHref, linkHref, unlinkHref, title, now } = handle.props;

    // Unlinked: only the button, with no "Not on Plex" text.
    if (!plex) {
      return (
        <section class="mt-6" aria-label="Plex" data-testid="plex-card" data-plex-state="unlinked">
          <PlexLinkDialog href={linkHref} label="Link to Plex" />
        </section>
      );
    }

    const state = plexLinkState(plex);
    const href = safeHref(plex.deepLink);

    return (
      <section
        class={`mt-6 flex flex-col gap-3 rounded-lg border border-slate-600 p-4 ${state === 'onPlex' ? '' : 'opacity-70'}`}
        aria-label="Plex"
        data-testid="plex-card"
        data-plex-state={state}
      >
        <h2 class="text-lg">Plex</h2>
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
            <div class="flex gap-2">
              <PlexLinkDialog href={linkHref} label="Relink" />
              <PlexUnlinkDialog title={title} href={unlinkHref} />
            </div>
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
            <a href={href} target="_blank" rel="noopener noreferrer">
              Open in Plex
            </a>
          ) : null}
          <form method="post" action={refreshHref}>
            <button type="submit">Refresh</button>
          </form>
          {state === 'broken' ? null : <PlexLinkDialog href={linkHref} label="Relink" />}
          {state === 'broken' ? null : <PlexUnlinkDialog title={title} href={unlinkHref} />}
        </div>
      </section>
    );
  };
}
