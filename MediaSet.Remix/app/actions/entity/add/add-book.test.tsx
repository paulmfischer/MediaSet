import * as assert from 'remix/assert';
import { describe, it } from 'remix/test';

import { router } from '../../../router.ts';
import { routes } from '../../../routes.ts';
import { optionalWholeNumber, fieldErrors, submittedValues } from '../../forms/form-schema.ts';
import * as s from 'remix/data-schema';

const metadata: Record<string, string[]> = {
  authors: ['Frank Herbert'],
  genres: ['Science Fiction'],
  publisher: ['Ace'],
  format: ['Hardcover'],
};

// Stubs the API: metadata lookups return fixed values, POST /Books is recorded (or fails).
function stubApi(
  t: { after: (fn: () => void) => void },
  options: { failSave?: boolean; noId?: boolean; rejectSave?: string; failMetadata?: boolean } = {}
) {
  const original = globalThis.fetch;
  const saved: unknown[] = [];
  globalThis.fetch = (async (input: RequestInfo | URL, init?: RequestInit) => {
    const request = new Request(input, init);
    const url = new URL(request.url);
    if (request.method === 'POST' && url.pathname.endsWith('/Books')) {
      if (options.failSave) return new Response('boom', { status: 500 });
      if (options.rejectSave) return new Response(options.rejectSave, { status: 400 });
      saved.push(await request.json());
      return Response.json(options.noId ? {} : { id: 'new-id' }, { status: 201 });
    }
    if (options.failMetadata) return new Response('down', { status: 503 });
    const property = url.pathname.split('/').pop() ?? '';
    return Response.json(metadata[property] ?? []);
  }) as typeof fetch;
  t.after(() => {
    globalThis.fetch = original;
  });
  return saved;
}

const addUrl = routes.entityAdd.books.index.href();
const get = () => router.fetch(new URL(addUrl, 'http://localhost'));
const post = (fields: Array<[string, string]>) =>
  router.fetch(
    new Request(new URL(addUrl, 'http://localhost'), {
      method: 'POST',
      headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
      body: new URLSearchParams(fields),
    })
  );

