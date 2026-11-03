import * as assert from 'remix/assert';
import { describe, it } from 'remix/test';

import { router } from '../../../router.ts';
import { routes } from '../../../routes.ts';

const metadata: Record<string, string[]> = {
  developers: ['Nintendo'],
  publishers: ['Sega'],
  genres: ['Platformer'],
  platform: ['Switch'],
  format: ['Cartridge'],
};

const zelda = {
  title: 'Zelda',
  genres: ['Adventure'],
  developers: ['Nintendo'],
  publishers: ['Nintendo'],
  platform: 'Switch',
  releaseDate: '2017',
  rating: 'E10+',
  format: 'Cartridge',
  imageUrl: 'https://example.com/zelda.jpg',
};

// Stubs the API: metadata and capabilities, the lookup endpoint (recorded) and POST /Games (recorded or failing).
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
    if (request.method === 'POST' && url.pathname.endsWith('/Games')) {
      if (options.failSave) return new Response('boom', { status: 500 });
      if (options.rejectSave) return new Response(options.rejectSave, { status: 400 });
      saved.push(await request.json());
      return Response.json({ id: 'new-id' }, { status: 201 });
    }
    if (url.pathname.endsWith('/config/lookup-capabilities')) {
      return Response.json({ supportsGameLookup: options.supported ?? true });
    }
    if (url.pathname.includes('/lookup/Games/')) {
      lookups.push(url.pathname + url.search);
      return (options.lookup ?? (() => Response.json([zelda])))();
    }
    return Response.json(metadata[url.pathname.split('/').pop() ?? ''] ?? []);
  }) as typeof fetch;
  t.after(() => {
    globalThis.fetch = original;
  });
  return { saved, lookups };
}

const addUrl = routes.entityAdd.games.index.href();
const get = () => router.fetch(new URL(addUrl, 'http://localhost'));
const post = (fields: Array<[string, string]>) =>
  router.fetch(
    new Request(new URL(addUrl, 'http://localhost'), {
      method: 'POST',
      headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
      body: new URLSearchParams(fields),
    })
  );

describe('add game form', () => {
  it('GET serves the form, posting to the same URL', async (t) => {
    stubApi(t);
    const response = await get();

    assert.equal(response.status, 200);
    const html = await response.text();
    assert.match(html, /<title>Add Game \| MediaSet<\/title>/);
    assert.match(html, /<form method="post" action="\/games\/add"/);
    for (const name of ['title', 'barcode', 'releaseDate', 'rating', 'description']) {
      assert.match(html, new RegExp(`name="${name}"`));
    }
    assert.match(html, /role="combobox"/);
    assert.doesNotMatch(html, /role="alert"/);
  });

  it('re-renders with 400, per-field errors and the submitted values preserved', async (t) => {
    const { saved } = stubApi(t);
    const response = await post([
      ['title', '   '],
      ['imageUrl', 'ftp://example.com/x.jpg'],
      ['rating', 'T'],
      ['developers', 'Nintendo'],
      ['developers', 'Someone New'],
      ['description', 'Hyrule & danger'],
    ]);

    assert.equal(response.status, 400);
    const html = await response.text();
    assert.match(html, /<p id="title-error"[^>]*>Title is required<\/p>/);
    assert.match(html, /<p id="imageUrl-error"[^>]*>Enter a valid http or https image URL<\/p>/);
    assert.match(html, /name="rating"[^>]*value="T"/);
    assert.match(html, /<input type="hidden" name="developers" value="Someone New"/);
    assert.match(html, /Hyrule &amp; danger/);
    assert.equal(saved.length, 0);
  });

  it('saves lists and text fields, then redirects 303 to the new game', async (t) => {
    const { saved } = stubApi(t);
    const response = await post([
      ['title', '  Zelda '],
      ['platform', 'Switch'],
      ['rating', 'E10+'],
      ['developers', 'Nintendo'],
      ['developers', ' Nintendo '],
      ['publishers', 'Nintendo'],
      ['genres', 'Adventure'],
      ['genres', ''],
      ['barcode', ''],
    ]);

    assert.equal(response.status, 303);
    assert.equal(response.headers.get('Location'), '/games/new-id');
    assert.deepEqual(saved, [
      {
        type: 'Games',
        title: 'Zelda',
        platform: 'Switch',
        rating: 'E10+',
        developers: ['Nintendo'],
        publishers: ['Nintendo'],
        genres: ['Adventure'],
      },
    ]);
  });

  it('leaves blank optional fields out and saves empty lists', async (t) => {
    const { saved } = stubApi(t);
    const response = await post([
      ['title', 'Zelda'],
      ['platform', ''],
    ]);

    assert.equal(response.status, 303);
    assert.deepEqual(saved, [{ type: 'Games', title: 'Zelda', developers: [], publishers: [], genres: [] }]);
  });

  it('shows an error with the input kept when the API fails or rejects the game', async (t) => {
    stubApi(t, { failSave: true });
    const failed = await post([['title', 'Zelda']]);
    assert.equal(failed.status, 500);
    const html = await failed.text();
    assert.match(html, /The game could not be saved/);
    assert.match(html, /name="title"[^>]*value="Zelda"/);

    stubApi(t, { rejectSave: 'An entity with that barcode already exists' });
    const rejected = await post([['title', 'Zelda']]);
    assert.equal(rejected.status, 400);
    assert.match(await rejected.text(), /role="alert"[^>]*>An entity with that barcode already exists</);
  });
});

