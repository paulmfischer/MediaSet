import * as assert from 'remix/assert';
import { describe, it } from 'remix/test';

import { createAppRouter } from '../router.ts';
import { apiFetch } from '../utils/apiFetch.server.ts';
import { CONTENT_SECURITY_POLICY } from './security-headers.ts';
import { createRouter } from 'remix/router';
import { requestContext } from './request-context.ts';

const get = (router: { fetch: (url: URL) => Promise<Response> }, path: string) =>
  router.fetch(new URL(path, 'http://localhost'));

const EXPECTED_HEADERS: Record<string, string> = {
  'x-content-type-options': 'nosniff',
  'x-frame-options': 'DENY',
  'referrer-policy': 'strict-origin-when-cross-origin',
  'permissions-policy': 'camera=self, microphone=(), geolocation=(), payment=()',
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
      assert.equal(response.headers.get('content-security-policy'), CONTENT_SECURITY_POLICY, path);
    }
  });

  it('should allow blob: and WebAssembly compilation in the production policy', () => {
    const directives = new Map(
      CONTENT_SECURITY_POLICY.split('; ').map((directive) => {
        const [name, ...sources] = directive.split(' ');
        return [name, sources];
      })
    );
    assert.ok(directives.get('script-src')?.includes('blob:'));
    assert.ok(directives.get('script-src')?.includes("'wasm-unsafe-eval'"));
    assert.ok(directives.get('img-src')?.includes('blob:'));
    assert.deepEqual(directives.get('worker-src'), ['blob:']);
    assert.ok(!directives.get('script-src')?.includes("'unsafe-eval'"));
  });
});

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
