import * as assert from 'remix/assert';
import { describe, it } from 'remix/test';

import { router } from '../../../router.ts';
import { routes } from '../../../routes.ts';

const metadata: Record<string, string[]> = {
  artist: ['Pink Floyd'],
  label: ['Harvest'],
  genres: ['Rock'],
  format: ['CD', 'Vinyl'],
};

const discList = [{ trackNumber: 1, title: 'Speak to Me', duration: 68000 }];

const album = {
  title: 'The Dark Side of the Moon',
  artist: 'Pink Floyd',
  releaseDate: '1973',
  label: 'Harvest',
  genres: ['Rock'],
  duration: 2580000,
  tracks: 10,
  discs: 1,
  discList,
  format: 'CD',
  imageUrl: 'https://example.com/dsotm.jpg',
};

// Stubs the API: metadata and capabilities, the lookup endpoint (recorded) and POST /Musics (recorded or failing).
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
    if (request.method === 'POST' && url.pathname.endsWith('/Musics')) {
      if (options.failSave) return new Response('boom', { status: 500 });
      if (options.rejectSave) return new Response(options.rejectSave, { status: 400 });
      saved.push(await request.json());
      return Response.json({ id: 'new-id' }, { status: 201 });
    }
    if (url.pathname.endsWith('/config/lookup-capabilities')) {
      return Response.json({ supportsMusicLookup: options.supported ?? true });
    }
    if (url.pathname.includes('/lookup/Musics/')) {
      lookups.push(url.pathname + url.search);
      return (options.lookup ?? (() => Response.json([album])))();
    }
    return Response.json(metadata[url.pathname.split('/').pop() ?? ''] ?? []);
  }) as typeof fetch;
  t.after(() => {
    globalThis.fetch = original;
  });
  return { saved, lookups };
}

const addUrl = routes.entityAdd.musics.index.href();
const get = () => router.fetch(new URL(addUrl, 'http://localhost'));
const post = (fields: Array<[string, string]>) =>
  router.fetch(
    new Request(new URL(addUrl, 'http://localhost'), {
      method: 'POST',
      headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
      body: new URLSearchParams(fields),
    })
  );

