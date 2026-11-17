import { baseUrl } from '../constants.server.ts';
import { apiFetch } from '../utils/apiFetch.server.ts';
import { createServerLogger } from '../utils/serverLogger.ts';
import type { PlexEntityStatus, PlexScope, PlexSearchResult, PlexStatistics } from '../models.ts';
import { getIntegrations } from './integrations-data.ts';

const serverLogger = createServerLogger('app/api/plex-data');

/** Which Plex entity types the UI treats as enabled; a failed lookup counts as off. */
export async function getPlexEnabledTypes(): Promise<{ movies: boolean; music: boolean }> {
  const integrations = await getIntegrations();
  const plex = integrations.find((integration) => integration.key === 'plex');
  const has = (name: string) => Boolean(plex?.enabled && plex.entityTypes?.some((type) => type.toLowerCase() === name));
  return { movies: has('movies'), music: has('music') };
}

/**
 * The single gate for the Plex movie UI: true only when `/config/integrations` reports Plex enabled with movies among
 * its entity types. Every Plex piece of the movie UI depends on it; a failed lookup counts as off.
 */
export async function isPlexEnabledForMovies(): Promise<boolean> {
  return (await getPlexEnabledTypes()).movies;
}

export type PlexFailure =
  | 'unreachable'
  | 'syncInProgress'
  | 'itemMissing'
  | 'movieMissing'
  | 'musicMissing'
  | 'notLinked'
  | 'other';

/** The entity types with Plex routes (`/plex/movies/...`, `/plex/music/...`). */
export type PlexEntityKind = 'movies' | 'music';

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
  if (response.status === 404 && title === 'Music not found') throw new PlexActionError('musicMissing', 404);
  if (response.status === 404 && title === 'Not linked to Plex') throw new PlexActionError('notLinked', 404);
  throw new PlexActionError('other', response.status);
}

interface PlexCallOptions {
  /** The call's name and the entry id, for the log. */
  operation: string;
  id?: string;
  path: string;
  init?: RequestInit;
  /** What a 400 means for the call; defaults to `other`. */
  badRequest?: PlexFailure;
  /** Which entity type's routes to call; defaults to movies. */
  kind?: PlexEntityKind;
}

async function plexCall({
  operation,
  id,
  path,
  init,
  badRequest = 'other',
  kind = 'movies',
}: PlexCallOptions): Promise<Response> {
  let response: Response;
  try {
    response = await apiFetch(`${baseUrl}/plex/${kind}/${path}`, init);
  } catch (error) {
    serverLogger.error('Could not reach the API for a Plex request', error, { operation, id });
    throw new PlexActionError('other', 0);
  }
  if (!response.ok) await failFrom(response, operation, id, badRequest);
  return response;
}

const itemPath = (entryId: string) => `items/${encodeURIComponent(entryId)}`;

/**
 * Re-reads one linked movie from Plex (POST; only ever called from a server action, never on page load).
 * Throws a `PlexActionError` naming the cause when the API answers with an error. The response body, which
 * could carry a file path for other calls, is not used.
 */
export async function refreshPlexItem(entryId: string): Promise<void> {
  await plexCall({
    operation: 'refreshPlexItem',
    id: entryId,
    path: `${itemPath(entryId)}/refresh`,
    init: { method: 'POST' },
    badRequest: 'notLinked',
  });
}

/** Searches Plex by title (GET). Throws a `PlexActionError` when Plex cannot be reached. */
export async function searchPlex(query: string, kind: PlexEntityKind = 'movies'): Promise<PlexSearchResult[]> {
  const response = await plexCall({
    operation: 'searchPlex',
    path: `search?query=${encodeURIComponent(query)}`,
    kind,
  });
  const results = (await response.json()) as PlexSearchResult[];
  return Array.isArray(results) ? results : [];
}

/**
 * The seasons a TV entry's title says, to prefill the scope step (GET; reads only the entry, never Plex).
 * Kind `unreadable` means the title does not say.
 */
export async function getPlexScope(entryId: string): Promise<PlexScope> {
  const response = await plexCall({
    operation: 'getPlexScope',
    id: entryId,
    path: `${itemPath(entryId)}/scope`,
  });
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
export async function linkPlexItem(
  entryId: string,
  request: PlexLinkRequest,
  kind: PlexEntityKind = 'movies'
): Promise<void> {
  await plexCall({
    operation: 'linkPlexItem',
    id: entryId,
    path: `${itemPath(entryId)}/link`,
    init: {
      method: 'PUT',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(request),
    },
    kind,
  });
}

/** Removes an entry's Plex link and its stored Plex data (DELETE). */
export async function unlinkPlexItem(entryId: string, kind: PlexEntityKind = 'movies'): Promise<void> {
  await plexCall({
    operation: 'unlinkPlexItem',
    id: entryId,
    path: `${itemPath(entryId)}/link`,
    init: { method: 'DELETE' },
    kind,
  });
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

/** The entity types the statistics can be limited to. */
export type PlexStatisticsSection = 'movies' | 'music';

/**
 * The Plex statistics (GET `/plex/statistics`), or null when they cannot be read. They are a bonus on the sync page, so a
 * failure is logged and the page renders without them. `sections` limits what the API computes (`movies` covers TV too).
 */
export async function getPlexStatistics(sections?: readonly PlexStatisticsSection[]): Promise<PlexStatistics | null> {
  try {
    // Without sections the API computes every enabled entity type; a caller that needs one asks for just that
    const query = sections && sections.length > 0 ? `?sections=${sections.map(encodeURIComponent).join(',')}` : '';
    const response = await apiFetch(`${baseUrl}/plex/statistics${query}`);
    if (!response.ok) {
      serverLogger.warn('Plex statistics were refused', { operation: 'getPlexStatistics', status: response.status });
      return null;
    }
    const body = (await response.json()) as Partial<PlexStatistics> | null;
    if (!body || typeof body !== 'object') return null;
    const hasVideo = Boolean(body.movies && body.tv);
    // Each section is present only when its entity type is enabled; a body with none is not statistics
    return hasVideo || body.music ? (body as PlexStatistics) : null;
  } catch (error) {
    serverLogger.error('Could not read the Plex statistics', error, { operation: 'getPlexStatistics' });
    return null;
  }
}
