import * as assert from 'remix/assert';
import { describe, it } from 'remix/test';

import { router } from '../../../router.ts';

const existing = {
  id: 'm1',
  type: 'Musics',
  title: 'Abbey Road',
  artist: 'The Beatles',
  format: 'Vinyl',
  label: 'Apple',
  tracks: 17,
  discs: 1,
  duration: 2834500,
  discList: [{ trackNumber: 1, title: 'Come Together', duration: 259000 }],
  genres: ['Rock'],
  coverImage: { filePath: 'musics/m1.jpg' },
};

// Stubs the API: GET /Musics/m1 returns the album (or 404/500), PUT and DELETE are recorded or fail.
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
    if (url.pathname.endsWith('/Musics/m1')) {
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

describe('edit album', () => {
  it('GET prefills the form from the existing album', async (t) => {
    stubApi(t);
    const response = await router.fetch(url('/musics/m1/edit'));

    assert.equal(response.status, 200);
    const html = await response.text();
    assert.match(html, /<title>Edit Abbey Road \| MediaSet<\/title>/);
    assert.match(html, /<form method="post" action="\/musics\/m1\/edit"/);
    assert.match(html, /name="title"[^>]*value="Abbey Road"/);
    assert.match(html, /name="tracks"[^>]*value="17"/);
    assert.match(html, /name="durationMinutes"[^>]*value="47"/);
    assert.match(html, /name="durationSeconds"[^>]*value="14"/);
    assert.match(html, /<input type="hidden" name="genres" value="Rock"/);
  });

  it('returns 404 for a missing album and 500 when it cannot be loaded', async (t) => {
    const calls = stubApi(t, { missing: true });
    assert.equal((await router.fetch(url('/musics/m1/edit'))).status, 404);
    assert.equal(
      (
        await post('/musics/m1/edit', [
          ['title', 'Abbey Road'],
          ['artist', 'The Beatles'],
        ])
      ).status,
      404
    );
    assert.equal(calls.length, 0);

    stubApi(t, { loadFails: true });
    assert.equal((await router.fetch(url('/musics/m1/edit'))).status, 500);
  });

  it('re-renders with 400, errors and the submitted values when validation fails', async (t) => {
    const calls = stubApi(t);
    const response = await post('/musics/m1/edit', [
      ['title', ' '],
      ['artist', ''],
      ['imageUrl', 'ftp://example.com/x.jpg'],
      ['durationSeconds', '75'],
      ['releaseDate', '1969'],
    ]);

    assert.equal(response.status, 400);
    const html = await response.text();
    assert.match(html, /<p id="title-error"[^>]*>Title is required<\/p>/);
    assert.match(html, /<p id="artist-error"[^>]*>Artist is required<\/p>/);
    assert.match(html, /<p id="imageUrl-error"/);
    assert.match(html, /<p id="durationSeconds-error"/);
    assert.match(html, /name="releaseDate"[^>]*value="1969"/);
    assert.equal(calls.length, 0);
  });

  it('saves over the existing album, dropping cleared fields and keeping the image, then redirects 303', async (t) => {
    const calls = stubApi(t);
    const response = await post('/musics/m1/edit', [
      ['title', 'Abbey Roads'],
      ['artist', 'The Beatles'],
      ['label', ''],
      ['tracks', ''],
      ['format', 'CD'],
      ['discs', '2'],
      ['durationMinutes', '47'],
      ['durationSeconds', '14'],
      ['genres', 'Rock'],
      ['genres', 'Pop'],
    ]);

    assert.equal(response.status, 303);
    assert.equal(response.headers.get('Location'), '/musics/m1');
    assert.deepEqual(calls, [
      {
        method: 'PUT',
        body: {
          id: 'm1',
          type: 'Musics',
          title: 'Abbey Roads',
          artist: 'The Beatles',
          format: 'CD',
          discs: 2,
          // Unchanged whole seconds keep the saved milliseconds.
          duration: 2834500,
          // The track list is not editable and is kept as saved.
          discList: [{ trackNumber: 1, title: 'Come Together', duration: 259000 }],
          genres: ['Rock', 'Pop'],
          coverImage: { filePath: 'musics/m1.jpg' },
        },
      },
    ]);
  });

  it('changes or clears the duration, and ignores a submitted track list', async (t) => {
    const calls = stubApi(t);
    await post('/musics/m1/edit', [
      ['title', 'Abbey Road'],
      ['artist', 'The Beatles'],
      ['durationMinutes', '1'],
      ['durationSeconds', '2'],
      ['discList', '[]'],
    ]);
    await post('/musics/m1/edit', [
      ['title', 'Abbey Road'],
      ['artist', 'The Beatles'],
    ]);

    const bodies = calls.map((call) => call.body as Record<string, unknown>);
    assert.equal(bodies[0]?.duration, 62000);
    assert.deepEqual(bodies[0]?.discList, [{ trackNumber: 1, title: 'Come Together', duration: 259000 }]);
    assert.equal('duration' in (bodies[1] ?? {}), false);
  });

  it('shows the API rejection with a 400, and a generic error with a 500', async (t) => {
    stubApi(t, { saveStatus: 400, saveBody: 'Barcode already exists' });
    const rejected = await post('/musics/m1/edit', [
      ['title', 'Abbey Road'],
      ['artist', 'The Beatles'],
    ]);
    assert.equal(rejected.status, 400);
    assert.match(await rejected.text(), /role="alert"[^>]*>Barcode already exists</);

    stubApi(t, { saveStatus: 500 });
    const failed = await post('/musics/m1/edit', [
      ['title', 'Abbey Road'],
      ['artist', 'The Beatles'],
    ]);
    assert.equal(failed.status, 500);
    const html = await failed.text();
    assert.match(html, /The album could not be saved/);
    assert.match(html, /name="title"[^>]*value="Abbey Road"/);
  });
});

describe('delete album', () => {
  it('GET shows a confirmation form posting to the delete URL', async (t) => {
    stubApi(t);
    const response = await router.fetch(url('/musics/m1/delete'));

    assert.equal(response.status, 200);
    const html = await response.text();
    assert.match(html, /<form method="post" action="\/musics\/m1\/delete"/);
    assert.match(html, /<strong>Abbey Road<\/strong>/);
  });

  it('returns 404 for a missing album on GET and POST', async (t) => {
    stubApi(t, { missing: true });
    assert.equal((await router.fetch(url('/musics/m1/delete'))).status, 404);

    stubApi(t, { deleteStatus: 404 });
    assert.equal((await post('/musics/m1/delete')).status, 404);
  });

  it('deletes through the API and redirects 303 to the list', async (t) => {
    const calls = stubApi(t);
    const response = await post('/musics/m1/delete');

    assert.equal(response.status, 303);
    assert.equal(response.headers.get('Location'), '/musics');
    assert.deepEqual(calls, [{ method: 'DELETE', body: null }]);
  });

  it('keeps the confirmation and shows an error when the API fails', async (t) => {
    stubApi(t, { deleteStatus: 500 });
    const response = await post('/musics/m1/delete');

    assert.equal(response.status, 500);
    assert.match(await response.text(), /role="alert"[^>]*>The album could not be deleted/);
  });
});
