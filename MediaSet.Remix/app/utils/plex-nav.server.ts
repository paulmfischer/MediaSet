import { AsyncLocalStorage } from 'async_hooks';
import type { Middleware } from 'remix/router';

import { getPlexEnabledTypes, isPlexEnabledForMovies } from '../api/plex-data.ts';
import { Entity } from '../models.ts';

// Whether the current request has Plex enabled for movies. Decided once per page request by the middleware below and
// reused by the header (which renders synchronously and cannot call the API) and by the Plex handlers.
const storage = new AsyncLocalStorage<{ plexEnabled: boolean; plexMusicEnabled: boolean; plexPageEnabled: boolean }>();

/**
 * True when the request being rendered has Plex enabled for movies (which include TV entries) or music, so the Plex
 * statistics page and its nav link are available; false outside a request or when it is off.
 */
export function isPlexNavEnabled(): boolean {
  return storage.getStore()?.plexPageEnabled ?? false;
}

/** The gate for the current request, without another lookup; outside the middleware it looks the gate up. */
export async function isPlexEnabledForRequest(): Promise<boolean> {
  const store = storage.getStore();
  return store ? store.plexEnabled : isPlexEnabledForMovies();
}

/** The gate for Plex on the music list and detail pages: Plex enabled for music. Outside the middleware it looks the gate up. */
export async function isPlexMusicEnabledForRequest(): Promise<boolean> {
  const store = storage.getStore();
  return store ? store.plexMusicEnabled : (await getPlexEnabledTypes()).music;
}

/** The Plex gate of an entity's list and detail pages: movies and music have a Plex layer, every other entity has none. */
export async function isPlexEnabledForEntity(entity: Entity): Promise<boolean> {
  if (entity === Entity.Movies) return isPlexEnabledForRequest();
  if (entity === Entity.Musics) return isPlexMusicEnabledForRequest();
  return false;
}

/** The gate of the Plex statistics page: Plex enabled for movies (and TV) or music. */
export async function isPlexPageEnabledForRequest(): Promise<boolean> {
  const store = storage.getStore();
  if (store) return store.plexPageEnabled;
  const types = await getPlexEnabledTypes();
  return types.movies || types.music;
}

// Asset and config requests render no page, so they skip the lookup. Files served from `public/` are answered by
// the static middleware, which the gate is placed after.
const SKIPPED = /^\/(assets\/|config\.json$)/;

/** Resolves the Plex gate once for page requests so every header and Plex handler can use it. */
export function plexNavGate(): Middleware {
  return async (context, next) => {
    if (SKIPPED.test(context.url.pathname)) return next();
    const types = await getPlexEnabledTypes();
    return storage.run(
      { plexEnabled: types.movies, plexMusicEnabled: types.music, plexPageEnabled: types.movies || types.music },
      next
    );
  };
}
