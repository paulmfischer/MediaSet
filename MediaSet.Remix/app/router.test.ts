import * as assert from 'remix/assert';
import { describe, it } from 'remix/test';

import { withDevApiProxy } from './dev-api-proxy.ts';
import { router } from './router.ts';
import { routes } from './routes.ts';

const get = (path: string) => router.fetch(new URL(path, 'http://localhost'));

describe('router', () => {
  it('GET / renders the home page document with the prebuilt stylesheet and browser entry', async () => {
    const response = await get(routes.home.href());

    assert.equal(response.status, 200);
    assert.match(response.headers.get('Content-Type') ?? '', /text\/html/);
    const html = await response.text();
    assert.match(html, /<html lang="en" class="dark">/);
    assert.match(html, /<title>Dashboard \| MediaSet<\/title>/);
    assert.match(html, /<link rel="stylesheet" href="\/app.css"/);
    assert.match(html, /<script type="module" src="\/assets\/app\/actions\/public\/entry\.ts[^"]*"/);
  });

  it('GET /config.json returns the runtime client config, cached for an hour', async (t) => {
    // showErrorDetails is read from the environment per request.
    const previous = process.env.SHOW_ERROR_DETAILS;
    process.env.SHOW_ERROR_DETAILS = 'true';
    t.after(() => {
      if (previous === undefined) delete process.env.SHOW_ERROR_DETAILS;
      else process.env.SHOW_ERROR_DETAILS = previous;
    });

    const response = await get(routes.config.href());

    assert.equal(response.status, 200);
    assert.equal(response.headers.get('Cache-Control'), 'public, max-age=3600');
    assert.match(response.headers.get('Content-Type') ?? '', /application\/json/);
    assert.deepEqual(await response.json(), { apiUrl: 'http://localhost:7130', showErrorDetails: true });
  });

  it('serves files from public/', async () => {
    const response = await get('/favicon.ico');

    assert.equal(response.status, 200);
  });

  it('returns 404 for unknown paths', async () => {
    const response = await get('/no-such-page/at-all');

    assert.equal(response.status, 404);
  });
});

describe('dev API proxy', () => {
  it('strips /dev-api and forwards to the API; other requests reach the app', async (t) => {
    const apiFetch = t.mock.fn(async (input: RequestInfo | URL) => {
      const request = input instanceof Request ? input : new Request(input);
      return Response.json({ url: request.url });
    });
    const handler = withDevApiProxy(router.fetch, 'http://127.0.0.1:5000', { fetch: apiFetch as typeof fetch });

    const proxied = await handler(new Request('https://192.168.1.20:3000/dev-api/static/images/books/1.jpg?w=1'));
    const root = await handler(new Request('http://localhost:3000/dev-api'));
    const app = await handler(new Request('http://localhost:3000/config.json'));

    assert.deepEqual(await proxied.json(), { url: 'http://127.0.0.1:5000/static/images/books/1.jpg?w=1' });
    assert.deepEqual(await root.json(), { url: 'http://127.0.0.1:5000/' });
    assert.equal(apiFetch.mock.calls.length, 2);
    assert.equal(app.headers.get('Cache-Control'), 'public, max-age=3600');
  });

  it('does not treat paths that only start with "dev-api" as API requests', async (t) => {
    const apiFetch = t.mock.fn(async () => Response.json({}));
    const handler = withDevApiProxy(router.fetch, 'http://127.0.0.1:5000', { fetch: apiFetch as typeof fetch });

    const response = await handler(new Request('http://localhost:3000/dev-apis'));

    assert.equal(response.status, 404);
    assert.equal(apiFetch.mock.calls.length, 0);
  });
});
