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

const dune = {
  title: 'Dune',
  subtitle: 'Book One',
  authors: [{ name: 'Frank Herbert', url: '' }],
  numberOfPages: 412,
  publishers: [{ name: 'Ace' }],
  publishDate: '1990',
  subjects: [{ name: 'Science Fiction', url: '' }],
  format: 'Paperback',
  imageUrl: 'https://example.com/dune.jpg',
};

// Stubs the API for lookups: capabilities, option lists and the lookup endpoint (which is recorded).
function stubLookupApi(
  t: { after: (fn: () => void) => void },
  options: { supported?: boolean; lookup?: () => Response } = {}
) {
  const original = globalThis.fetch;
  const lookups: string[] = [];
  globalThis.fetch = (async (input: RequestInfo | URL, init?: RequestInit) => {
    const url = new URL(new Request(input, init).url);
    if (url.pathname.endsWith('/config/lookup-capabilities')) {
      return Response.json({ supportsBookLookup: options.supported ?? true });
    }
    if (url.pathname.includes('/lookup/Books/')) {
      lookups.push(url.pathname + url.search);
      return (options.lookup ?? (() => Response.json([dune])))();
    }
    return Response.json(metadata[url.pathname.split('/').pop() ?? ''] ?? []);
  }) as typeof fetch;
  t.after(() => {
    globalThis.fetch = original;
  });
  return lookups;
}

