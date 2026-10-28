import * as assert from 'remix/assert';
import { describe, it } from 'remix/test';

import * as fs from 'node:fs';
import * as os from 'node:os';
import * as path from 'node:path';

import { router } from './router.ts';
import { routes } from './routes.ts';
import { createRequestHandler, readHttpsOptions, readPort } from './server-config.ts';

const get = (path: string) => router.fetch(new URL(path, 'http://localhost'));

describe('router', () => {
  it('GET / renders the home page document with the prebuilt stylesheet and browser entry', async () => {
    const response = await get(routes.home.href());

    assert.equal(response.status, 200);
    assert.match(response.headers.get('Content-Type') ?? '', /text\/html/);
    const html = await response.text();
    assert.match(html, /<html lang="en">/);
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

// Echoes what reached the API so tests can assert on the forwarded request.
async function echoApi(input: RequestInfo | URL, init?: RequestInit) {
  const request = new Request(input, init);
  return Response.json({
    url: request.url,
    method: request.method,
    contentType: request.headers.get('Content-Type'),
    body: await request.text(),
  });
}

const devEnv = { NODE_ENV: 'development' };

describe('dev API proxy', () => {
  it('strips /dev-api and forwards to the API; other requests reach the app', async (t) => {
    const apiFetch = t.mock.fn(echoApi);
    const handler = createRequestHandler(router.fetch, devEnv, { fetch: apiFetch as typeof fetch });

    const proxied = await handler(new Request('https://192.168.1.20:3000/dev-api/static/images/books/1.jpg?w=1'));
    const root = await handler(new Request('http://localhost:3000/dev-api'));
    const app = await handler(new Request('http://localhost:3000/config.json'));

    assert.equal((await proxied.json()).url, 'http://127.0.0.1:5000/static/images/books/1.jpg?w=1');
    assert.equal((await root.json()).url, 'http://127.0.0.1:5000/');
    assert.equal(apiFetch.mock.calls.length, 2);
    assert.equal(app.headers.get('Cache-Control'), 'public, max-age=3600');
  });

  it('forwards the method, content type and body of POST and PUT requests', async (t) => {
    const handler = createRequestHandler(
      router.fetch,
      { ...devEnv, apiUrl: 'http://api.test:8080' },
      { fetch: t.mock.fn(echoApi) as typeof fetch }
    );
    const upload = new FormData();
    upload.set('image', new File(['fake-jpeg'], 'cover.jpg', { type: 'image/jpeg' }));

    const post = await (
      await handler(new Request('http://localhost:3000/dev-api/books', { method: 'POST', body: upload }))
    ).json();
    const put = await (
      await handler(
        new Request('http://localhost:3000/dev-api/books/42', {
          method: 'PUT',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({ title: 'Dune' }),
        })
      )
    ).json();

    assert.equal(post.url, 'http://api.test:8080/books');
    assert.equal(post.method, 'POST');
    assert.match(post.contentType, /^multipart\/form-data; boundary=/);
    assert.match(post.body, /filename="cover.jpg"[\s\S]*fake-jpeg/);
    assert.deepEqual(put, {
      url: 'http://api.test:8080/books/42',
      method: 'PUT',
      contentType: 'application/json',
      body: '{"title":"Dune"}',
    });
  });

  it('does not treat paths that only start with "dev-api" as API requests', async (t) => {
    const apiFetch = t.mock.fn(echoApi);
    const handler = createRequestHandler(router.fetch, devEnv, { fetch: apiFetch as typeof fetch });

    const response = await handler(new Request('http://localhost:3000/dev-apis'));

    assert.equal(response.status, 404);
    assert.equal(apiFetch.mock.calls.length, 0);
  });

  it('is not installed in production, so /dev-api is a 404', async (t) => {
    const apiFetch = t.mock.fn(echoApi);
    const handler = createRequestHandler(router.fetch, { NODE_ENV: 'production' }, { fetch: apiFetch as typeof fetch });

    const response = await handler(new Request('http://localhost:3000/dev-api/health'));

    assert.equal(response.status, 404);
    assert.equal(apiFetch.mock.calls.length, 0);
  });
});

describe('server config', () => {
  it('defaults the port to 3000 and rejects values that are not a valid port', () => {
    assert.equal(readPort({}), 3000);
    assert.equal(readPort({ PORT: '' }), 3000);
    assert.equal(readPort({ PORT: '3005' }), 3005);
    for (const PORT of ['abc', '30x0', '3.5', '0', '70000', '-1']) {
      assert.throws(() => readPort({ PORT }), /PORT must be an integer between 1 and 65535/);
    }
  });

  it('serves HTTPS only in mobile mode outside production, and requires both certificate settings', (t) => {
    const dir = fs.mkdtempSync(path.join(os.tmpdir(), 'mediaset-certs-'));
    t.after(() => fs.rmSync(dir, { recursive: true, force: true }));
    const DEV_HTTPS_CERT = path.join(dir, 'dev.pem');
    const DEV_HTTPS_KEY = path.join(dir, 'dev-key.pem');
    fs.writeFileSync(DEV_HTTPS_CERT, 'cert');
    fs.writeFileSync(DEV_HTTPS_KEY, 'key');
    const certs = { DEV_HTTPS_CERT, DEV_HTTPS_KEY };

    assert.equal(readHttpsOptions({ ...devEnv, ...certs }), null);
    assert.equal(readHttpsOptions({ NODE_ENV: 'production', MEDIASET_DEV_HTTPS: '1', ...certs }), null);
    assert.throws(
      () => readHttpsOptions({ ...devEnv, MEDIASET_DEV_HTTPS: '1', DEV_HTTPS_CERT }),
      /Mobile HTTPS requires DEV_HTTPS_CERT and DEV_HTTPS_KEY/
    );
    const options = readHttpsOptions({ ...devEnv, MEDIASET_DEV_HTTPS: '1', ...certs });
    assert.equal(String(options?.cert), 'cert');
    assert.equal(String(options?.key), 'key');
  });
});
