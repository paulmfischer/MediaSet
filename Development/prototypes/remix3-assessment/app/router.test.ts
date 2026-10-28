import * as assert from 'remix/assert';
import { describe, it } from 'remix/test';

import { router } from './router.ts';

const get = (path: string) => router.fetch(new URL(path, 'http://localhost'));

describe('router', () => {
  it('serves health and config.json (with cache header)', async () => {
    assert.deepEqual(await (await get('/health')).json(), { status: 'ok' });
    let config = await get('/config.json');
    assert.equal(config.headers.get('Cache-Control'), 'public, max-age=3600');
    assert.equal((await config.json()).apiUrl, 'http://localhost:7130');
  });

  it('filters entities by searchText, and literal routes win over /:entity', async () => {
    let html = await (await get('/books?searchText=dune')).text();
    assert.match(html, /<li>Dune<\/li>/);
    assert.doesNotMatch(html, /Neuromancer/);
    assert.match(await (await get('/stats')).text(), /Bar chart/);
  });

  it('returns 404 for unknown entity', async () => {
    assert.equal((await get('/nope')).status, 404);
  });

  it('sets security headers, and CSP only in production', async (t) => {
    let response = await get('/');
    assert.equal(response.headers.get('X-Frame-Options'), 'DENY');
    assert.equal(response.headers.get('Content-Security-Policy'), null);

    let prev = process.env.NODE_ENV;
    process.env.NODE_ENV = 'production';
    t.after(() => {
      process.env.NODE_ENV = prev;
    });
    let prod = await get('/');
    assert.match(
      prod.headers.get('Content-Security-Policy') ?? '',
      /script-src 'self' 'unsafe-inline' blob: 'wasm-unsafe-eval'/
    );
  });

  it('propagates a W3C traceparent to API fetches via AsyncLocalStorage', async (t) => {
    process.env.API_URL = 'http://api.test';
    t.after(() => {
      delete process.env.API_URL;
    });
    let seen: string[] = [];
    t.mock.method(globalThis, 'fetch', async (_url: unknown, init?: RequestInit) => {
      seen.push(new Headers(init?.headers).get('traceparent') ?? '');
      return Response.json([]);
    });
    await get('/books');
    await get('/books');
    assert.match(seen[0], /^00-[0-9a-f]{32}-[0-9a-f]{16}-01$/);
    assert.notEqual(seen[0], seen[1]); // distinct per request
  });
  it('add: invalid POST re-renders 400 with field errors and keeps submitted values', async () => {
    let body = new FormData();
    body.set('title', '   ');
    body.set('year', '12');
    body.append('genres', 'noir');
    let response = await router.fetch(new Request('http://localhost/books/add', { method: 'POST', body }));
    assert.equal(response.status, 400);
    let html = await response.text();
    assert.match(html, /Title is required/);
    assert.match(html, /Year must be 4 digits/);
    assert.match(html, /value="noir"[^>]*checked/);
  });

  it('add: valid POST saves (repeated genres) and 303-redirects to the list', async () => {
    let body = new FormData();
    body.set('title', 'Solaris');
    body.set('year', '1961');
    body.append('genres', 'scifi');
    body.append('genres', 'noir');
    let response = await router.fetch(new Request('http://localhost/books/add', { method: 'POST', body }));
    assert.equal(response.status, 303);
    assert.equal(response.headers.get('Location'), '/books');
    assert.match(await (await get('/books')).text(), /Solaris/);
  });
});
