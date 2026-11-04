import type { Middleware } from 'remix/router';

import { clientApiUrl as defaultClientApiUrl } from '../constants.server.ts';

export interface SecurityHeadersOptions {
  // Adds the Content-Security-Policy. Defaults to production mode only.
  production?: boolean;
  // The API URL the browser loads cover images from. Defaults to the configured clientApiUrl.
  clientApiUrl?: string;
}

// The origin of an absolute http(s) URL, or null for a relative path (same origin, covered by
// 'self'), an unset value or anything that is not a valid URL.
export function originOf(url: string | undefined): string | null {
  if (!url) return null;
  try {
    const parsed = new URL(url);
    return parsed.protocol === 'http:' || parsed.protocol === 'https:' ? parsed.origin : null;
  } catch {
    return null;
  }
}

// Cover images are loaded by the browser straight from the API (`<img src="${clientApiUrl}/static/...">`),
// so that origin must be in img-src; it is often plain http, which `https:` does not cover.
// blob: in img-src is for previewing a chosen image file. Hydration inlines scripts and styles, hence
// 'unsafe-inline'. script-src blob: and 'wasm-unsafe-eval' are needed by the multiple-import-maps polyfill that
// loads every client entry (#733): its feature detection imports blob: modules in a frame, and where the browser
// lacks multiple import maps it links modules as blob: URLs and parses them with a WebAssembly lexer. Without
// them hydration logs CSP violations or fails. The barcode scanner needs neither (@zxing/library is a same-origin
// dynamic import covered by 'self'; no workers or wasm; the preview is a MediaStream), so there is no worker-src
// (it falls back to default-src 'self') and 'unsafe-eval' stays off. app/e2e/csp.test.e2e.ts and
// app/e2e/barcode-scanner.test.e2e.ts run the app under this policy and fail if a directive is removed.
export function buildContentSecurityPolicy(clientApiUrl?: string): string {
  const apiOrigin = originOf(clientApiUrl);
  return [
    "default-src 'self'",
    ['img-src', "'self'", 'data:', 'blob:', 'https:', ...(apiOrigin ? [apiOrigin] : [])].join(' '),
    "style-src 'self' 'unsafe-inline'",
    "script-src 'self' 'unsafe-inline' blob: 'wasm-unsafe-eval'",
    "base-uri 'self'",
    "form-action 'self'",
    "object-src 'none'",
    "frame-ancestors 'none'",
  ].join('; ');
}

export function securityHeaders(options: SecurityHeadersOptions = {}): Middleware {
  const production = options.production ?? process.env.NODE_ENV === 'production';
  const policy = production ? buildContentSecurityPolicy(options.clientApiUrl ?? defaultClientApiUrl) : null;

  return async (_context, next) => {
    const response = await next();
    const values: Array<[string, string]> = [
      ['X-Content-Type-Options', 'nosniff'],
      ['X-Frame-Options', 'DENY'],
      ['Referrer-Policy', 'strict-origin-when-cross-origin'],
      ['Permissions-Policy', 'camera=(self), microphone=(), geolocation=(), payment=()'],
    ];
    if (policy) values.push(['Content-Security-Policy', policy]);
    try {
      for (const [name, value] of values) response.headers.set(name, value);
      return response;
    } catch {
      // Headers of a response built elsewhere (for example a fetched one) can be immutable.
      const headers = new Headers(response.headers);
      for (const [name, value] of values) headers.set(name, value);
      return new Response(response.body, { status: response.status, statusText: response.statusText, headers });
    }
  };
}
