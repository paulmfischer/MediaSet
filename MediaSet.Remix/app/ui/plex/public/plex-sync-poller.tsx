import { clientEntry, type Handle } from 'remix/component';

interface PlexSyncPollerProps {
  /** The same-origin route that answers with every entity type's status as JSON. */
  statusHref: string;
}

const POLL_MS = 3000;

type PolledStatus = { lastRun?: { state?: string } | null };

const isActive = (status: PolledStatus): boolean =>
  status.lastRun?.state === 'running' || status.lastRun?.state === 'queued';

// Rendered only while a run is active. It polls the status route, and reloads the page once no run is active so the
// finished run's counts show; the reloaded page does not render it, so polling stops. Without JavaScript the page
// is simply reloaded by hand. A failed poll is retried on the next tick.
export const PlexSyncPoller = clientEntry(
  import.meta.url,
  function PlexSyncPoller(handle: Handle<PlexSyncPollerProps>) {
    handle.queueTask(() => {
      const { signal } = handle;
      let timer: ReturnType<typeof setTimeout> | undefined;

      async function poll() {
        try {
          const response = await fetch(handle.props.statusHref, {
            signal,
            headers: { Accept: 'application/json' },
            cache: 'no-store',
          });
          if (response.ok) {
            const statuses = (await response.json()) as PolledStatus[];
            if (Array.isArray(statuses) && !statuses.some(isActive)) {
              window.location.reload();
              return;
            }
          }
        } catch {
          if (signal.aborted) return;
        }
        if (!signal.aborted) timer = setTimeout(() => void poll(), POLL_MS);
      }

      timer = setTimeout(() => void poll(), POLL_MS);
      signal.addEventListener('abort', () => clearTimeout(timer));
    });

    return () => <span hidden data-testid="plex-sync-poller"></span>;
  }
);