describe('add music form', () => {
  it('GET serves the form, posting to the same URL', async (t) => {
    stubApi(t);
    const response = await get();

    assert.equal(response.status, 200);
    const html = await response.text();
    assert.match(html, /<title>Add Music \| MediaSet<\/title>/);
    assert.match(html, /<form method="post" action="\/musics\/add"/);
    for (const name of ['title', 'barcode', 'releaseDate', 'tracks', 'discs', 'durationMinutes', 'durationSeconds']) {
      assert.match(html, new RegExp(`name="${name}"`));
    }
    assert.match(html, /role="combobox"/);
    assert.doesNotMatch(html, /role="alert"/);
  });

  it('re-renders with 400, per-field errors and the submitted values preserved', async (t) => {
    const { saved } = stubApi(t);
    const response = await post([
      ['title', '   '],
      ['artist', ''],
      ['imageUrl', 'ftp://example.com/x.jpg'],
      ['tracks', '1.5'],
      ['discs', '-1'],
      ['durationMinutes', 'abc'],
      ['durationSeconds', '60'],
      ['label', 'Harvest'],
      ['genres', 'Rock'],
      ['genres', 'Someone New'],
    ]);

    assert.equal(response.status, 400);
    const html = await response.text();
    assert.match(html, /<p id="title-error"[^>]*>Title is required<\/p>/);
    assert.match(html, /<p id="artist-error"[^>]*>Artist is required<\/p>/);
    assert.match(html, /<p id="imageUrl-error"[^>]*>Enter a valid http or https image URL<\/p>/);
    assert.match(html, /<p id="tracks-error"[^>]*>Tracks must be a whole number/);
    assert.match(html, /<p id="discs-error"[^>]*>Discs must be a whole number/);
    assert.match(html, /<p id="durationMinutes-error"[^>]*>Minutes must be a whole number/);
    assert.match(html, /<p id="durationSeconds-error"[^>]*>Seconds must be a whole number from 0 to 59/);
    assert.match(html, /name="tracks"[^>]*value="1.5"/);
    assert.match(html, /<input type="hidden" name="genres" value="Someone New"/);
    assert.equal(saved.length, 0);
  });

  it('saves lists, numbers and the duration in milliseconds, then redirects 303 to the new album', async (t) => {
    const { saved } = stubApi(t);
    const response = await post([
      ['title', '  The Wall '],
      ['artist', 'Pink Floyd'],
      ['format', 'Vinyl'],
      ['label', 'Harvest'],
      ['tracks', '26'],
      ['discs', '2'],
      ['durationMinutes', '81'],
      ['durationSeconds', '5'],
      ['genres', 'Rock'],
      ['genres', ' Rock '],
      ['genres', ''],
      ['barcode', ''],
    ]);

    assert.equal(response.status, 303);
    assert.equal(response.headers.get('Location'), '/musics/new-id');
    assert.deepEqual(saved, [
      {
        type: 'Musics',
        title: 'The Wall',
        artist: 'Pink Floyd',
        format: 'Vinyl',
        label: 'Harvest',
        tracks: 26,
        discs: 2,
        duration: 4865000,
        genres: ['Rock'],
      },
    ]);
  });

  it('treats a single duration part as zero for the other, and 0 as a valid number', async (t) => {
    const { saved } = stubApi(t);
    await post([
      ['title', 'A'],
      ['artist', 'B'],
      ['durationSeconds', '30'],
      ['tracks', '0'],
    ]);

    assert.deepEqual(saved, [{ type: 'Musics', title: 'A', artist: 'B', tracks: 0, duration: 30000, genres: [] }]);
  });

  it('leaves blank optional fields out and saves empty lists', async (t) => {
    const { saved } = stubApi(t);
    const response = await post([
      ['title', 'A'],
      ['artist', 'B'],
      ['format', ''],
      ['tracks', ''],
      ['durationMinutes', ' '],
    ]);

    assert.equal(response.status, 303);
    assert.deepEqual(saved, [{ type: 'Musics', title: 'A', artist: 'B', genres: [] }]);
  });

  it('passes a track list through unchanged and ignores a malformed one', async (t) => {
    const { saved } = stubApi(t);
    await post([
      ['title', 'A'],
      ['artist', 'B'],
      ['discList', JSON.stringify(discList)],
    ]);
    await post([
      ['title', 'A'],
      ['artist', 'B'],
      ['discList', '{not json'],
    ]);

    assert.deepEqual(saved, [
      { type: 'Musics', title: 'A', artist: 'B', discList, genres: [] },
      { type: 'Musics', title: 'A', artist: 'B', genres: [] },
    ]);
  });

  it('shows an error with the input kept when the API fails or rejects the album', async (t) => {
    stubApi(t, { failSave: true });
    const failed = await post([
      ['title', 'A'],
      ['artist', 'B'],
    ]);
    assert.equal(failed.status, 500);
    const html = await failed.text();
    assert.match(html, /The album could not be saved/);
    assert.match(html, /name="title"[^>]*value="A"/);

    stubApi(t, { rejectSave: 'An entity with that barcode already exists' });
    const rejected = await post([
      ['title', 'A'],
      ['artist', 'B'],
    ]);
    assert.equal(rejected.status, 400);
    assert.match(await rejected.text(), /role="alert"[^>]*>An entity with that barcode already exists</);
  });
});

