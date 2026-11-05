import * as assert from 'remix/assert';
import { describe, it } from 'remix/test';

import { router } from '../../../router.ts';

const existing = {
  id: 'm1',
  type: 'Movies',
  title: 'Alien',
  format: 'Blu-ray',
  runtime: 117,
  rating: 'R',
  isTvSeries: false,
  studios: ['20th Century Fox'],
  genres: ['Horror'],
  coverImage: { filePath: 'movies/m1.jpg' },
};

// A lookup result: it lacks some fields the saved item has.
const result = {
  title: 'Aliens',
  genres: ['Action'],
  studios: ['20th Century Fox'],
  runtime: 137,
  imageUrl: 'https://example.com/aliens.jpg',
};

// Stubs the API: GET /Movies/m1 returns the movie (or 404/500), PUT and DELETE are recorded or fail, plus lookup capabilities and results.
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
    if (url.pathname.endsWith('/Movies/m1')) {
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
      return Response.json({ supportsMovieLookup: options.supported ?? true });
    }
    if (url.pathname.includes('/lookup/Movies/')) {
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

describe('edit movie', () => {
  it('GET prefills the form from the existing movie', async (t) => {
    stubApi(t);
    const response = await router.fetch(url('/movies/m1/edit'));

    assert.equal(response.status, 200);
    const html = await response.text();
    assert.match(html, /<title>Edit Alien \| MediaSet<\/title>/);
    assert.match(html, /<form method="post" action="\/movies\/m1\/edit"/);
    assert.match(html, /name="title"[^>]*value="Alien"/);
    assert.match(html, /name="runtime"[^>]*value="117"/);
    assert.match(html, /<input type="hidden" name="studios" value="20th Century Fox"/);
  });

  it('returns 404 for a missing movie and 500 when it cannot be loaded', async (t) => {
    const calls = stubApi(t, { missing: true });
    assert.equal((await router.fetch(url('/movies/m1/edit'))).status, 404);
    assert.equal((await post('/movies/m1/edit', [['title', 'Alien']])).status, 404);
    assert.equal(calls.length, 0);

    stubApi(t, { loadFails: true });
    assert.equal((await router.fetch(url('/movies/m1/edit'))).status, 500);
  });

  it('re-renders with 400, errors and the submitted values when validation fails', async (t) => {
    const calls = stubApi(t);
    const response = await post('/movies/m1/edit', [
      ['title', ' '],
      ['runtime', 'long'],
      ['releaseDate', '1979'],
    ]);

    assert.equal(response.status, 400);
    const html = await response.text();
    assert.match(html, /<p id="title-error"[^>]*>Title is required<\/p>/);
    assert.match(html, /<p id="runtime-error"/);
    assert.match(html, /name="releaseDate"[^>]*value="1979"/);
    assert.equal(calls.length, 0);
  });

  it('saves over the existing movie, keeping unedited fields and dropping cleared ones, then redirects 303', async (t) => {
    const calls = stubApi(t);
    const response = await post('/movies/m1/edit', [
      ['title', 'Aliens'],
      ['runtime', ''],
      ['isTvSeries', 'true'],
      ['format', 'Blu-ray'],
      ['studios', '20th Century Fox'],
    ]);

    assert.equal(response.status, 303);
    assert.equal(response.headers.get('Location'), '/movies/m1');
    assert.deepEqual(calls, [
      {
        method: 'PUT',
        body: {
          id: 'm1',
          type: 'Movies',
          title: 'Aliens',
          format: 'Blu-ray',
          rating: 'R',
          isTvSeries: true,
          studios: ['20th Century Fox'],
          genres: [],
          coverImage: { filePath: 'movies/m1.jpg' },
        },
      },
    ]);
  });

  it('shows the API rejection with a 400, and a generic error with a 500', async (t) => {
    stubApi(t, { saveStatus: 400, saveBody: 'Barcode already exists' });
    const rejected = await post('/movies/m1/edit', [['title', 'Alien']]);
    assert.equal(rejected.status, 400);
    assert.match(await rejected.text(), /role="alert"[^>]*>Barcode already exists</);

    stubApi(t, { saveStatus: 500 });
    const failed = await post('/movies/m1/edit', [['title', 'Alien']]);
    assert.equal(failed.status, 500);
    const html = await failed.text();
    assert.match(html, /The movie could not be saved/);
    assert.match(html, /name="title"[^>]*value="Alien"/);
  });
});

describe('edit movie metadata lookup', () => {
  const editUrl = '/movies/m1/edit';
  const savedImage = 'movies/m1.jpg';

  // The results come back in a hidden field; read them out the way the browser would send them.
  const resultsOf = (html: string) =>
    /name="lookupResults" value="([^"]*)"/.exec(html)?.[1]?.replaceAll('&quot;', '"').replaceAll('&amp;', '&') ?? '[]';

  it('offers the lookup buttons only when the provider supports movie lookup', async (t) => {
    stubApi(t);
    assert.match(await (await router.fetch(url(editUrl))).text(), /value="lookup:title"[\s\S]*value="lookup:barcode"/);

    stubApi(t, { supported: false });
    assert.doesNotMatch(await (await router.fetch(url(editUrl))).text(), /value="lookup:/);
  });

  it('lists the matches, keeping the entered values and the saved image, without saving', async (t) => {
    const lookups: string[] = [];
    const calls = stubApi(t, { lookups });
    const response = await post(editUrl, [
      ['intent', 'lookup:title'],
      ['title', ' Alien '],
      ['plot', 'Typed plot'],
    ]);

    assert.equal(response.status, 200);
    assert.deepEqual(lookups, ['/lookup/Movies/entity?title=Alien']);
    const html = await response.text();
    assert.match(html, /<dialog open/);
    assert.match(html, /Aliens/);
    assert.match(html, /name="intent" value="apply:0"/);
    assert.match(html, /name="title"[^>]*value=" ?Alien ?"/);
    assert.ok(html.includes(savedImage), 'the saved image is still shown');
    assert.deepEqual(calls, []);
  });

  it('looks up by barcode too', async (t) => {
    const lookups: string[] = [];
    stubApi(t, { lookups });
    const response = await post(editUrl, [
      ['intent', 'lookup:barcode'],
      ['barcode', '024543'],
    ]);

    assert.equal(response.status, 200);
    assert.deepEqual(lookups, ['/lookup/Movies/upc/024543']);
    const html = await response.text();
    // One match for an ISBN or barcode fills the form at once; no dialog to choose from.
    assert.doesNotMatch(html, /<dialog open/);
    assert.match(html, /data-rmx-key="/);
  });

  it('asks for a value, with a 400, when the search field is blank', async (t) => {
    const lookups: string[] = [];
    stubApi(t, { lookups });
    const response = await post(editUrl, [
      ['intent', 'lookup:barcode'],
      ['barcode', ' '],
    ]);

    assert.equal(response.status, 400);
    assert.match(await response.text(), /Enter a barcode to look up/);
    assert.deepEqual(lookups, []);
  });

  it('shows not found, unavailable and error states in the dialog', async (t) => {
    const search: Array<[string, string]> = [
      ['intent', 'lookup:title'],
      ['title', 'Alien'],
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
        ['title', 'Alien'],
      ])
    ).text();

    const response = await post(editUrl, [
      ['intent', 'apply:0'],
      ['lookupResults', resultsOf(found)],
      ['title', 'Alien'],
      ['releaseDate', '1979'],
    ]);

    assert.equal(response.status, 200);
    const html = await response.text();
    assert.doesNotMatch(html, /<dialog open/);
    assert.match(html, /name="title"[^>]*value="Aliens"/);
    assert.match(html, /name="runtime"[^>]*value="137"/);
    // A field the result lacks keeps its current value.
    assert.match(html, /name="releaseDate"[^>]*value="1979"/);
    assert.ok(html.includes(savedImage), 'the saved image is still shown');
    assert.match(html, /name="removeImage"/);
    // Nothing is saved until the user saves.
    assert.deepEqual(calls, []);
  });

  it('keeps a ticked remove-image box through a lookup and an apply, and leaves it unticked otherwise', async (t) => {
    const calls = stubApi(t);
    const search: Array<[string, string]> = [
      ['intent', 'lookup:title'],
      ['title', 'Alien'],
    ];
    const ticked = await (await post(editUrl, [...search, ['removeImage', 'on']])).text();
    assert.match(ticked, /name="removeImage"[^>]*checked/);

    const applied = await (
      await post(editUrl, [
        ['intent', 'apply:0'],
        ['lookupResults', resultsOf(ticked)],
        ['title', 'Alien'],
        ['removeImage', 'on'],
      ])
    ).text();
    assert.match(applied, /name="removeImage"[^>]*checked/);

    const unticked = await (await post(editUrl, search)).text();
    assert.doesNotMatch(unticked, /name="removeImage"[^>]*checked/);
    assert.deepEqual(calls, []);
  });

  it('closes the dialog without saving', async (t) => {
    const calls = stubApi(t);
    const response = await post(editUrl, [
      ['intent', 'dismiss'],
      ['title', 'Alien'],
    ]);

    assert.equal(response.status, 200);
    assert.doesNotMatch(await response.text(), /<dialog open/);
    assert.deepEqual(calls, []);
  });
});

