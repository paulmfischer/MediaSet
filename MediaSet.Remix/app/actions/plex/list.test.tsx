import * as assert from 'remix/assert';
import { describe, it } from 'remix/test';

import { router } from '../../router.ts';
import { listHref, parseListParams } from '../entity/list-params.ts';

const get = (path: string) => router.fetch(new URL(path, 'http://localhost'));

const enabled = { enabled: true, entityTypes: ['movies'] };

const movies = [
  { id: 'm1', type: 'Movies', title: 'Alien', format: 'DVD', plex: { onPlex: true, broken: false, watched: false } },
  { id: 'm2', type: 'Movies', title: 'Brazil', format: 'DVD', plex: { onPlex: true, broken: false, watched: true } },
  { id: 'm3', type: 'Movies', title: 'Cube', format: 'DVD', plex: { onPlex: false, broken: false, watched: false } },
  { id: 'm4', type: 'Movies', title: 'Dune', format: 'DVD', plex: { onPlex: false, broken: true, watched: false } },
  { id: 'm5', type: 'Movies', title: 'Eraserhead', format: 'DVD' },
];

// Stubs the API; `plex` is what /config/integrations reports. Records every call as "METHOD /path?query".
function stubApi(
  t: { after: (fn: () => void) => void },
  options: { plex?: { enabled: boolean; entityTypes?: readonly string[] }; items?: unknown[]; totalPages?: number } = {}
) {
  const original = globalThis.fetch;
  const calls: URL[] = [];
  globalThis.fetch = (async (input: RequestInfo | URL) => {
    const url = new URL(input instanceof Request ? input.url : String(input));
    if (url.pathname === '/config/integrations') {
      return Response.json(options.plex ? [{ key: 'plex', displayName: 'Plex', ...options.plex }] : []);
    }
    calls.push(url);
    const items = options.items ?? movies;
    return Response.json({
      items,
      totalCount: items.length,
      page: 1,
      pageSize: 75,
      totalPages: options.totalPages ?? 1,
    });
  }) as typeof fetch;
  t.after(() => {
    globalThis.fetch = original;
  });
  return calls;
}

const iconCount = (html: string) => (html.match(/data-plex-icon=/g) ?? []).length;

describe('plex list params', () => {
  it('should read a valid plex filter from the query', () => {
    for (const value of ['onPlex', 'notOnPlex', 'unwatched', 'broken'] as const) {
      assert.equal(parseListParams(new URL(`http://x/movies?plex=${value}`)).plex, value);
    }
  });

  it('should treat a missing or unknown plex value as no filter', () => {
    assert.equal(parseListParams(new URL('http://x/movies')).plex, null);
    assert.equal(parseListParams(new URL('http://x/movies?plex=bogus')).plex, null);
    assert.equal(parseListParams(new URL('http://x/movies?plex=')).plex, null);
  });

  it('should carry the plex filter with search, sort and page in list hrefs', () => {
    assert.equal(
      listHref('/movies', { searchText: 'a', orderBy: 'title:desc', page: 2, plex: 'unwatched' }),
      '/movies?searchText=a&orderBy=title%3Adesc&plex=unwatched&page=2'
    );
    assert.equal(listHref('/movies', { plex: null }), '/movies');
    assert.equal(listHref('/movies', { plex: 'broken' }), '/movies?plex=broken');
  });
});

