import { AsyncLocalStorage } from 'async_hooks';
import type { Middleware } from 'remix/router';

import { isPlexEnabledForMovies } from '../api/plex-data.ts';

// Whether the current request's layout shows the gear menu's Plex link. Decided once per request by the middleware
// below, because the header renders synchronously and cannot call the API itself.
const storage = new AsyncLocalStorage<{ plexEnabled: boolean }>();

/** True when the request being rendered has Plex enabled for movies; false outside a request or when it is off. */
export function isPlexNavEnabled(): boolean {
  return storage.getStore()?.plexEnabled ?? false;
}

// Asset and config requests render no page, so they skip the lookup.
const SKIPPED = /^\/(assets\/|config\.json$)/;

/** Resolves the Plex gate for page requests so every page's header can show the Plex link only when it is on. */
export function plexNavGate(): Middleware {
  return async (context, next) => {
    if (SKIPPED.test(context.url.pathname)) return next();
    const plexEnabled = await isPlexEnabledForMovies();
    return storage.run({ plexEnabled }, next);
  };
}
