import * as assert from 'remix/assert';
import { describe, it } from 'remix/test';

import type { PlexMusicItem, PlexSearchResult } from '../../models.ts';
import { router } from '../../router.ts';

const get = (path: string) => router.fetch(new URL(path, 'http://localhost'));
const post = (path: string, fields: Record<string, string> = {}) =>
  router.fetch(
    new Request(new URL(path, 'http://localhost'), {
      method: 'POST',
      headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
      body: new URLSearchParams(fields).toString(),
    })
  );

interface Call {
  method: string;
  path: string;
  search: string;
  body: unknown;
}

interface StubOptions {
  plex?: { enabled: boolean; entityTypes?: readonly string[] };
  /** The album's fields; `plex` makes it linked. */
  album?: { title?: string; plex?: Partial<PlexMusicItem> };
  results?: PlexSearchResult[];
  search?: Response;
  link?: Response;
  unlink?: Response;
}

const enabled = { enabled: true, entityTypes: ['music'] };

// Stubs the API and records every call.
function stubApi(t: { after: (fn: () => void) => void }, options: StubOptions = {}) {
  const original = globalThis.fetch;
  const calls: Call[] = [];
  globalThis.fetch = (async (input: RequestInfo | URL, init?: RequestInit) => {
    const request = new Request(input, init);
    const url = new URL(request.url);
    const text = request.method === 'GET' ? '' : await request.text();
    calls.push({
      method: request.method,
      path: url.pathname,
      search: url.search,
      body: text ? JSON.parse(text) : null,
    });
    if (url.pathname.endsWith('/config/integrations')) {
      return Response.json(options.plex ? [{ key: 'plex', displayName: 'Plex', ...options.plex }] : []);
    }
    if (url.pathname.endsWith('/plex/music/search')) return options.search ?? Response.json(options.results ?? []);
    if (url.pathname.endsWith('/link')) {
      return request.method === 'PUT'
        ? (options.link ?? Response.json({}))
        : (options.unlink ?? new Response(null, { status: 204 }));
    }
    if (url.pathname.endsWith('/Musics/a1')) {
      const album = options.album ?? {};
      return Response.json({
        id: 'a1',
        type: 'Musics',
        title: album.title ?? 'Abbey Road',
        artist: 'The Beatles',
        ...(album.plex
          ? {
              plex: {
                musicId: 'a1',
                plexItemId: '1',
                onPlex: true,
                broken: false,
                linkSource: 'manual',
                playCount: 0,
                library: 'Music',
                lastSyncedAt: new Date().toISOString(),
                ...album.plex,
              },
            }
          : {}),
      });
    }
    return Response.json([]);
  }) as typeof fetch;
  t.after(() => {
    globalThis.fetch = original;
  });
  return calls;
}

const problem = (status: number, title: string) =>
  new Response(JSON.stringify({ title, status }), { status, headers: { 'Content-Type': 'application/problem+json' } });

const album = (title: string, plexItemId: string, artist: string | null = 'The Beatles'): PlexSearchResult => ({
  title,
  plexItemId,
  year: 1969,
  library: 'Music',
  type: 'album',
  artist,
});

const apiCalls = (calls: Call[], method: string, suffix: string) =>
  calls.filter((call) => call.method === method && call.path.endsWith(suffix));