describe('add book metadata lookup', () => {
  it('offers the lookup buttons only when the provider supports book lookup', async (t) => {
    stubLookupApi(t);
    assert.match(await (await get()).text(), /value="lookup:title"[\s\S]*value="lookup:isbn"/);

    stubLookupApi(t, { supported: false });
    assert.doesNotMatch(await (await get()).text(), /value="lookup:/);
  });

  it('looks up by title with the first author as an extra parameter and lists the matches', async (t) => {
    const lookups = stubLookupApi(t);
    const response = await post([
      ['intent', 'lookup:title'],
      ['title', ' Dune '],
      ['authors', 'Frank Herbert'],
      ['authors', 'Brian Herbert'],
      ['plot', 'Typed already'],
    ]);

    assert.equal(response.status, 200);
    assert.deepEqual(lookups, ['/lookup/Books/entity?title=Dune&author=Frank+Herbert']);
    const html = await response.text();
    assert.match(html, /<dialog open/);
    assert.match(html, /Dune: Book One/);
    assert.match(html, /Frank Herbert · Ace · 1990 · 412 pages · Paperback/);
    assert.match(html, /name="intent" value="apply:0"/);
    // The form keeps what was entered, as typed.
    assert.match(html, /name="title"[^>]*value=" Dune "/);
    assert.match(html, /Typed already/);
  });

  it('looks up by ISBN with the identifier alone', async (t) => {
    const lookups = stubLookupApi(t);
    const response = await post([
      ['intent', 'lookup:isbn'],
      ['isbn', '9780441172719'],
      ['title', 'ignored'],
    ]);

    assert.equal(response.status, 200);
    assert.deepEqual(lookups, ['/lookup/Books/isbn/9780441172719']);
  });

  it('shows a not-found state for an empty result and for a 404', async (t) => {
    stubLookupApi(t, { lookup: () => Response.json([]) });
    const empty = await (
      await post([
        ['intent', 'lookup:title'],
        ['title', 'Nope'],
      ])
    ).text();
    assert.match(empty, /<dialog open[\s\S]*No matches were found/);

    stubLookupApi(t, { lookup: () => new Response('none', { status: 404 }) });
    const missing = await (
      await post([
        ['intent', 'lookup:title'],
        ['title', 'Nope'],
      ])
    ).text();
    assert.match(missing, /No matches were found/);
  });

  it('shows the API reason for a rejected search and a generic message for a server failure', async (t) => {
    stubLookupApi(t, { lookup: () => new Response('Unsupported search properties: author', { status: 400 }) });
    const rejected = await (
      await post([
        ['intent', 'lookup:title'],
        ['title', 'Dune'],
      ])
    ).text();
    assert.match(rejected, /role="alert"[^>]*>Unsupported search properties: author</);

    stubLookupApi(t, { lookup: () => new Response('stack trace', { status: 500 }) });
    const failed = await (
      await post([
        ['intent', 'lookup:title'],
        ['title', 'Dune'],
      ])
    ).text();
    assert.match(failed, /role="alert"[^>]*>The lookup failed\. Please try again\.</);
    assert.doesNotMatch(failed, /stack trace/);
  });

  it('shows a could-not-be-reached message when the lookup request throws', async (t) => {
    stubLookupApi(t, {
      lookup: () => {
        throw new TypeError('fetch failed');
      },
    });
    const response = await post([
      ['intent', 'lookup:title'],
      ['title', 'Dune'],
    ]);

    assert.equal(response.status, 200);
    assert.match(await response.text(), /role="alert"[^>]*>The lookup could not be reached\. Please try again\./);
  });

  it('accepts a lookup submitted as multipart, ignoring the cover file', async (t) => {
    const lookups = stubLookupApi(t);
    const body = new FormData();
    body.set('intent', 'lookup:title');
    body.set('title', 'Dune');
    body.set('coverImage', new File(['x'], 'cover.png', { type: 'image/png' }));
    const response = await router.fetch(new Request(new URL(addUrl, 'http://localhost'), { method: 'POST', body }));

    assert.equal(response.status, 200);
    assert.equal(lookups.length, 1);
    assert.match(await response.text(), /<dialog open/);
  });

  it('says lookup is unavailable, without calling the lookup API, when the provider is off', async (t) => {
    const lookups = stubLookupApi(t, { supported: false });
    const html = await (
      await post([
        ['intent', 'lookup:title'],
        ['title', 'Dune'],
      ])
    ).text();

    assert.match(html, /Metadata lookup is not available/);
    assert.equal(lookups.length, 0);
  });

  it('asks for a value, with a 400, when the search field is blank', async (t) => {
    const lookups = stubLookupApi(t);
    const response = await post([
      ['intent', 'lookup:isbn'],
      ['isbn', '  '],
      ['title', 'Dune'],
    ]);

    assert.equal(response.status, 400);
    assert.match(await response.text(), /<p id="isbn-error"[^>]*>Enter an ISBN to look up<\/p>/);
    assert.equal(lookups.length, 0);
  });

  it('applies the chosen result to the form, keeping fields the result lacks', async (t) => {
    const lookups = stubLookupApi(t);
    const first = await (
      await post([
        ['intent', 'lookup:title'],
        ['title', 'Dune'],
        ['plot', 'My notes'],
      ])
    ).text();
    const results = /name="lookupResults" value="([^"]*)"/.exec(first)?.[1] ?? '';
    const payload = results.replaceAll('&quot;', '"').replaceAll('&amp;', '&');

    const response = await post([
      ['intent', 'apply:0'],
      ['lookupResults', payload],
      ['title', 'dune'],
      ['plot', 'My notes'],
      ['authors', 'Someone Else'],
    ]);

    assert.equal(response.status, 200);
    assert.equal(lookups.length, 1);
    const html = await response.text();
    assert.doesNotMatch(html, /<dialog/);
    assert.match(html, /name="title"[^>]*value="Dune"/);
    assert.match(html, /name="subtitle"[^>]*value="Book One"/);
    assert.match(html, /name="pages"[^>]*value="412"/);
    assert.match(html, /name="publicationDate"[^>]*value="1990"/);
    assert.match(html, /name="imageUrl"[^>]*value="https:\/\/example.com\/dune.jpg"/);
    assert.match(html, /<input type="hidden" name="authors" value="Frank Herbert"/);
    assert.doesNotMatch(html, /Someone Else/);
    assert.match(html, /My notes/);
  });

  it('rejects an apply for a result that is not there, and ignores unknown or mistyped fields', async (t) => {
    stubLookupApi(t);
    const missing = await post([
      ['intent', 'apply:3'],
      ['lookupResults', '[]'],
      ['title', 'Dune'],
    ]);
    assert.equal(missing.status, 400);
    assert.match(await missing.text(), /That result is no longer available/);

    const hostile = JSON.stringify([{ title: ['x'], pages: 5, subtitle: 'Kept', admin: 'yes' }]);
    const html = await (
      await post([
        ['intent', 'apply:0'],
        ['lookupResults', hostile],
        ['title', 'Dune'],
      ])
    ).text();
    assert.match(html, /name="title"[^>]*value="Dune"/);
    assert.match(html, /name="subtitle"[^>]*value="Kept"/);
    assert.match(html, /name="pages"(?![^>]*value="5")/);
    assert.doesNotMatch(html, /admin/);
  });

  it('closes the dialog without saving', async (t) => {
    stubLookupApi(t);
    const saved = stubApi(t);
    const response = await post([
      ['intent', 'dismiss'],
      ['title', 'Dune'],
    ]);

    assert.equal(response.status, 200);
    assert.doesNotMatch(await response.text(), /<dialog/);
    assert.equal(saved.length, 0);
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
