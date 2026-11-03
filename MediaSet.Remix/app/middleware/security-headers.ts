import type { Middleware } from 'remix/router';

export interface SecurityHeadersOptions {
  // Adds the Content-Security-Policy. Defaults to production mode only.
  production?: boolean;
}

// Hydration needs more than a plain 'self' policy:
// - script-src 'unsafe-inline': the server inlines the hydration data and entry bootstrap scripts.
// - script-src blob: and 'wasm-unsafe-eval': the browser runtime and the barcode scanner load
//   code from blob: URLs and compile WebAssembly (the latter does not need 'unsafe-eval').
// - worker-src blob: the scanner decodes frames in a worker started from a blob: URL.
// - img-src blob: previews of a chosen image file; https: for cover art from external sources.
// - style-src 'unsafe-inline': inline style attributes on rendered elements.
// Image URLs from our own API are served through the app, so the API origin is not listed.
export const CONTENT_SECURITY_POLICY = [
  "default-src 'self'",
  "img-src 'self' data: blob: https:",
  "style-src 'self' 'unsafe-inline'",
  "script-src 'self' 'unsafe-inline' blob: 'wasm-unsafe-eval'",
  'worker-src blob:',
].join('; ');

export function securityHeaders(options: SecurityHeadersOptions = {}): Middleware {
  const production = options.production ?? process.env.NODE_ENV === 'production';

  return async (_context, next) => {
    const response = await next();
    // Headers of a response built elsewhere (for example a fetched one) can be immutable.
    const headers = new Headers(response.headers);
    headers.set('X-Content-Type-Options', 'nosniff');
    headers.set('X-Frame-Options', 'DENY');
    headers.set('Referrer-Policy', 'strict-origin-when-cross-origin');
    headers.set('Permissions-Policy', 'camera=self, microphone=(), geolocation=(), payment=()');
    if (production) headers.set('Content-Security-Policy', CONTENT_SECURITY_POLICY);
    return new Response(response.body, { status: response.status, statusText: response.statusText, headers });
  };
}
