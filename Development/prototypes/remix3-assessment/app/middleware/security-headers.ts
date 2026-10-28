import type { Middleware } from 'remix/router';

// Ported from root.tsx `headers()` on main.
export function securityHeaders(): Middleware {
  return async (_context, next) => {
    let response = await next();
    let headers = new Headers(response.headers);
    headers.set('X-Content-Type-Options', 'nosniff');
    headers.set('X-Frame-Options', 'DENY');
    headers.set('Referrer-Policy', 'strict-origin-when-cross-origin');
    headers.set('Permissions-Policy', 'camera=self, microphone=(), geolocation=(), payment=()');

    if (process.env.NODE_ENV === 'production') {
      let apiUrl = process.env.clientApiUrl || 'http://localhost:7130';
      headers.set(
        'Content-Security-Policy',
        `default-src 'self'; img-src 'self' data: https: ${apiUrl}; style-src 'self' 'unsafe-inline'; script-src 'self' 'unsafe-inline' blob: 'wasm-unsafe-eval'`
      );
    }

    return new Response(response.body, {
      status: response.status,
      statusText: response.statusText,
      headers,
    });
  };
}
