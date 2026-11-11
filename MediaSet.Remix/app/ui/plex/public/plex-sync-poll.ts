// The decisions of the sync page's poller, kept apart from the browser code so they can be tested.

export const POLL_MS = 3000;
export const MAX_RETRY_MS = 30_000;
/** Consecutive failed polls after which the poller gives up (the page is still correct, just not live). */
export const MAX_FAILURES = 5;

type PolledStatus = { enabled?: boolean; lastRun?: { state?: string } | null };

export type PollOutcome = 'active' | 'finished' | 'gone' | 'failed';

const isActive = (status: PolledStatus): boolean =>
  status.enabled !== false && (status.lastRun?.state === 'running' || status.lastRun?.state === 'queued');

/**
 * What one poll answered: `active` (keep polling), `finished` (no run is active, so reload to show the result),
 * `gone` (404: Plex was switched off, so reload to show the real page) or `failed` (anything else).
 */
export function classifyPoll(httpStatus: number, body: unknown): PollOutcome {
  if (httpStatus === 404) return 'gone';
  if (httpStatus < 200 || httpStatus > 299 || !Array.isArray(body)) return 'failed';
  return (body as PolledStatus[]).some(isActive) ? 'active' : 'finished';
}

/** The wait before the next poll after `failures` failures in a row, or null once the poller should stop. */
export function nextDelay(failures: number): number | null {
  if (failures >= MAX_FAILURES) return null;
  return Math.min(POLL_MS * 2 ** failures, MAX_RETRY_MS);
}
