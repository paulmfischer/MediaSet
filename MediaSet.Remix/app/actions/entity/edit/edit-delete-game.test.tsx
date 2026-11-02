import * as assert from 'remix/assert';
import { describe, it } from 'remix/test';

import { router } from '../../../router.ts';

const existing = {
  id: 'm1',
  type: 'Games',
  title: 'Zelda',
  format: 'Blu-ray',
  platform: 'Switch',
  rating: 'E10+',
  developers: ['Nintendo'],
  publishers: ['Nintendo'],
  genres: ['Adventure'],
  coverImage: { filePath: 'games/m1.jpg' },
};

// Stubs the API: GET /Games/m1 returns the game (or 404/500), PUT and DELETE are recorded or fail.
function stubApi(
  t: { after: (fn: () => void) => void },
  options: {
    missing?: boolean;
    loadFails?: boolean;
    saveStatus?: number;
    saveBody?: string;
    deleteStatus?: number;
  } = {}
) {
  const original = globalThis.fetch;
  const calls: Array<{ method: string; body: unknown }> = [];
  globalThis.fetch = (async (input: RequestInfo | URL, init?: RequestInit) => {
    const request = new Request(input, init);
    const url = new URL(request.url);
    if (url.pathname.endsWith('/Games/m1')) {
      if (request.method === 'PUT') {
        calls.push({ method: 'PUT', body: await request.json() });
        return new Response(options.saveBody ?? null, { status: options.saveStatus ?? 204 });
      }
      if (request.method === 'DELETE') {
        calls.push({ method: 'DELETE', body: null });
        return new Response(null, { status: options.deleteStatus ?? 204 });
      }
      if (options.missing) return new Response('nope', { status: 404 });
      if (options.loadFails) return new Response('down', { status: 503 });
      return Response.json(existing);
    }
    return Response.json([]);
  }) as typeof fetch;
  t.after(() => {
    globalThis.fetch = original;
  });
  return calls;
}

const url = (path: string) => new URL(path, 'http://localhost');
const post = (path: string, fields: Array<[string, string]> = []) =>
  router.fetch(
    new Request(url(path), {
      method: 'POST',
      headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
      body: new URLSearchParams(fields),
    })
  );

describe('edit game', () => {
  it('GET prefills the form from the existing game', async (t) => {
    stubApi(t);
    const response = await router.fetch(url('/games/m1/edit'));

    assert.equal(response.status, 200);
    const html = await response.text();
    assert.match(html, /<title>Edit Zelda \| MediaSet<\/title>/);
    assert.match(html, /<form method="post" action="\/games\/m1\/edit"/);
    assert.match(html, /name="title"[^>]*value="Zelda"/);
    assert.match(html, /name="rating"[^>]*value="E10\+"/);
    assert.match(html, /<input type="hidden" name="developers" value="Nintendo"/);
  });

  it('returns 404 for a missing game and 500 when it cannot be loaded', async (t) => {
    const calls = stubApi(t, { missing: true });
    assert.equal((await router.fetch(url('/games/m1/edit'))).status, 404);
    assert.equal((await post('/games/m1/edit', [['title', 'Zelda']])).status, 404);
    assert.equal(calls.length, 0);

    stubApi(t, { loadFails: true });
    assert.equal((await router.fetch(url('/games/m1/edit'))).status, 500);
  });

  it('re-renders with 400, errors and the submitted values when validation fails', async (t) => {
    const calls = stubApi(t);
    const response = await post('/games/m1/edit', [
      ['title', ' '],
      ['imageUrl', 'ftp://example.com/x.jpg'],
      ['releaseDate', '2017'],
    ]);

    assert.equal(response.status, 400);
    const html = await response.text();
    assert.match(html, /<p id="title-error"[^>]*>Title is required<\/p>/);
    assert.match(html, /<p id="imageUrl-error"/);
    assert.match(html, /name="releaseDate"[^>]*value="2017"/);
    assert.equal(calls.length, 0);
  });

  it('saves over the existing game, dropping cleared fields and keeping the image, then redirects 303', async (t) => {
    const calls = stubApi(t);
    const response = await post('/games/m1/edit', [
      ['title', 'Zeldas'],
      ['rating', ''],
      ['format', 'Cartridge'],
      ['developers', 'Nintendo'],
    ]);

    assert.equal(response.status, 303);
    assert.equal(response.headers.get('Location'), '/games/m1');
    assert.deepEqual(calls, [
      {
        method: 'PUT',
        body: {
          id: 'm1',
          type: 'Games',
          title: 'Zeldas',
          format: 'Cartridge',
          developers: ['Nintendo'],
          publishers: [],
          genres: [],
          coverImage: { filePath: 'games/m1.jpg' },
        },
      },
    ]);
  });

  it('shows the API rejection with a 400, and a generic error with a 500', async (t) => {
    stubApi(t, { saveStatus: 400, saveBody: 'Barcode already exists' });
    const rejected = await post('/games/m1/edit', [['title', 'Zelda']]);
    assert.equal(rejected.status, 400);
    assert.match(await rejected.text(), /role="alert"[^>]*>Barcode already exists</);

    stubApi(t, { saveStatus: 500 });
    const failed = await post('/games/m1/edit', [['title', 'Zelda']]);
    assert.equal(failed.status, 500);
    const html = await failed.text();
    assert.match(html, /The game could not be saved/);
    assert.match(html, /name="title"[^>]*value="Zelda"/);
  });
});

describe('delete game', () => {
  it('GET shows a confirmation form posting to the delete URL', async (t) => {
    stubApi(t);
    const response = await router.fetch(url('/games/m1/delete'));

    assert.equal(response.status, 200);
    const html = await response.text();
    assert.match(html, /<form method="post" action="\/games\/m1\/delete"/);
    assert.match(html, /<strong>Zelda<\/strong>/);
  });

  it('returns 404 for a missing game on GET and POST', async (t) => {
    stubApi(t, { missing: true });
    assert.equal((await router.fetch(url('/games/m1/delete'))).status, 404);

    stubApi(t, { deleteStatus: 404 });
    assert.equal((await post('/games/m1/delete')).status, 404);
  });

  it('deletes through the API and redirects 303 to the list', async (t) => {
    const calls = stubApi(t);
    const response = await post('/games/m1/delete');

    assert.equal(response.status, 303);
    assert.equal(response.headers.get('Location'), '/games');
    assert.deepEqual(calls, [{ method: 'DELETE', body: null }]);
  });

  it('keeps the confirmation and shows an error when the API fails', async (t) => {
    stubApi(t, { deleteStatus: 500 });
    const response = await post('/games/m1/delete');

    assert.equal(response.status, 500);
    assert.match(await response.text(), /role="alert"[^>]*>The game could not be deleted/);
  });
});