describe('add music metadata lookup', () => {
  it('offers the lookup buttons only when the provider supports music lookup', async (t) => {
    stubApi(t);
    assert.match(await (await get()).text(), /value="lookup:title"[\s\S]*value="lookup:barcode"/);

    stubApi(t, { supported: false });
    assert.doesNotMatch(await (await get()).text(), /value="lookup:/);
  });

  it('looks up by title and artist and lists the matches, keeping the entered values', async (t) => {
    const { lookups } = stubApi(t);
    const response = await post([
      ['intent', 'lookup:title'],
      ['title', ' The Dark Side of the Moon '],
      ['artist', 'Pink Floyd'],
      ['label', 'Typed already'],
    ]);

    assert.equal(response.status, 200);
    assert.deepEqual(lookups, ['/lookup/Musics/entity?title=The+Dark+Side+of+the+Moon&artist=Pink+Floyd']);
    const html = await response.text();
    assert.match(html, /<dialog open/);
    assert.match(html, /Pink Floyd · 1973 · Harvest · CD/);
    assert.match(html, /name="intent" value="apply:0"/);
    assert.match(html, /name="label"[^>]*value="Typed already"/);
  });

  it('looks up by barcode as a UPC', async (t) => {
    const { lookups } = stubApi(t);
    const response = await post([
      ['intent', 'lookup:barcode'],
      ['barcode', '602547'],
    ]);

    assert.equal(response.status, 200);
    assert.deepEqual(lookups, ['/lookup/Musics/upc/602547']);
  });

  it('asks for the missing search value instead of looking up', async (t) => {
    const { lookups } = stubApi(t);
    const blankBarcode = await post([
      ['intent', 'lookup:barcode'],
      ['barcode', ' '],
    ]);
    assert.equal(blankBarcode.status, 400);
    assert.match(await blankBarcode.text(), /Enter a barcode to look up/);

    const noArtist = await post([
      ['intent', 'lookup:title'],
      ['title', 'Wall'],
    ]);
    assert.equal(noArtist.status, 400);
    assert.match(await noArtist.text(), /<p id="artist-error"[^>]*>Enter an artist to look up/);

    const noTitle = await post([
      ['intent', 'lookup:title'],
      ['artist', 'Pink Floyd'],
    ]);
    assert.equal(noTitle.status, 400);
    assert.match(await noTitle.text(), /Enter a title to look up/);
    assert.equal(lookups.length, 0);
  });

  it('applies a chosen result, including duration and track list, without overwriting fields it lacks', async (t) => {
    stubApi(t);
    const search = await (
      await post([
        ['intent', 'lookup:title'],
        ['title', 'Dark Side'],
        ['artist', 'Pink Floyd'],
      ])
    ).text();
    const results = /name="lookupResults" value="([^"]*)"/.exec(search)?.[1]?.replaceAll('&quot;', '"') ?? '[]';

    const html = await (
      await post([
        ['intent', 'apply:0'],
        ['lookupResults', results],
        ['title', 'Dark Side'],
        ['barcode', '602547'],
      ])
    ).text();

    assert.doesNotMatch(html, /<dialog open/);
    assert.match(html, /name="title"[^>]*value="The Dark Side of the Moon"/);
    assert.match(html, /name="durationMinutes"[^>]*value="43"/);
    assert.match(html, /name="durationSeconds"[^>]*value="0"/);
    assert.match(html, /name="tracks"[^>]*value="10"/);
    assert.match(html, /name="barcode"[^>]*value="602547"/);
    assert.match(html, /<input type="hidden" name="genres" value="Rock"/);
    assert.match(html, /<input type="hidden" name="discList" value="[^"]*Speak to Me/);
  });

  it('clears a track list from an earlier result when the new result has none', async (t) => {
    stubApi(t, { lookup: () => Response.json([{ ...album, discList: undefined }]) });
    const search = await (
      await post([
        ['intent', 'lookup:title'],
        ['title', 'Dark Side'],
        ['artist', 'Pink Floyd'],
      ])
    ).text();
    const results = /name="lookupResults" value="([^"]*)"/.exec(search)?.[1]?.replaceAll('&quot;', '"') ?? '[]';

    const html = await (
      await post([
        ['intent', 'apply:0'],
        ['lookupResults', results],
        ['title', 'Dark Side'],
        ['discList', JSON.stringify(discList)],
      ])
    ).text();

    assert.doesNotMatch(html, /name="discList"/);
  });

  it('links the barcode lookup error to the barcode input', async (t) => {
    stubApi(t);
    const html = await (
      await post([
        ['intent', 'lookup:barcode'],
        ['barcode', ' '],
      ])
    ).text();

    assert.match(html, /name="barcode"[^>]*aria-invalid[^>]*aria-describedby="barcode-error"/);
  });
});