describe('add book form', () => {
  it('GET serves the form, posting to the same URL', async (t) => {
    stubApi(t);
    const response = await get();

    assert.equal(response.status, 200);
    const html = await response.text();
    assert.match(html, /<title>Add Book \| MediaSet<\/title>/);
    assert.match(html, /<form method="post" action="\/books\/add"/);
    for (const name of ['title', 'subtitle', 'isbn', 'pages', 'publicationDate', 'plot']) {
      assert.match(html, new RegExp(`name="${name}"`));
    }
    assert.match(html, /role="combobox"/);
    assert.doesNotMatch(html, /role="alert"/);
  });

  it('re-renders with 400, per-field errors and the submitted values preserved', async (t) => {
    const saved = stubApi(t);
    const response = await post([
      ['title', '   '],
      ['subtitle', 'Book One'],
      ['pages', 'lots'],
      ['format', 'Hardcover'],
      ['authors', 'Frank Herbert'],
      ['authors', 'Someone New'],
      ['genres', 'Science Fiction'],
      ['plot', 'Spice & worms'],
    ]);

    assert.equal(response.status, 400);
    const html = await response.text();
    assert.match(html, /role="alert"/);
    assert.match(html, /<p id="title-error"[^>]*>Title is required<\/p>/);
    assert.match(html, /<p id="pages-error"[^>]*>Pages must be a whole number from 0 to 999999<\/p>/);
    assert.match(html, /name="title"[^>]*aria-invalid[^>]*aria-describedby="title-error"/);
    // Submitted values come back, including repeated fields and an option that is not in the API list.
    assert.match(html, /name="subtitle"[^>]*value="Book One"/);
    assert.match(html, /name="pages"[^>]*value="lots"/);
    assert.match(html, /<input type="hidden" name="authors" value="Frank Herbert"/);
    assert.match(html, /<input type="hidden" name="authors" value="Someone New"/);
    assert.match(html, /<input type="hidden" name="format" value="Hardcover"/);
    assert.match(html, /Spice &amp; worms/);
    assert.equal(saved.length, 0);
  });

  it('saves repeated fields as lists, maps blank numerics to no value and redirects 303 to the new book', async (t) => {
    const saved = stubApi(t);
    const response = await post([
      ['title', '  Dune '],
      ['pages', ''],
      ['authors', 'Frank Herbert'],
      ['authors', ' Frank Herbert '],
      ['authors', ''],
      ['authors', 'Brian Herbert'],
      ['genres', 'Science Fiction'],
      ['format', 'Hardcover'],
      ['isbn', ''],
    ]);

    assert.equal(response.status, 303);
    assert.equal(response.headers.get('Location'), '/books/new-id');
    assert.deepEqual(saved, [
      {
        type: 'Books',
        title: 'Dune',
        format: 'Hardcover',
        authors: ['Frank Herbert', 'Brian Herbert'],
        genres: ['Science Fiction'],
      },
    ]);
  });

  it('sends a numeric page count as a number', async (t) => {
    const saved = stubApi(t);
    const response = await post([
      ['title', 'Dune'],
      ['pages', '412'],
    ]);

    assert.equal(response.status, 303);
    assert.equal((saved[0] as { pages: unknown }).pages, 412);
  });

  it('keeps the form and input and shows an error when the API rejects the save', async (t) => {
    stubApi(t, { failSave: true });
    const response = await post([
      ['title', 'Dune'],
      ['authors', 'Frank Herbert'],
    ]);

    assert.equal(response.status, 500);
    const html = await response.text();
    assert.match(html, /The book could not be saved/);
    assert.match(html, /name="title"[^>]*value="Dune"/);
    assert.match(html, /<input type="hidden" name="authors" value="Frank Herbert"/);
  });

  it('shows the API rejection message with a 400 when the API refuses the book', async (t) => {
    stubApi(t, { rejectSave: 'An entity with that ISBN already exists' });
    const response = await post([
      ['title', 'Dune'],
      ['isbn', '123'],
    ]);

    assert.equal(response.status, 400);
    const html = await response.text();
    assert.match(html, /role="alert"[^>]*>An entity with that ISBN already exists</);
    assert.match(html, /name="isbn"[^>]*value="123"/);
  });

  it('shows an error with the input kept, not a redirect, when the saved book has no id', async (t) => {
    stubApi(t, { noId: true });
    const response = await post([['title', 'Dune']]);

    assert.equal(response.status, 500);
    assert.equal(response.headers.get('Location'), null);
    const html = await response.text();
    assert.match(html, /role="alert"[^>]*>The book was saved, but it could not be opened/);
    assert.match(html, /name="title"[^>]*value="Dune"/);
  });

  it('warns that existing values are missing when the option lists fail to load', async (t) => {
    stubApi(t, { failMetadata: true });
    const html = await (await get()).text();

    assert.match(html, /role="status"[^>]*>Existing authors, genres, publishers and formats could not be loaded/);
    assert.match(html, /role="combobox"/);
  });
});

describe('form schema helpers', () => {
  const pages = optionalWholeNumber('bad');

  it('maps blank optional numerics to no value and rejects non-whole numbers', () => {
    assert.deepEqual(s.parseSafe(pages, ''), { success: true, value: undefined });
    assert.deepEqual(s.parseSafe(pages, '  '), { success: true, value: undefined });
    assert.deepEqual(s.parseSafe(pages, undefined), { success: true, value: undefined });
    assert.deepEqual(s.parseSafe(pages, '0'), { success: true, value: 0 });
    assert.deepEqual(s.parseSafe(pages, '12'), { success: true, value: 12 });
    for (const bad of ['1.5', '-3', 'abc', '1e3', '0x1F', '1.0', '1000000'])
      assert.equal(s.parseSafe(pages, bad).success, false);
  });

  it('reads the first issue per field and the submitted strings back', () => {
    assert.deepEqual(
      fieldErrors([
        { message: 'first', path: ['title'] },
        { message: 'second', path: ['title'] },
        { message: 'other', path: ['pages'] },
      ]),
      { title: 'first', pages: 'other' }
    );
    const formData = new FormData();
    formData.append('title', 'Dune');
    formData.append('authors', 'a');
    formData.append('authors', 'b');
    assert.deepEqual(submittedValues(formData, { single: ['title', 'plot'], repeated: ['authors'] }), {
      title: 'Dune',
      plot: '',
      authors: ['a', 'b'],
    });
  });
});
