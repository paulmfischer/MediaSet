import { AsyncLocalStorage } from 'async_hooks';
import type { Middleware } from 'remix/router';

import { isPlexEnabledForMovies } from '../api/plex-data.ts';

// Whether the current request has Plex enabled for movies. Decided once per page request by the middleware below and
// reused by the header (which renders synchronously and cannot call the API) and by the Plex handlers.
const storage = new AsyncLocalStorage<{ plexEnabled: boolean }>();

/** True when the request being rendered has Plex enabled for movies; false outside a request or when it is off. */
export function isPlexNavEnabled(): boolean {
  return storage.getStore()?.plexEnabled ?? false;
}

/** The gate for the current request, without another lookup; outside the middleware it looks the gate up. */
export async function isPlexEnabledForRequest(): Promise<boolean> {
  const store = storage.getStore();
  return store ? store.plexEnabled : isPlexEnabledForMovies();
}

// Asset and config requests render no page, so they skip the lookup. Files served from `public/` are answered by
// the static middleware, which the gate is placed after.
const SKIPPED = /^\/(assets\/|config\.json$)/;

/** Resolves the Plex gate once for page requests so every header and Plex handler can use it. */
export function plexNavGate(): Middleware {
  return async (context, next) => {
    if (SKIPPED.test(context.url.pathname)) return next();
    const plexEnabled = await isPlexEnabledForMovies();
    return storage.run({ plexEnabled }, next);
  };
}
