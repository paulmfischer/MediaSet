import type { Middleware } from 'remix/router';
import { withRequestContext, type RequestContext } from '../utils/apiFetch.server.ts';

function hex(length: number) {
  return Array.from(crypto.getRandomValues(new Uint8Array(length / 2)))
    .map((b) => b.toString(16).padStart(2, '0'))
    .join('');
}

// Runs the rest of the request inside the AsyncLocalStorage context that apiFetch reads.
export function requestTrace(): Middleware {
  return (_context, next) => {
    let traceId = hex(32);
    let spanId = hex(16);
    let ctx: RequestContext = {
      traceId,
      spanId,
      traceparent: `00-${traceId}-${spanId}-01`,
    };
    return withRequestContext(ctx, next) as Promise<Response>;
  };
}
