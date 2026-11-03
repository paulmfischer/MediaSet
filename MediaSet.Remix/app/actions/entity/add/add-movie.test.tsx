import * as assert from 'remix/assert';
import { describe, it } from 'remix/test';

import { router } from '../../../router.ts';
import { routes } from '../../../routes.ts';

const metadata: Record<string, string[]> = {
  studios: ['Universal'],
  genres: ['Science Fiction'],
  format: ['Blu-ray'],
};

const alien = {
  title: 'Alien',
  genres: ['Horror'],
  studios: ['20th Century Fox'],
  releaseDate: '1979',
  runtime: 117,
  format: 'Blu-ray',
  imageUrl: 'https://example.com/alien.jpg',
};

// Stubs the API: metadata and capabilities, the lookup endpoint (recorded) and POST /Movies (recorded or failing).
function stubApi(
  t: { after: (fn: () => void) => void },
  options: { failSave?: boolean; rejectSave?: string; supported?: boolean; lookup?: () => Response } = {}
) {
  const original = globalThis.fetch;
  const saved: unknown[] = [];
  const lookups: string[] = [];
  globalThis.fetch = (async (input: RequestInfo | URL, init?: RequestInit) => {
    const request = new Request(input, init);
    const url = new URL(request.url);
    if (request.method === 'POST' && url.pathname.endsWith('/Movies')) {
      if (options.failSave) return new Response('boom', { status: 500 });
      if (options.rejectSave) return new Response(options.rejectSave, { status: 400 });
      saved.push(await request.json());
      return Response.json({ id: 'new-id' }, { status: 201 });
    }
    if (url.pathname.endsWith('/config/lookup-capabilities')) {
      return Response.json({ supportsMovieLookup: options.supported ?? true });
    }
    if (url.pathname.includes('/lookup/Movies/')) {
      lookups.push(url.pathname + url.search);
      return (options.lookup ?? (() => Response.json([alien])))();
    }
    return Response.json(metadata[url.pathname.split('/').pop() ?? ''] ?? []);
  }) as typeof fetch;
  t.after(() => {
    globalThis.fetch = original;
  });
  return { saved, lookups };
}

const addUrl = routes.entityAdd.movies.index.href();
const get = () => router.fetch(new URL(addUrl, 'http://localhost'));
const post = (fields: Array<[string, string]>) =>
  router.fetch(
    new Request(new URL(addUrl, 'http://localhost'), {
      method: 'POST',
      headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
      body: new URLSearchParams(fields),
    })
  );

describe('add movie form', () => {
  it('GET serves the form, posting to the same URL', async (t) => {
    stubApi(t);
    const response = await get();

    assert.equal(response.status, 200);
    const html = await response.text();
    assert.match(html, /<title>Add Movie \| MediaSet<\/title>/);
    assert.match(html, /<form method="post" action="\/movies\/add"/);
    for (const name of ['title', 'barcode', 'runtime', 'releaseDate', 'isTvSeries', 'plot']) {
      assert.match(html, new RegExp(`name="${name}"`));
    }
    assert.match(html, /role="combobox"/);
    assert.doesNotMatch(html, /role="alert"/);
  });

  it('re-renders with 400, per-field errors and the submitted values preserved', async (t) => {
    const { saved } = stubApi(t);
    const response = await post([
      ['title', '   '],
      ['runtime', 'long'],
      ['isTvSeries', 'true'],
      ['studios', 'Universal'],
      ['studios', 'Someone New'],
      ['plot', 'Space & dread'],
    ]);

    assert.equal(response.status, 400);
    const html = await response.text();
    assert.match(html, /<p id="title-error"[^>]*>Title is required<\/p>/);
    assert.match(html, /<p id="runtime-error"[^>]*>Runtime must be a whole number from 0 to 999999<\/p>/);
    assert.match(html, /name="runtime"[^>]*value="long"/);
    assert.match(html, /name="isTvSeries"[^>]*checked/);
    assert.match(html, /<input type="hidden" name="studios" value="Someone New"/);
    assert.match(html, /Space &amp; dread/);
    assert.equal(saved.length, 0);
  });

  it('saves lists, numbers and the TV flag, then redirects 303 to the new movie', async (t) => {
    const { saved } = stubApi(t);
    const response = await post([
      ['title', '  Alien '],
      ['runtime', '117'],
      ['isTvSeries', 'true'],
      ['studios', '20th Century Fox'],
      ['studios', ' 20th Century Fox '],
      ['genres', 'Horror'],
      ['genres', ''],
      ['barcode', ''],
    ]);

    assert.equal(response.status, 303);
    assert.equal(response.headers.get('Location'), '/movies/new-id');
    assert.deepEqual(saved, [
      {
        type: 'Movies',
        title: 'Alien',
        runtime: 117,
        isTvSeries: true,
        studios: ['20th Century Fox'],
        genres: ['Horror'],
      },
    ]);
  });

  it('saves an unchecked TV flag as false and a blank runtime as no value', async (t) => {
    const { saved } = stubApi(t);
    const response = await post([
      ['title', 'Alien'],
      ['runtime', ''],
    ]);

    assert.equal(response.status, 303);
    assert.deepEqual(saved, [{ type: 'Movies', title: 'Alien', isTvSeries: false, studios: [], genres: [] }]);
  });

  it('shows an error with the input kept when the API fails or rejects the movie', async (t) => {
    stubApi(t, { failSave: true });
    const failed = await post([['title', 'Alien']]);
    assert.equal(failed.status, 500);
    const html = await failed.text();
    assert.match(html, /The movie could not be saved/);
    assert.match(html, /name="title"[^>]*value="Alien"/);

    stubApi(t, { rejectSave: 'An entity with that barcode already exists' });
    const rejected = await post([['title', 'Alien']]);
    assert.equal(rejected.status, 400);
    assert.match(await rejected.text(), /role="alert"[^>]*>An entity with that barcode already exists</);
  });
});