describe('delete movie', () => {
  it('GET shows a confirmation form posting to the delete URL', async (t) => {
    stubApi(t);
    const response = await router.fetch(url('/movies/m1/delete'));

    assert.equal(response.status, 200);
    const html = await response.text();
    assert.match(html, /<form method="post" action="\/movies\/m1\/delete"/);
    assert.match(html, /<strong>Alien<\/strong>/);
  });

  it('returns 404 for a missing movie on GET and POST', async (t) => {
    stubApi(t, { missing: true });
    assert.equal((await router.fetch(url('/movies/m1/delete'))).status, 404);

    stubApi(t, { deleteStatus: 404 });
    assert.equal((await post('/movies/m1/delete')).status, 404);
  });

  it('deletes through the API and redirects 303 to the list', async (t) => {
    const calls = stubApi(t);
    const response = await post('/movies/m1/delete');

    assert.equal(response.status, 303);
    assert.equal(response.headers.get('Location'), '/movies');
    assert.deepEqual(calls, [{ method: 'DELETE', body: null }]);
  });

  it('keeps the confirmation and shows an error when the API fails', async (t) => {
    stubApi(t, { deleteStatus: 500 });
    const response = await post('/movies/m1/delete');

    assert.equal(response.status, 500);
    assert.match(await response.text(), /role="alert"[^>]*>The movie could not be deleted/);
  });
});
