import { baseUrl } from '../constants.server.ts';
import { apiFetch } from '../utils/apiFetch.server.ts';
import { serverLogger } from '../utils/serverLogger.ts';
import type { PlexEntityStatus, PlexScope, PlexSearchResult, PlexStatistics } from '../models.ts';
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

export type PlexFailure = 'unreachable' | 'syncInProgress' | 'itemMissing' | 'movieMissing' | 'notLinked' | 'other';

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
 * Turns a refused Plex call into a `PlexActionError`. The API tells the causes apart by status and problem
 * title; `badRequest` is what a 400 means for the call (refresh: not linked; link and search: nothing the
 * user can fix, so `other`).
 */
async function failFrom(
  response: Response,
  operation: string,
  id: string | undefined,
  badRequest: PlexFailure = 'other'
): Promise<never> {
  const title = await problemTitle(response);
  serverLogger.warn('Plex request was refused', { operation, id, status: response.status });
  if (response.status === 502) throw new PlexActionError('unreachable', 502);
  if (response.status === 409) throw new PlexActionError('syncInProgress', 409);
  if (response.status === 400) throw new PlexActionError(badRequest, 400);
  if (response.status === 404 && title === 'Plex item not found') throw new PlexActionError('itemMissing', 404);
  if (response.status === 404 && title === 'Movie not found') throw new PlexActionError('movieMissing', 404);
  if (response.status === 404 && title === 'Not linked to Plex') throw new PlexActionError('notLinked', 404);
  throw new PlexActionError('other', response.status);
}

async function plexCall(
  operation: string,
  id: string | undefined,
  path: string,
  init?: RequestInit,
  badRequest: PlexFailure = 'other'
): Promise<Response> {
  let response: Response;
  try {
    response = await apiFetch(`${baseUrl}/plex/movies/${path}`, init);
  } catch (error) {
    serverLogger.error('Could not reach the API for a Plex request', error, { operation, id });
    throw new PlexActionError('other', 0);
  }
  if (!response.ok) await failFrom(response, operation, id, badRequest);
  return response;
}

const itemPath = (movieId: string) => `items/${encodeURIComponent(movieId)}`;

/**
 * Re-reads one linked movie from Plex (POST; only ever called from a server action, never on page load).
 * Throws a `PlexActionError` naming the cause when the API answers with an error. The response body, which
 * could carry a file path for other calls, is not used.
 */
export async function refreshPlexItem(movieId: string): Promise<void> {
  await plexCall('refreshPlexItem', movieId, `${itemPath(movieId)}/refresh`, { method: 'POST' }, 'notLinked');
}

/** Searches Plex by title (GET). Throws a `PlexActionError` when Plex cannot be reached. */
export async function searchPlex(query: string): Promise<PlexSearchResult[]> {
  const response = await plexCall('searchPlex', undefined, `search?query=${encodeURIComponent(query)}`);
  const results = (await response.json()) as PlexSearchResult[];
  return Array.isArray(results) ? results : [];
}

/**
 * The seasons a TV entry's title says, to prefill the scope step (GET; reads only the entry, never Plex).
 * Kind `unreadable` means the title does not say.
 */
export async function getPlexScope(movieId: string): Promise<PlexScope> {
  const response = await plexCall('getPlexScope', movieId, `${itemPath(movieId)}/scope`);
  return (await response.json()) as PlexScope;
}

/** The seasons a TV entry covers, as the link call takes them (season numbers 0 to 99). */
export type PlexLinkScope =
  | { kind: 'season'; season: number }
  | { kind: 'range'; from: number; to: number }
  | { kind: 'all' };

/** What a manual link sends: the Plex item and, for a TV entry, the seasons it covers. */
export type PlexLinkRequest = { plexItemId: string; scope?: PlexLinkScope };

/**
 * Links a movie or TV entry to a Plex item by hand (PUT). The response, which carries a file path, is not used.
 * Throws a `PlexActionError` naming the cause when the API answers with an error.
 */
export async function linkPlexItem(movieId: string, request: PlexLinkRequest): Promise<void> {
  await plexCall('linkPlexItem', movieId, `${itemPath(movieId)}/link`, {
    method: 'PUT',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(request),
  });
}

/** Removes a movie's Plex link and its stored Plex data (DELETE). */
export async function unlinkPlexItem(movieId: string): Promise<void> {
  await plexCall('unlinkPlexItem', movieId, `${itemPath(movieId)}/link`, { method: 'DELETE' });
}

/**
 * The sync status of every Plex entity type (GET `/plex/status`), disabled ones included. Throws a
 * `PlexActionError` when the API cannot be reached or answers with an error.
 */
export async function getPlexStatuses(): Promise<PlexEntityStatus[]> {
  let response: Response;
  try {
    response = await apiFetch(`${baseUrl}/plex/status`);
  } catch (error) {
    serverLogger.error('Could not reach the API for the Plex status', error, { operation: 'getPlexStatuses' });
    throw new PlexActionError('other', 0);
  }
  if (!response.ok) await failFrom(response, 'getPlexStatuses', undefined);
  const statuses = (await response.json()) as PlexEntityStatus[];
  return Array.isArray(statuses) ? statuses : [];
}

/**
 * Starts a sync for one entity type (POST; only ever called from a server action). Returns true when it started and
 * false on a 409, which is not a failure: a run is already queued or running.
 */
export async function startPlexSync(entityType: string): Promise<boolean> {
  let response: Response;
  try {
    response = await apiFetch(`${baseUrl}/plex/${encodeURIComponent(entityType)}/sync`, { method: 'POST' });
  } catch (error) {
    serverLogger.error('Could not reach the API to start a Plex sync', error, { operation: 'startPlexSync' });
    throw new PlexActionError('other', 0);
  }
  if (response.status === 409) return false;
  if (!response.ok) await failFrom(response, 'startPlexSync', entityType);
  return true;
}

/**
 * The Plex statistics (GET `/plex/statistics`), or null when they cannot be read. They are a bonus on the sync page, so a
 * failure is logged and the page renders without them.
 */
export async function getPlexStatistics(): Promise<PlexStatistics | null> {
  try {
    const response = await apiFetch(`${baseUrl}/plex/statistics`);
    if (!response.ok) {
      serverLogger.warn('Plex statistics were refused', { operation: 'getPlexStatistics', status: response.status });
      return null;
    }
    const body = (await response.json()) as Partial<PlexStatistics> | null;
    return body && typeof body === 'object' && body.movies && body.tv ? (body as PlexStatistics) : null;
  } catch (error) {
    serverLogger.error('Could not read the Plex statistics', error, { operation: 'getPlexStatistics' });
    return null;
  }
}