describe('movie list with Plex', () => {
  it('should show a Plex icon with each state on the rows, mobile and table', async (t) => {
    stubApi(t, { plex: enabled });
    const html = await (await get('/movies')).text();

    // Four linked movies, once in the mobile list and once in the table; the unlinked one has none
    assert.equal(iconCount(html), 8);
    assert.match(html, /data-plex-icon="onPlex"/);
    assert.match(html, /aria-label="On Plex, watched"/);
    assert.match(html, /aria-label="No longer on Plex"/);
    assert.match(html, /aria-label="Plex link is broken"/);
    assert.match(html, /data-plex-badge="watched"/);
    assert.match(html, /data-plex-badge="warning"/);
  });

  it('should render no icon for an unlinked movie', async (t) => {
    stubApi(t, { plex: enabled, items: [movies[4]] });
    const html = await (await get('/movies')).text();
    assert.equal(iconCount(html), 0);
    assert.match(html, /Eraserhead/);
  });

  it('should render a filter control beside the search', async (t) => {
    stubApi(t, { plex: enabled });
    const html = await (await get('/movies')).text();

    assert.match(html, /<select[^>]*name="plex"/);
    for (const label of ['All movies', 'On Plex', 'Not on Plex', 'Unwatched on Plex', 'Broken link']) {
      assert.match(html, new RegExp(`>${label}<`));
    }
  });

  it('should pass the plex filter to the paged search and select it', async (t) => {
    const calls = stubApi(t, { plex: enabled });
    const html = await (await get('/movies?plex=unwatched')).text();

    assert.equal(calls[0].pathname, '/Movies/pagedsearch');
    assert.equal(calls[0].searchParams.get('plex'), 'unwatched');
    assert.match(html, /<option value="unwatched" selected/);
  });

  it('should not send a plex param without a filter', async (t) => {
    const calls = stubApi(t, { plex: enabled });
    await get('/movies');
    assert.equal(calls[0].searchParams.has('plex'), false);
  });

  it('should keep the filter in search, sort, paging and clear links', async (t) => {
    stubApi(t, { plex: enabled, totalPages: 3 });
    const html = await (await get('/movies?plex=broken&searchText=du&page=2')).text();

    assert.match(html, /href="\/movies\?searchText=du&amp;orderBy=format%3Aasc&amp;plex=broken"/);
    assert.match(html, /href="\/movies\?searchText=du&amp;orderBy=title%3Aasc&amp;plex=broken&amp;page=2"/);
    assert.match(html, /href="\/movies\?plex=broken" aria-label="Clear search"/);
  });

  it('should offer no filter and no icons when Plex is off', async (t) => {
    const calls = stubApi(t, { plex: { enabled: false, entityTypes: ['movies'] } });
    const html = await (await get('/movies?plex=onPlex')).text();

    assert.equal(iconCount(html), 0);
    assert.doesNotMatch(html, /name="plex"/);
    assert.equal(calls[0].searchParams.has('plex'), false);
  });

  it('should offer no filter and no icons when Plex has no movies enabled', async (t) => {
    stubApi(t, { plex: { enabled: true, entityTypes: ['music'] } });
    const html = await (await get('/movies')).text();
    assert.equal(iconCount(html), 0);
    assert.doesNotMatch(html, /name="plex"/);
  });

  it('should render nothing for Plex when the integration is absent', async (t) => {
    stubApi(t, {});
    const html = await (await get('/movies')).text();
    assert.equal(iconCount(html), 0);
    assert.doesNotMatch(html, /name="plex"/);
  });

  it('should not render Plex UI or filter on other lists', async (t) => {
    const calls = stubApi(t, {
      plex: enabled,
      items: [{ id: 'b1', type: 'Books', title: 'Dune', plex: { onPlex: true, broken: false, watched: true } }],
    });
    const html = await (await get('/books?plex=onPlex')).text();

    assert.equal(iconCount(html), 0);
    assert.doesNotMatch(html, /name="plex"/);
    assert.equal(calls[0].searchParams.has('plex'), false);
  });

  it('should say no movies match when the filter finds none', async (t) => {
    stubApi(t, { plex: enabled, items: [] });
    const html = await (await get('/movies?plex=broken')).text();
    assert.match(html, /No Movies match\s*this Plex filter\./);
  });

  it('should say no movies match the search with the filter when both are set', async (t) => {
    stubApi(t, { plex: enabled, items: [] });
    const html = await (await get('/movies?plex=broken&searchText=zzz')).text();
    assert.match(html, /No Movies match\s*"zzz"\s*with this Plex filter\./);
  });

  it('should look the gate up once per request', async (t) => {
    const original = globalThis.fetch;
    let lookups = 0;
    globalThis.fetch = (async (input: RequestInfo | URL) => {
      const url = new URL(input instanceof Request ? input.url : String(input));
      if (url.pathname === '/config/integrations') {
        lookups++;
        return Response.json([{ key: 'plex', displayName: 'Plex', ...enabled }]);
      }
      return Response.json({ items: movies, totalCount: 5, page: 1, pageSize: 75, totalPages: 1 });
    }) as typeof fetch;
    t.after(() => {
      globalThis.fetch = original;
    });

    await get('/movies');
    assert.equal(lookups, 1);
  });
});