describe('add game metadata lookup', () => {
  it('offers the lookup buttons only when the provider supports game lookup', async (t) => {
    stubApi(t);
    assert.match(await (await get()).text(), /value="lookup:title"[\s\S]*value="lookup:barcode"/);

    stubApi(t, { supported: false });
    assert.doesNotMatch(await (await get()).text(), /value="lookup:/);
  });

  it('looks up by title and lists the matches, keeping the entered values', async (t) => {
    const { lookups } = stubApi(t);
    const response = await post([
      ['intent', 'lookup:title'],
      ['title', ' Zelda '],
      ['description', 'Typed already'],
    ]);

    assert.equal(response.status, 200);
    assert.deepEqual(lookups, ['/lookup/Games/entity?title=Zelda']);
    const html = await response.text();
    assert.match(html, /<dialog open/);
    assert.match(html, /2017 · Switch · Nintendo · Cartridge/);
    assert.match(html, /name="intent" value="apply:0"/);
    assert.match(html, /name="title"[^>]*value=" Zelda "/);
    assert.match(html, /Typed already/);
  });

  it('looks up by barcode as a UPC', async (t) => {
    const { lookups } = stubApi(t);
    const response = await post([
      ['intent', 'lookup:barcode'],
      ['barcode', '024543123456'],
    ]);

    assert.equal(response.status, 200);
    assert.deepEqual(lookups, ['/lookup/Games/upc/024543123456']);
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
    assert.match(html, /name="barcode"[^>]*aria-invalid[^>]*aria-describedby="barcode-error"/);
    assert.equal(lookups.length, 0);
  });

  it('applies a chosen result to the form without overwriting fields the result lacks', async (t) => {
    stubApi(t);
    const search = await (
      await post([
        ['intent', 'lookup:title'],
        ['title', 'Zelda'],
      ])
    ).text();
    const results = /name="lookupResults" value="([^"]*)"/.exec(search)?.[1]?.replaceAll('&quot;', '"') ?? '[]';

    const html = await (
      await post([
        ['intent', 'apply:0'],
        ['lookupResults', results],
        ['title', 'Zelda'],
        ['barcode', '024543123456'],
      ])
    ).text();

    assert.doesNotMatch(html, /<dialog open/);
    assert.match(html, /name="rating"[^>]*value="E10\+"/);
    assert.match(html, /name="barcode"[^>]*value="024543123456"/);
    assert.match(html, /<input type="hidden" name="developers" value="Nintendo"/);
  });
});
