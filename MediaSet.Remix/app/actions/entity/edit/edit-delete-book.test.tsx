import * as assert from 'remix/assert';
import { describe, it } from 'remix/test';

import { router } from '../../../router.ts';

const existing = {
  id: 'b1',
  type: 'Books',
  title: 'Dune',
  subtitle: 'Book One',
  format: 'Hardcover',
  pages: 412,
  publisher: 'Ace',
  authors: ['Frank Herbert'],
  genres: ['Science Fiction'],
  coverImage: { filePath: 'books/b1.jpg' },
};

// Stubs the API: GET /Books/b1 returns the book (or 404/500), PUT and DELETE are recorded or fail.
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
    if (url.pathname.endsWith('/Books/b1')) {
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

describe('edit book', () => {
  it('GET prefills the form from the existing book', async (t) => {
    stubApi(t);
    const response = await router.fetch(url('/books/b1/edit'));

    assert.equal(response.status, 200);
    const html = await response.text();
    assert.match(html, /<title>Edit Dune \| MediaSet<\/title>/);
    assert.match(html, /<form method="post" action="\/books\/b1\/edit"/);
    assert.match(html, /name="title"[^>]*value="Dune"/);
    assert.match(html, /name="pages"[^>]*value="412"/);
    assert.match(html, /<input type="hidden" name="authors" value="Frank Herbert"/);
  });

  it('returns 404 for a missing book on GET and POST', async (t) => {
    const calls = stubApi(t, { missing: true });

    assert.equal((await router.fetch(url('/books/b1/edit'))).status, 404);
    assert.equal((await post('/books/b1/edit', [['title', 'Dune']])).status, 404);
    assert.equal(calls.length, 0);
  });

  it('returns 500 when the book cannot be loaded', async (t) => {
    stubApi(t, { loadFails: true });
    assert.equal((await router.fetch(url('/books/b1/edit'))).status, 500);
  });

  it('re-renders with 400, errors and the submitted values when validation fails', async (t) => {
    const calls = stubApi(t);
    const response = await post('/books/b1/edit', [
      ['title', ' '],
      ['subtitle', 'Changed'],
      ['pages', 'lots'],
    ]);

    assert.equal(response.status, 400);
    const html = await response.text();
    assert.match(html, /<p id="title-error"[^>]*>Title is required<\/p>/);
    assert.match(html, /<p id="pages-error"/);
    assert.match(html, /name="subtitle"[^>]*value="Changed"/);
    assert.equal(calls.length, 0);
  });

  it('saves over the existing book, keeping unedited fields and dropping cleared ones, then redirects 303', async (t) => {
    const calls = stubApi(t);
    const response = await post('/books/b1/edit', [
      ['title', 'Dune Messiah'],
      ['subtitle', ''],
      ['pages', ''],
      ['format', 'Hardcover'],
      ['authors', 'Frank Herbert'],
    ]);

    assert.equal(response.status, 303);
    assert.equal(response.headers.get('Location'), '/books/b1');
    assert.deepEqual(calls, [
      {
        method: 'PUT',
        body: {
          id: 'b1',
          type: 'Books',
          title: 'Dune Messiah',
          format: 'Hardcover',
          authors: ['Frank Herbert'],
          genres: [],
          coverImage: { filePath: 'books/b1.jpg' },
        },
      },
    ]);
  });

  it('shows the API rejection with a 400, and a generic error with a 500', async (t) => {
    stubApi(t, { saveStatus: 400, saveBody: 'ISBN already exists' });
    const rejected = await post('/books/b1/edit', [['title', 'Dune']]);
    assert.equal(rejected.status, 400);
    assert.match(await rejected.text(), /role="alert"[^>]*>ISBN already exists</);

    stubApi(t, { saveStatus: 500 });
    const failed = await post('/books/b1/edit', [['title', 'Dune']]);
    assert.equal(failed.status, 500);
    const html = await failed.text();
    assert.match(html, /The book could not be saved/);
    assert.match(html, /name="title"[^>]*value="Dune"/);
  });
});

describe('delete book', () => {
  it('GET shows a confirmation form posting to the delete URL', async (t) => {
    stubApi(t);
    const response = await router.fetch(url('/books/b1/delete'));

    assert.equal(response.status, 200);
    const html = await response.text();
    assert.match(html, /<form method="post" action="\/books\/b1\/delete"/);
    assert.match(html, /<strong>Dune<\/strong>/);
  });

  it('returns 404 for a missing book on GET and POST', async (t) => {
    stubApi(t, { missing: true });
    assert.equal((await router.fetch(url('/books/b1/delete'))).status, 404);

    stubApi(t, { deleteStatus: 404 });
    assert.equal((await post('/books/b1/delete')).status, 404);
  });

  it('deletes through the API and redirects 303 to the list', async (t) => {
    const calls = stubApi(t);
    const response = await post('/books/b1/delete');

    assert.equal(response.status, 303);
    assert.equal(response.headers.get('Location'), '/books');
    assert.deepEqual(calls, [{ method: 'DELETE', body: null }]);
  });

  it('keeps the confirmation and shows an error when the API fails', async (t) => {
    stubApi(t, { deleteStatus: 500 });
    const response = await post('/books/b1/delete');

    assert.equal(response.status, 500);
    assert.match(await response.text(), /role="alert"[^>]*>The book could not be deleted/);
  });
});
