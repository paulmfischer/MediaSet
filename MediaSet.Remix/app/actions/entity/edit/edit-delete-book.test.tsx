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

// A lookup result: it lacks some fields the saved item has.
const result = {
  title: 'Dune Messiah',
  authors: [{ name: 'Frank Herbert', url: '' }],
  numberOfPages: 331,
  publishDate: '1969',
  imageUrl: 'https://example.com/messiah.jpg',
};

// Stubs the API: GET /Books/b1 returns the book (or 404/500), PUT and DELETE are recorded or fail, plus lookup capabilities and results.
function stubApi(
  t: { after: (fn: () => void) => void },
  options: {
    missing?: boolean;
    loadFails?: boolean;
    saveStatus?: number;
    saveBody?: string;
    deleteStatus?: number;
    /** Whether the provider supports lookup; defaults to true. */
    supported?: boolean;
    lookup?: () => Response;
    /** Collects each lookup request's path and query. */
    lookups?: string[];
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
    if (url.pathname.endsWith('/config/lookup-capabilities')) {
      return Response.json({ supportsBookLookup: options.supported ?? true });
    }
    if (url.pathname.includes('/lookup/Books/')) {
      options.lookups?.push(url.pathname + url.search);
      return (options.lookup ?? (() => Response.json([result])))();
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

describe('edit book metadata lookup', () => {
  const editUrl = '/books/b1/edit';
  const savedImage = 'books/b1.jpg';

  // The results come back in a hidden field; read them out the way the browser would send them.
  const resultsOf = (html: string) =>
    /name="lookupResults" value="([^"]*)"/.exec(html)?.[1]?.replaceAll('&quot;', '"').replaceAll('&amp;', '&') ?? '[]';

  it('shows Update before a Cancel button', async (t) => {
    stubApi(t);
    const html = await (await router.fetch(url(editUrl))).text();

    const update = html.indexOf('>Update</button>');
    const cancel = html.search(/<button[^>]*class="secondary"[^>]*>Cancel<\/button>/);
    assert.ok(update !== -1 && cancel !== -1 && update < cancel);
  });

  it('offers the lookup buttons only when the provider supports book lookup', async (t) => {
    stubApi(t);
    assert.match(await (await router.fetch(url(editUrl))).text(), /value="lookup:title"[\s\S]*value="lookup:isbn"/);

    stubApi(t, { supported: false });
    assert.doesNotMatch(await (await router.fetch(url(editUrl))).text(), /value="lookup:/);
  });

  it('prefills the lookup Author with the first author of the saved book', async (t) => {
    stubApi(t);
    const html = await (await router.fetch(url(editUrl))).text();

    assert.match(html, /name="lookupAuthor"[^>]*value="Frank Herbert"/);
  });

  it('lists the matches, keeping the entered values and the saved image, without saving', async (t) => {
    const lookups: string[] = [];
    const calls = stubApi(t, { lookups });
    const response = await post(editUrl, [
      ['intent', 'lookup:title'],
      ['title', ' Dune '],
      ['lookupAuthor', ' Frank Herbert '],
      ['authors', 'Frank Herbert'],
      ['subtitle', 'Typed subtitle'],
    ]);

    assert.equal(response.status, 200);
    assert.deepEqual(lookups, ['/lookup/Books/entity?title=Dune&author=Frank+Herbert']);
    const html = await response.text();
    assert.match(html, /<dialog open/);
    assert.match(html, /Dune Messiah/);
    assert.match(html, /name="intent" value="apply:0"/);
    assert.match(html, /name="title"[^>]*value=" ?Dune ?"/);
    assert.ok(html.includes(savedImage), 'the saved image is still shown');
    assert.deepEqual(calls, []);
  });

  it('looks up by ISBN too', async (t) => {
    const lookups: string[] = [];
    stubApi(t, { lookups });
    const response = await post(editUrl, [
      ['intent', 'lookup:isbn'],
      ['isbn', '9780441172719'],
    ]);

    assert.equal(response.status, 200);
    assert.deepEqual(lookups, ['/lookup/Books/isbn/9780441172719']);
    const html = await response.text();
    // One match for an ISBN or barcode fills the form at once; no dialog to choose from.
    assert.doesNotMatch(html, /<dialog open/);
    assert.match(html, /data-rmx-key="/);
  });

  it('asks for a value, with a 400, when the search field is blank', async (t) => {
    const lookups: string[] = [];
    stubApi(t, { lookups });
    const response = await post(editUrl, [
      ['intent', 'lookup:isbn'],
      ['isbn', ' '],
    ]);

    assert.equal(response.status, 400);
    assert.match(await response.text(), /Enter an ISBN to look up/);
    assert.deepEqual(lookups, []);
  });

  it('shows not found, unavailable and error states in the dialog', async (t) => {
    const search: Array<[string, string]> = [
      ['intent', 'lookup:title'],
      ['title', 'Dune'],
    ];
    stubApi(t, { lookup: () => Response.json([]) });
    assert.match(await (await post(editUrl, search)).text(), /<dialog open[\s\S]*No matches were found/);

    stubApi(t, { lookup: () => new Response('none', { status: 404 }) });
    assert.match(await (await post(editUrl, search)).text(), /No matches were found/);

    const lookups: string[] = [];
    stubApi(t, { supported: false, lookups });
    assert.match(await (await post(editUrl, search)).text(), /Metadata lookup is not available/);
    assert.deepEqual(lookups, []);

    stubApi(t, { lookup: () => new Response('Unsupported search', { status: 400 }) });
    assert.match(await (await post(editUrl, search)).text(), /role="alert"[^>]*>Unsupported search</);

    stubApi(t, { lookup: () => new Response('stack trace', { status: 500 }) });
    const failed = await (await post(editUrl, search)).text();
    assert.match(failed, /role="alert"[^>]*>The lookup failed\. Please try again\./);
    assert.doesNotMatch(failed, /stack trace/);

    stubApi(t, {
      lookup: () => {
        throw new TypeError('fetch failed');
      },
    });
    assert.match(await (await post(editUrl, search)).text(), /The lookup could not be reached/);
  });

  it('applies a result to the form without saving, keeping fields it lacks and the saved image', async (t) => {
    const calls = stubApi(t);
    const found = await (
      await post(editUrl, [
        ['intent', 'lookup:title'],
        ['title', 'Dune'],
      ])
    ).text();

    const response = await post(editUrl, [
      ['intent', 'apply:0'],
      ['lookupResults', resultsOf(found)],
      ['title', 'Dune'],
      ['subtitle', 'Book One'],
    ]);

    assert.equal(response.status, 200);
    const html = await response.text();
    assert.doesNotMatch(html, /<dialog open/);
    assert.match(html, /name="title"[^>]*value="Dune Messiah"/);
    assert.match(html, /name="pages"[^>]*value="331"/);
    assert.match(html, /name="publicationDate"[^>]*value="1969"/);
    // A field the result lacks keeps its current value.
    assert.match(html, /name="subtitle"[^>]*value="Book One"/);
    assert.ok(html.includes(savedImage), 'the saved image is still shown');
    assert.match(html, /name="removeImage"/);
    // Nothing is saved until the user saves.
    assert.deepEqual(calls, []);
  });

  it('keeps a ticked remove-image box through a lookup and an apply, and leaves it unticked otherwise', async (t) => {
    const calls = stubApi(t);
    const search: Array<[string, string]> = [
      ['intent', 'lookup:title'],
      ['title', 'Dune'],
    ];
    const ticked = await (await post(editUrl, [...search, ['removeImage', 'on']])).text();
    assert.match(ticked, /name="removeImage"[^>]*checked/);

    const applied = await (
      await post(editUrl, [
        ['intent', 'apply:0'],
        ['lookupResults', resultsOf(ticked)],
        ['title', 'Dune'],
        ['removeImage', 'on'],
      ])
    ).text();
    assert.match(applied, /name="removeImage"[^>]*checked/);

    const unticked = await (await post(editUrl, search)).text();
    assert.doesNotMatch(unticked, /name="removeImage"[^>]*checked/);
    assert.deepEqual(calls, []);
  });

  it('sends an applied image URL on Save, replacing the saved image unless remove is ticked', async (t) => {
    const calls = stubApi(t);
    const found = await (
      await post(editUrl, [
        ['intent', 'lookup:title'],
        ['title', 'Dune'],
      ])
    ).text();
    const applied = await (
      await post(editUrl, [
        ['intent', 'apply:0'],
        ['lookupResults', resultsOf(found)],
        ['title', 'Dune'],
      ])
    ).text();
    assert.match(applied, /name="imageUrl"[^>]*value="https:\/\/example.com\/messiah.jpg"/);
    assert.equal(calls.length, 0);

    // Saving with the applied URL sends it with the saved image still referenced, so the API replaces it.
    await post(editUrl, [
      ['title', 'Dune Messiah'],
      ['imageUrl', 'https://example.com/messiah.jpg'],
    ]);
    const body = calls[0]?.body as Record<string, unknown>;
    assert.equal(body.imageUrl, 'https://example.com/messiah.jpg');
    assert.deepEqual(body.coverImage, { filePath: 'books/b1.jpg' });

    // With remove ticked the saved image is dropped and the URL still downloads.
    await post(editUrl, [
      ['title', 'Dune Messiah'],
      ['imageUrl', 'https://example.com/messiah.jpg'],
      ['removeImage', 'on'],
    ]);
    const removed = calls[1]?.body as Record<string, unknown>;
    assert.equal(removed.imageUrl, 'https://example.com/messiah.jpg');
    assert.equal(removed.coverImage, undefined);
  });

  it('closes the dialog without saving', async (t) => {
    const calls = stubApi(t);
    const response = await post(editUrl, [
      ['intent', 'dismiss'],
      ['title', 'Dune'],
    ]);

    assert.equal(response.status, 200);
    assert.doesNotMatch(await response.text(), /<dialog open/);
    assert.deepEqual(calls, []);
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