describe('add movie metadata lookup', () => {
  it('offers the lookup buttons only when the provider supports movie lookup', async (t) => {
    stubApi(t);
    assert.match(await (await get()).text(), /value="lookup:title"[\s\S]*value="lookup:barcode"/);

    stubApi(t, { supported: false });
    assert.doesNotMatch(await (await get()).text(), /value="lookup:/);
  });

  it('looks up by title and lists the matches, keeping the entered values', async (t) => {
    const { lookups } = stubApi(t);
    const response = await post([
      ['intent', 'lookup:title'],
      ['title', ' Alien '],
      ['plot', 'Typed already'],
    ]);

    assert.equal(response.status, 200);
    assert.deepEqual(lookups, ['/lookup/Movies/entity?title=Alien']);
    const html = await response.text();
    assert.match(html, /<dialog open/);
    assert.match(html, /1979 · 117 min · 20th Century Fox · Blu-ray/);
    assert.match(html, /name="intent" value="apply:0"/);
    assert.match(html, /name="title"[^>]*value=" Alien "/);
    assert.match(html, /Typed already/);
  });

  it('looks up by barcode as a UPC', async (t) => {
    const { lookups } = stubApi(t);
    const response = await post([
      ['intent', 'lookup:barcode'],
      ['barcode', '024543123456'],
    ]);

    assert.equal(response.status, 200);
    assert.deepEqual(lookups, ['/lookup/Movies/upc/024543123456']);
  });

  it('asks for a value when the search field is blank', async (t) => {
    const { lookups } = stubApi(t);
    const response = await post([
      ['intent', 'lookup:barcode'],
      ['barcode', ' '],
    ]);

    assert.equal(response.status, 400);
    const html = await response.text();
    assert.match(html, /Enter a barcode to look up/);
    const barcodeInput = html.match(/<input[^>]*name="barcode"[^>]*>/)?.[0] ?? '';
    assert.match(barcodeInput, /aria-invalid/);
    assert.match(barcodeInput, /aria-describedby="barcode-error"/);
    assert.equal(lookups.length, 0);
  });

  it('applies a chosen result to the form without overwriting fields the result lacks', async (t) => {
    stubApi(t);
    const search = await (
      await post([
        ['intent', 'lookup:title'],
        ['title', 'Alien'],
      ])
    ).text();
    const results = /name="lookupResults" value="([^"]*)"/.exec(search)?.[1]?.replaceAll('&quot;', '"') ?? '[]';

    const html = await (
      await post([
        ['intent', 'apply:0'],
        ['lookupResults', results],
        ['title', 'Alien'],
        ['barcode', '024543123456'],
      ])
    ).text();

    assert.doesNotMatch(html, /<dialog open/);
    assert.match(html, /name="runtime"[^>]*value="117"/);
    assert.match(html, /name="barcode"[^>]*value="024543123456"/);
    assert.match(html, /<input type="hidden" name="studios" value="20th Century Fox"/);
  });
});
