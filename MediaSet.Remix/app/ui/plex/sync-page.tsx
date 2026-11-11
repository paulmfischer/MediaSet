import type { Handle } from 'remix/component';

import type { PlexEntityStatus, PlexSyncRun } from '../../models.ts';
import { entityTypeLabel, hasActiveRun, isActiveRun, runStateLabel } from '../../utils/plex-sync.ts';
import { LocalDateTime } from '../image-stats/public/local-date-time.tsx';
import { PlexSyncPoller } from './public/plex-sync-poller.tsx';

interface SyncPageProps {
  /** One status per enabled entity type. */
  statuses: PlexEntityStatus[];
  /** Where the Sync now forms post (the page's own route). */
  action: string;
  /** The same-origin status route the poller reads while a run is active. */
  statusHref: string;
  /** Shown above the list, for example when a second sync request found a run already active. */
  notice?: string;
}

const STATE_CLASS: Record<string, string> = {
  succeeded: 'text-green-400',
  failed: 'text-red-400',
  interrupted: 'text-amber-400',
  running: 'text-cyan-400',
  queued: 'text-cyan-400',
};

// The counts a finished run records; a count the run did not record (null) is left out.
function counts(run: PlexSyncRun): Array<[string, number]> {
  const rows: Array<[string, number | null | undefined]> = [
    ['Items scanned', run.itemsScanned],
    ['Matched', run.matchedCount],
    ['Unmatched', run.unmatchedCount],
    ['Errors', run.erroredCount],
    ['IDs backfilled', run.externalIdsBackfilled],
    ['Backfill skipped', run.backfillSkipped],
  ];
  return rows.filter((row): row is [string, number] => row[1] != null);
}

function LastRun(handle: Handle<{ run: PlexSyncRun | null | undefined }>) {
  return () => {
    const run = handle.props.run;
    if (!run) return <p data-testid="plex-last-run">No sync has run yet.</p>;
    const when = run.endedAt ?? run.startedAt;
    return (
      <div class="flex flex-col gap-1" data-testid="plex-last-run">
        <p>
          <span class={`font-medium ${STATE_CLASS[run.state] ?? ''}`} data-testid="plex-run-state">
            {runStateLabel(run.state)}
          </span>{' '}
          <LocalDateTime value={when} />
        </p>
        {run.message ? (
          <p class="text-sm text-red-400" data-testid="plex-run-message">
            {run.message}
          </p>
        ) : null}
        {counts(run).length > 0 ? (
          <dl class="grid grid-cols-[max-content_1fr] gap-x-6 gap-y-1 text-sm" data-testid="plex-run-counts">
            {counts(run).map(([name, value]) => (
              <div key={name} class="contents">
                <dt>{name}</dt>
                <dd>{value}</dd>
              </div>
            ))}
          </dl>
        ) : null}
      </div>
    );
  };
}

function EntityStatus(handle: Handle<{ status: PlexEntityStatus; action: string }>) {
  return () => {
    const { status, action } = handle.props;
    const active = isActiveRun(status.lastRun) ? status.lastRun : null;
    const label = entityTypeLabel(status.entityType);
    return (
      <section
        class="flex flex-col gap-3 rounded-lg border border-slate-600 p-4"
        aria-labelledby={`plex-${status.entityType}-heading`}
        data-testid="plex-entity-status"
        data-entity-type={status.entityType}
      >
        <h2 id={`plex-${status.entityType}-heading`} class="text-lg">
          {label}
        </h2>
        <dl class="grid grid-cols-[max-content_1fr] gap-x-6 gap-y-2 text-sm">
          <dt>Last run</dt>
          <dd>
            <LastRun run={status.lastRun} />
          </dd>
          <dt>Next run</dt>
          <dd data-testid="plex-next-run">
            {status.nextRun ? <LocalDateTime value={status.nextRun} /> : 'Not scheduled'}
          </dd>
        </dl>
        {active ? (
          <div class="flex flex-wrap items-center gap-3">
            <button
              type="button"
              disabled
              aria-disabled="true"
              aria-describedby={`plex-${status.entityType}-running`}
              data-testid="plex-sync-now"
            >
              Sync now
            </button>
            <span id={`plex-${status.entityType}-running`} data-testid="plex-running-since">
              {active.state === 'queued' ? 'Queued since ' : 'Running since '}
              <LocalDateTime value={active.startedAt} />
            </span>
          </div>
        ) : (
          <form method="post" action={action} class="flex">
            <input type="hidden" name="entityType" value={status.entityType} />
            <button type="submit" data-testid="plex-sync-now">
              Sync now
            </button>
          </form>
        )}
      </section>
    );
  };
}

// The Plex sync page: one block per enabled entity type. Reload is the baseline; the poller island refreshes the
// page while a run is active.
export function PlexSyncPage(handle: Handle<SyncPageProps>) {
  return () => {
    const { statuses, action, statusHref, notice } = handle.props;
    return (
      <div class="flex flex-col gap-4 px-2 max-w-3xl mx-auto">
        <h1 class="text-2xl">Plex sync</h1>
        {notice ? (
          <p class="text-sm text-amber-400" data-testid="plex-notice">
            {notice}
          </p>
        ) : null}
        {statuses.length === 0 ? <p>No Plex entity types are enabled.</p> : null}
        {statuses.map((status) => (
          <EntityStatus key={status.entityType} status={status} action={action} />
        ))}
        {hasActiveRun(statuses) ? <PlexSyncPoller statusHref={statusHref} /> : null}
      </div>
    );
  };
}