describe('music Plex link flow', () => {
  it('should search the music routes with the album title and show each result with its artist', async (t) => {
    const calls = stubApi(t, {
      plex: enabled,
      results: [album('Abbey Road', '7001'), album('Abbey Road', '7002', 'Other')],
    });
    const response = await get('/musics/a1/plex/link');
    const html = await response.text();

    assert.equal(response.status, 200);
    assert.equal(apiCalls(calls, 'GET', '/plex/music/search').length, 1);
    assert.equal(
      new URLSearchParams(apiCalls(calls, 'GET', '/plex/music/search')[0].search).get('query'),
      'Abbey Road'
    );
    assert.match(html, /Search Plex for an album/);
    assert.match(html, /data-plex-artist[^>]*>\s*The Beatles/);
    assert.match(html, /data-plex-artist[^>]*>\s*Other/);
    assert.match(
      html,
      /<form method="post" action="\/musics\/a1\/plex\/link"><input type="hidden" name="plexItemId" value="7001"/
    );
    assert.doesNotMatch(html, /Which seasons/);
  });

  it('should render the bare flow for the dialog with fragment=1', async (t) => {
    stubApi(t, { plex: enabled, results: [album('Abbey Road', '7001')] });
    const html = await (await get('/musics/a1/plex/link?fragment=1')).text();
    assert.match(html, /data-plex-link="fragment"/);
    assert.doesNotMatch(html, /<html/);
  });

  it('should say when nothing matches', async (t) => {
    stubApi(t, { plex: enabled, results: [] });
    assert.match(await (await get('/musics/a1/plex/link?query=zzz')).text(), /No matches on Plex/);
  });

  it('should link the chosen album and redirect 303 to the detail page', async (t) => {
    const calls = stubApi(t, { plex: enabled });
    const response = await post('/musics/a1/plex/link', { plexItemId: '7001' });
    assert.equal(response.status, 303);
    assert.equal(response.headers.get('Location'), '/musics/a1');
    const puts = apiCalls(calls, 'PUT', '/plex/music/items/a1/link');
    assert.equal(puts.length, 1);
    assert.deepEqual(puts[0].body, { plexItemId: '7001' });
  });

  it('should link a lone match at once on the first link', async (t) => {
    const calls = stubApi(t, { plex: enabled, results: [album('Abbey Road', '7001')] });
    const response = await post('/musics/a1/plex/link', { intent: 'auto' });
    assert.equal(response.status, 303);
    assert.equal(response.headers.get('Location'), '/musics/a1');
    assert.deepEqual(apiCalls(calls, 'PUT', '/plex/music/items/a1/link')[0].body, { plexItemId: '7001' });
  });

  it('should go to the search when the first link finds several albums', async (t) => {
    const calls = stubApi(t, { plex: enabled, results: [album('Abbey Road', '7001'), album('Abbey Road', '7002')] });
    const response = await post('/musics/a1/plex/link', { intent: 'auto' });
    assert.equal(response.status, 303);
    assert.equal(response.headers.get('Location'), '/musics/a1/plex/link');
    assert.equal(apiCalls(calls, 'PUT', '/link').length, 0);
  });

  it('should answer 400 and link nothing when no album was chosen', async (t) => {
    const calls = stubApi(t, { plex: enabled });
    const response = await post('/musics/a1/plex/link');
    assert.equal(response.status, 400);
    assert.equal(apiCalls(calls, 'PUT', '/link').length, 0);
  });

  it('should answer 404 and call Plex not at all when Plex is not enabled for music', async (t) => {
    const calls = stubApi(t, { plex: { enabled: true, entityTypes: ['movies'] } });
    assert.equal((await get('/musics/a1/plex/link')).status, 404);
    assert.equal((await post('/musics/a1/plex/link', { plexItemId: '7001' })).status, 404);
    assert.equal(calls.filter((call) => call.path.includes('/plex/')).length, 0);
  });

  for (const [name, response, status, message] of [
    ['Plex is unreachable', problem(502, 'Plex request failed'), 502, /Couldn.t reach Plex/],
    ['a sync is running', problem(409, 'Sync in progress'), 409, /sync is already in progress/],
    ['the album is gone from Plex', problem(404, 'Plex item not found'), 404, /Plex no longer has the item/],
    ['the album entry is gone', problem(404, 'Music not found'), 404, /couldn't find that album/],
    ['the API fails otherwise', problem(500, 'Boom'), 500, /Something went wrong while linking to Plex/],
  ] as const) {
    it(`should render the entity error page when linking fails because ${name}`, async (t) => {
      stubApi(t, { plex: enabled, link: response });
      const result = await post('/musics/a1/plex/link', { plexItemId: '7001' });
      const html = await result.text();
      assert.equal(result.status, status);
      assert.match(html, message);
      assert.match(html, /href="\/musics\/a1"/);
    });
  }

  it('should render the error page when the search fails', async (t) => {
    stubApi(t, { plex: enabled, search: problem(502, 'Plex request failed') });
    const result = await get('/musics/a1/plex/link');
    assert.equal(result.status, 502);
    assert.match(await result.text(), /Couldn.t reach Plex/);
  });
});

describe('music Plex unlink flow', () => {
  it('should show the confirmation page for a linked album without calling Plex', async (t) => {
    const calls = stubApi(t, { plex: enabled, album: { plex: {} } });
    const response = await get('/musics/a1/plex/unlink');
    const html = await response.text();
    assert.equal(response.status, 200);
    assert.match(html, /Unlink <strong>Abbey Road<\/strong> from Plex\?/);
    assert.match(html, /The album itself is kept/);
    assert.match(html, /<form method="post" action="\/musics\/a1\/plex\/unlink"/);
    assert.equal(apiCalls(calls, 'DELETE', '/link').length, 0);
  });

  it('should answer the not-linked error for an unlinked album', async (t) => {
    stubApi(t, { plex: enabled });
    const response = await get('/musics/a1/plex/unlink');
    assert.equal(response.status, 404);
    assert.match(await response.text(), /This album is not linked to Plex/);
  });

  it('should unlink through the music route then redirect 303 to the detail page', async (t) => {
    const calls = stubApi(t, { plex: enabled, album: { plex: {} } });
    const response = await post('/musics/a1/plex/unlink');
    assert.equal(response.status, 303);
    assert.equal(response.headers.get('Location'), '/musics/a1');
    assert.equal(apiCalls(calls, 'DELETE', '/plex/music/items/a1/link').length, 1);
  });

  it('should answer 404 and unlink nothing when Plex is not enabled for music', async (t) => {
    const calls = stubApi(t, { plex: { enabled: true, entityTypes: ['movies'] } });
    assert.equal((await get('/musics/a1/plex/unlink')).status, 404);
    assert.equal((await post('/musics/a1/plex/unlink')).status, 404);
    assert.equal(apiCalls(calls, 'DELETE', '/link').length, 0);
  });

  for (const [name, response, status, message] of [
    ['the album is not linked', problem(404, 'Not linked to Plex'), 404, /nothing to unlink/],
    ['the album is gone', problem(404, 'Music not found'), 404, /couldn't find that album/],
    ['the API fails otherwise', problem(500, 'Boom'), 500, /Something went wrong while removing the Plex link/],
  ] as const) {
    it(`should render the entity error page when ${name}`, async (t) => {
      stubApi(t, { plex: enabled, unlink: response });
      const result = await post('/musics/a1/plex/unlink');
      const html = await result.text();
      assert.equal(result.status, status);
      assert.match(html, message);
      assert.match(html, /href="\/musics\/a1"/);
    });
  }
});
