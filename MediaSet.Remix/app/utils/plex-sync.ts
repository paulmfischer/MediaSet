import type { PlexEntityStatus, PlexSyncRun } from '../models.ts';

/** A run that has not ended: waiting for the shared lock or in progress. */
export function isActiveRun(run: PlexSyncRun | null | undefined): run is PlexSyncRun {
  return run?.state === 'queued' || run?.state === 'running';
}

/** True when any of the entity types has an active run. */
export function hasActiveRun(statuses: PlexEntityStatus[]): boolean {
  return statuses.some((status) => isActiveRun(status.lastRun));
}

/** "Movies" for the API's entity type name. */
export function entityTypeLabel(entityType: string): string {
  return entityType.charAt(0).toUpperCase() + entityType.slice(1);
}

const STATE_LABELS: Record<string, string> = {
  queued: 'Queued',
  running: 'Running',
  succeeded: 'Succeeded',
  failed: 'Failed',
  interrupted: 'Interrupted',
};

/** The state as shown to the collector; an unknown state is shown as the API sent it. */
export function runStateLabel(state: string): string {
  return STATE_LABELS[state] ?? state;
}
