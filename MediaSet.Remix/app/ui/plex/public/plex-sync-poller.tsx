import { clientEntry, type Handle } from 'remix/component';

import { classifyPoll, nextDelay, POLL_MS } from './plex-sync-poll.ts';

interface PlexSyncPollerProps {
  /** The same-origin route that answers with every enabled entity type's status as JSON. */
  statusHref: string;
}

const REQUEST_TIMEOUT_MS = 10_000;

// Rendered only while a run is active. It polls the status route, and reloads the page once no run is active (or
// Plex was switched off) so the page shows the result; the reloaded page does not render it, so polling stops. A
// failed poll is retried with a growing delay and abandoned after a few in a row. Without JavaScript the page is
// reloaded by hand.
export const PlexSyncPoller = clientEntry(
  import.meta.url,
  function PlexSyncPoller(handle: Handle<PlexSyncPollerProps>) {
    let stale = false;

    handle.queueTask(() => {
      const { signal } = handle;
      let timer: ReturnType<typeof setTimeout> | undefined;
      let failures = 0;

      async function poll() {
        let outcome: ReturnType<typeof classifyPoll> = 'failed';
        try {
          const response = await fetch(handle.props.statusHref, {
            signal: AbortSignal.any([signal, AbortSignal.timeout(REQUEST_TIMEOUT_MS)]),
            headers: { Accept: 'application/json' },
            cache: 'no-store',
          });
          outcome = classifyPoll(response.status, await response.json().catch(() => null));
        } catch {
          if (signal.aborted) return;
        }
        if (outcome === 'finished' || outcome === 'gone') {
          window.location.reload();
          return;
        }
        failures = outcome === 'failed' ? failures + 1 : 0;
        const delay = nextDelay(failures);
        if (delay === null) {
          stale = true;
          handle.update();
        } else if (!signal.aborted) timer = setTimeout(() => void poll(), delay);
      }

      timer = setTimeout(() => void poll(), POLL_MS);
      signal.addEventListener('abort', () => clearTimeout(timer));
    });

    return () => (
      <span role="status" class="text-sm text-amber-400" data-testid="plex-sync-poller">
        {stale ? 'Status may be out of date. Reload the page to refresh it.' : ''}
      </span>
    );
  }
);
