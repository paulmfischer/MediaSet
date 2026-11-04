import * as assert from 'remix/assert';
import { describe, it } from 'remix/test';

import { createRouter } from 'remix/router';

import { createAppRouter } from '../router.ts';
import { apiFetch } from '../utils/apiFetch.server.ts';
import { requestContext } from './request-context.ts';
import { securityHeaders } from './security-headers.ts';

const get = (router: { fetch: (url: URL) => Promise<Response> }, path: string) =>
  router.fetch(new URL(path, 'http://localhost'));

const EXPECTED_HEADERS: Record<string, string> = {
  'x-content-type-options': 'nosniff',
  'x-frame-options': 'DENY',
  'referrer-policy': 'strict-origin-when-cross-origin',
  'permissions-policy': 'camera=(self), microphone=(), geolocation=(), payment=()',
};

function assertSecurityHeaders(response: Response) {
  for (const [name, value] of Object.entries(EXPECTED_HEADERS)) {
    assert.equal(response.headers.get(name), value, name);
  }
}

describe('securityHeaders middleware', () => {
  const dev = createAppRouter({ production: false });
  const prod = createAppRouter({ production: true });

  it('should set the security headers when the response is a not-found page', async () => {
    const response = await get(dev, '/no/such/page/anywhere');
    assert.equal(response.status, 404);
    assertSecurityHeaders(response);
  });

  it('should set the security headers when the response is a static file', async () => {
    const response = await get(dev, '/favicon.ico');
    assert.equal(response.status, 200);
    assertSecurityHeaders(response);
  });

  it('should omit the Content-Security-Policy when not in production', async () => {
    const response = await get(dev, '/no/such/page/anywhere');
    assert.equal(response.headers.get('content-security-policy'), null);
  });

  it('should set the Content-Security-Policy on every kind of response when in production', async () => {
    for (const path of ['/no/such/page/anywhere', '/favicon.ico']) {
      const response = await get(prod, path);
      assertSecurityHeaders(response);
      assert.ok(response.headers.get('content-security-policy'), path);
    }
  });

  it('should allow blob: and WebAssembly compilation for the import-map polyfill, no workers, and the hardening directives in the production policy', async () => {
    const policy = await cspFor('http://api.example:8080');
    assert.deepEqual(policy.get('script-src'), ["'self'", "'unsafe-inline'", 'blob:', "'wasm-unsafe-eval'"]);
    assert.equal(policy.has('worker-src'), false);
    assert.equal(policy.get('script-src')?.includes("'unsafe-eval'"), false);
    assert.ok(policy.get('img-src')?.includes('blob:'));
    assert.deepEqual(policy.get('base-uri'), ["'self'"]);
    assert.deepEqual(policy.get('form-action'), ["'self'"]);
    assert.deepEqual(policy.get('object-src'), ["'none'"]);
    assert.deepEqual(policy.get('frame-ancestors'), ["'none'"]);
  });

  it('should allow images from the configured client API origin when in production', async () => {
    const policy = await cspFor('http://media.example:8080/api/');
    assert.deepEqual(policy.get('img-src'), ["'self'", 'data:', 'blob:', 'https:', 'http://media.example:8080']);
  });

  it('should add no API origin to img-src when the client API URL is relative, unset or invalid', async () => {
    for (const url of ['/dev-api', '', 'not a url', 'ftp://files.example']) {
      const policy = await cspFor(url);
      assert.deepEqual(policy.get('img-src'), ["'self'", 'data:', 'blob:', 'https:'], url);
    }
  });

  it('should keep status, body and headers of redirects and 304 responses', async () => {
    const router = createRouter({ middleware: [securityHeaders({ production: true })] });
    router.get('/moved', () => new Response(null, { status: 303, headers: { Location: '/elsewhere' } }));
    router.get('/same', () => new Response(null, { status: 304, headers: { ETag: '"x"' } }));
    // Response.redirect() has immutable headers, so this takes the copy-the-response fallback.
    router.get('/frozen', () => Response.redirect('http://localhost/target', 302));
    const moved = await get(router, '/moved');
    assert.equal(moved.status, 303);
    assert.equal(moved.headers.get('location'), '/elsewhere');
    assertSecurityHeaders(moved);
    const same = await get(router, '/same');
    assert.equal(same.status, 304);
    assert.equal(same.headers.get('etag'), '"x"');
    assertSecurityHeaders(same);
    const frozen = await get(router, '/frozen');
    assert.equal(frozen.status, 302);
    assert.equal(frozen.headers.get('location'), 'http://localhost/target');
    assertSecurityHeaders(frozen);
  });
});

// Reads the production Content-Security-Policy a router sends for the given client API URL.
async function cspFor(clientApiUrl: string): Promise<Map<string, string[]>> {
  const response = await get(createAppRouter({ production: true, clientApiUrl }), '/favicon.ico');
  const header = response.headers.get('content-security-policy') ?? '';
  return new Map(
    header.split('; ').map((directive) => {
      const [name, ...sources] = directive.split(' ');
      return [name, sources];
    })
  );
}

describe('requestContext middleware', () => {
  it('should send a distinct, well-formed traceparent for each request', async (t) => {
    const original = globalThis.fetch;
    const sent: (string | null)[] = [];
    globalThis.fetch = (async (_input: RequestInfo | URL, init?: RequestInit) => {
      sent.push(new Headers(init?.headers).get('traceparent'));
      return Response.json([]);
    }) as typeof fetch;
    t.after(() => {
      globalThis.fetch = original;
    });

    const router = createRouter({ middleware: [requestContext()] });
    router.get('/probe', async () => {
      // Two calls in one request share a trace context.
      await apiFetch('http://api.test/a');
      await apiFetch('http://api.test/b');
      return new Response('ok');
    });

    await router.fetch(new URL('http://localhost/probe'));
    await router.fetch(new URL('http://localhost/probe'));

    assert.equal(sent.length, 4);
    for (const value of sent) assert.match(value ?? '', /^00-[0-9a-f]{32}-[0-9a-f]{16}-01$/);
    assert.equal(sent[0], sent[1]);
    assert.equal(sent[2], sent[3]);
    assert.notEqual(sent[0], sent[2]);
  });
});
