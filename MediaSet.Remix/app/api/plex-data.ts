import { baseUrl } from '../constants.server.ts';
import { apiFetch } from '../utils/apiFetch.server.ts';
import { serverLogger } from '../utils/serverLogger.ts';
import { getIntegrations } from './integrations-data.ts';

/**
 * The single gate for the Plex UI: true only when `/config/integrations` reports Plex enabled with movies among
 * its entity types. Every Plex piece of the UI depends on it; a failed lookup counts as off.
 */
export async function isPlexEnabledForMovies(): Promise<boolean> {
  const integrations = await getIntegrations();
  const plex = integrations.find((integration) => integration.key === 'plex');
  return Boolean(plex?.enabled && plex.entityTypes?.some((type) => type.toLowerCase() === 'movies'));
}

export type PlexFailure = 'unreachable' | 'syncInProgress' | 'itemMissing' | 'notLinked' | 'other';

/** A Plex action the API refused or could not complete; `failure` says which of the known causes it was. */
export class PlexActionError extends Error {
  readonly failure: PlexFailure;
  readonly status: number;

  constructor(failure: PlexFailure, status: number) {
    super(`Plex action failed: ${failure} (${status})`);
    this.name = 'PlexActionError';
    this.failure = failure;
    this.status = status;
  }
}

async function problemTitle(response: Response): Promise<string> {
  try {
    const problem = (await response.json()) as { title?: unknown };
    return typeof problem.title === 'string' ? problem.title : '';
  } catch {
    return '';
  }
}

/**
 * Re-reads one linked movie from Plex (POST; only ever called from a server action, never on page load).
 * Throws a `PlexActionError` naming the cause when the API answers with an error. The response body, which
 * could carry a file path for other calls, is not used.
 */
export async function refreshPlexItem(movieId: string): Promise<void> {
  const operation = 'refreshPlexItem';
  let response: Response;
  try {
    response = await apiFetch(`${baseUrl}/plex/movies/items/${encodeURIComponent(movieId)}/refresh`, {
      method: 'POST',
    });
  } catch (error) {
    serverLogger.error('Could not reach the API to refresh a Plex item', error, { operation, id: movieId });
    throw new PlexActionError('other', 0);
  }
  if (response.ok) return;

  const title = await problemTitle(response);
  serverLogger.warn('Plex refresh was refused', { operation, id: movieId, status: response.status });
  if (response.status === 502) throw new PlexActionError('unreachable', 502);
  if (response.status === 409) throw new PlexActionError('syncInProgress', 409);
  if (response.status === 400) throw new PlexActionError('notLinked', 400);
  if (response.status === 404 && title === 'Plex item not found') throw new PlexActionError('itemMissing', 404);
  throw new PlexActionError('other', response.status);
}
