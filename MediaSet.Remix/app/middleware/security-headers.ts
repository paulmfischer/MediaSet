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
// 'unsafe-inline'. script-src blob: and 'wasm-unsafe-eval' and worker-src blob: are allowed (#687) for the
// barcode scanner's decoders (#698); 'unsafe-eval' stays off. The scanner itself needs no more than that:
// @zxing/library is a same-origin module loaded by dynamic import (covered by 'self') and the camera
// preview is a MediaStream, not a URL. app/e2e/barcode-scanner.test.e2e.ts runs it under this policy.
export function buildContentSecurityPolicy(clientApiUrl?: string): string {
  const apiOrigin = originOf(clientApiUrl);
  return [
    "default-src 'self'",
    ['img-src', "'self'", 'data:', 'blob:', 'https:', ...(apiOrigin ? [apiOrigin] : [])].join(' '),
    "style-src 'self' 'unsafe-inline'",
    "script-src 'self' 'unsafe-inline' blob: 'wasm-unsafe-eval'",
    'worker-src blob:',
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
