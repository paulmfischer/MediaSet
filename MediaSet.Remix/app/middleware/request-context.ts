import type { Middleware } from 'remix/router';

import { createRequestContext, withRequestContext } from '../utils/apiFetch.server.ts';

// Runs each request in its own async context so apiFetch sends a distinct traceparent.
export function requestContext(): Middleware {
  return (_context, next) => withRequestContext(createRequestContext(), next);
}
