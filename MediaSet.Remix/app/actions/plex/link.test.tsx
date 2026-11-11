import * as assert from 'remix/assert';
import { describe, it } from 'remix/test';

import type { PlexItem, PlexScope, PlexSearchResult } from '../../models.ts';
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
  /** The entry's fields; `plex` makes it linked. */
  movie?: { title?: string; isTvSeries?: boolean; plex?: Partial<PlexItem> };
  results?: PlexSearchResult[];
  /** What the scope endpoint answers (a Response for a failure). */
  scope?: PlexScope | Response;
  /** The answer to the search, link and unlink calls when not the default. */
  search?: Response;
  link?: Response;
  unlink?: Response;
}

const enabled = { enabled: true, entityTypes: ['movies'] };

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
    if (url.pathname.endsWith('/plex/movies/search')) return options.search ?? Response.json(options.results ?? []);
    if (url.pathname.endsWith('/scope')) {
      return options.scope instanceof Response ? options.scope : Response.json(options.scope ?? { kind: 'unreadable' });
    }
    if (url.pathname.endsWith('/link')) {
      return request.method === 'PUT'
        ? (options.link ?? Response.json({}))
        : (options.unlink ?? new Response(null, { status: 204 }));
    }
    if (url.pathname.endsWith('/Movies/m1')) {
      const movie = options.movie ?? {};
      return Response.json({
        id: 'm1',
        type: 'Movies',
        title: movie.title ?? 'Heat',
        isTvSeries: movie.isTvSeries ?? false,
        ...(movie.plex
          ? {
              plex: {
                movieId: 'm1',
                plexItemId: '1',
                onPlex: true,
                linkSource: 'manual',
                broken: false,
                subtitleLanguages: [],
                watched: false,
                viewCount: 0,
                lastSyncedAt: new Date().toISOString(),
                library: 'Movies',
                ...movie.plex,
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

const result = (title: string, plexItemId: string, type = 'movie', year: number | null = 1995): PlexSearchResult => ({
  title,
  plexItemId,
  year,
  library: type === 'show' ? 'TV Shows' : 'Movies',
  type,
});

const apiCalls = (calls: Call[], method: string, suffix: string) =>
  calls.filter((call) => call.method === method && call.path.endsWith(suffix));

describe('plex link controls on the detail page', () => {
  it('should offer a working Link to Plex form for an unlinked movie', async (t) => {
    stubApi(t, { plex: enabled });
    const html = await (await get('/movies/m1')).text();
    assert.match(
      html,
      /<form method="post" action="\/movies\/m1\/plex\/link"><input type="hidden" name="intent" value="auto"/
    );
    assert.match(html, /Link to Plex/);
    assert.doesNotMatch(html, /aria-disabled|not available yet/);
  });

  it('should offer Relink and Unlink on a linked movie', async (t) => {
    stubApi(t, { plex: enabled, movie: { plex: {} } });
    const html = await (await get('/movies/m1')).text();
    assert.match(html, /<form method="get" action="\/movies\/m1\/plex\/link">/);
    assert.doesNotMatch(html, /name="intent" value="auto"/);
    assert.match(html, /<form method="get" action="\/movies\/m1\/plex\/unlink">/);
    assert.match(html, />\s*Relink\s*</);
    assert.match(html, />\s*Unlink\s*</);
  });

  it('should keep Relink and Unlink in the Plex menu when the link is broken', async (t) => {
    stubApi(t, { plex: enabled, movie: { plex: { onPlex: false, broken: true } } });
    const html = await (await get('/movies/m1')).text();
    const menu = html.match(/<details[^>]*data-testid="plex-menu"[\s\S]*?<\/details>/)?.[0] ?? '';
    assert.match(menu, /<summary[^>]*aria-label="Plex actions"/);
    assert.match(menu, /action="\/movies\/m1\/plex\/refresh"/);
    assert.match(menu, />\s*Relink\s*</);
    assert.match(menu, />\s*Unlink\s*</);
    assert.doesNotMatch(html, /aria-disabled|not available yet/);
  });

  it('should put Refresh, Relink and Unlink in a menu beside the Plex heading, with no border', async (t) => {
    stubApi(t, { plex: enabled, movie: { plex: {} } });
    const html = await (await get('/movies/m1')).text();
    const menu = html.match(/<details[^>]*data-testid="plex-menu"[\s\S]*?<\/details>/)?.[0] ?? '';
    for (const label of ['Refresh', 'Relink', 'Unlink']) assert.match(menu, new RegExp(`>\\s*${label}\\s*<`));
    const section = html.match(/<section[^>]*data-testid="plex-card"[^>]*>/)?.[0] ?? '';
    assert.doesNotMatch(section, /\bborder\b/);
  });
});

describe('plex link search step', () => {
  it('should answer 404 and never call Plex when Plex is off', async (t) => {
    const calls = stubApi(t, { plex: { enabled: false, entityTypes: ['movies'] } });
    const response = await get('/movies/m1/plex/link');
    assert.equal(response.status, 404);
    assert.equal(apiCalls(calls, 'GET', '/search').length, 0);
  });

  it('should search with the movie title prefilled and list title, year and library', async (t) => {
    const calls = stubApi(t, {
      plex: enabled,
      results: [result('Heat', '11'), result('Heat (Extended)', '12', 'movie', null)],
    });
    const html = await (await get('/movies/m1/plex/link')).text();

    assert.equal(apiCalls(calls, 'GET', '/search')[0]?.search, '?query=Heat');
    assert.match(html, /<h1[^>]*>Link to Plex: Heat<\/h1>/);
    assert.match(html, /<input[^>]*id="plex-query"[^>]*value="Heat"/);
    assert.match(html, /Heat \(1995\)/);
    assert.match(html, /Heat \(Extended\)/);
    assert.match(html, /Movies/);
    // Without JavaScript each result is a POST form to the link action
    assert.match(html, /<form method="post" action="\/movies\/m1\/plex\/link">/);
    assert.match(html, /name="plexItemId" value="11"/);
  });

  it('should be titled Relink for a linked entry', async (t) => {
    stubApi(t, { plex: enabled, movie: { plex: {} } });
    const html = await (await get('/movies/m1/plex/link')).text();
    assert.match(html, /Relink to Plex: Heat/);
  });

  it('should search the typed query instead of the title', async (t) => {
    const calls = stubApi(t, { plex: enabled });
    await get('/movies/m1/plex/link?query=Ronin');
    assert.equal(apiCalls(calls, 'GET', '/search')[0]?.search, '?query=Ronin');
  });

  it('should list only movies for a movie entry', async (t) => {
    stubApi(t, {
      plex: enabled,
      results: [result('Heat', '11'), result('Heat 2', '13'), result('Heat Show', '12', 'show')],
    });
    const html = await (await get('/movies/m1/plex/link')).text();
    assert.match(html, /value="11"/);
    assert.match(html, /value="13"/);
    assert.doesNotMatch(html, /value="12"/);
  });

  it('should use the secondary style for Cancel, in the page and in the dialog', async (t) => {
    stubApi(t, { plex: enabled, results: [result('Heat', '11'), result('Heat 2', '13')] });
    const page = await (await get('/movies/m1/plex/link')).text();
    assert.match(page, /<form method="get" action="\/movies\/m1"><button type="submit" class="secondary">Cancel/);
    const fragment = await (await get('/movies/m1/plex/link?fragment=1')).text();
    assert.match(
      fragment,
      /<button[^>]*class="secondary"[^>]*data-plex-close|<button[^>]*data-plex-close[^>]*class="secondary"/
    );
  });

  it('should say so when nothing matches', async (t) => {
    stubApi(t, { plex: enabled, results: [] });
    const html = await (await get('/movies/m1/plex/link')).text();
    assert.match(html, /No matches on Plex for “Heat”/);
  });

  it('should not search for a query shorter than two characters', async (t) => {
    const calls = stubApi(t, { plex: enabled });
    const html = await (await get('/movies/m1/plex/link?query=H')).text();
    assert.equal(apiCalls(calls, 'GET', '/search').length, 0);
    assert.match(html, /at least 2 characters/);
  });

  it('should render only the flow, without the page shell, for the dialog', async (t) => {
    stubApi(t, { plex: enabled, results: [result('Heat', '11'), result('Heat 2', '13')] });
    const html = await (await get('/movies/m1/plex/link?fragment=1')).text();
    assert.doesNotMatch(html, /<html|<body|<nav/);
    assert.match(html, /data-plex-link="fragment"/);
    assert.match(html, /<h2 id="plex-link-heading"/);
    assert.match(html, /data-plex-close/);
  });

  it('should render the entity error page when Plex is unreachable', async (t) => {
    stubApi(t, { plex: enabled, search: problem(502, 'Plex request failed') });
    const response = await get('/movies/m1/plex/link');
    const html = await response.text();
    assert.equal(response.status, 502);
    assert.match(html, /could not reach your Plex server/);
    assert.match(html, /href="\/movies\/m1"/);
  });
});

describe('plex automatic link on the first Link', () => {
  const tv = { title: 'Show: Season 2', isTvSeries: true };
  const linkCalls = (calls: Call[]) => apiCalls(calls, 'PUT', '/plex/movies/items/m1/link');
  const auto = (query = '') => post(`/movies/m1/plex/link${query}`, { intent: 'auto' });

  it('should never link on a GET, however many matches there are', async (t) => {
    const calls = stubApi(t, { plex: enabled, results: [result('Heat', '11')] });
    for (const path of ['/movies/m1/plex/link', '/movies/m1/plex/link?query=Heat', '/movies/m1/plex/link?fragment=1']) {
      assert.equal((await get(path)).status, 200);
    }
    assert.equal(linkCalls(calls).length, 0);
  });

  it('should link the only match and redirect 303 to the detail page', async (t) => {
    const calls = stubApi(t, { plex: enabled, results: [result('Heat', '11')] });
    const response = await auto();
    assert.equal(response.status, 303);
    assert.equal(response.headers.get('Location'), '/movies/m1');
    assert.equal(apiCalls(calls, 'GET', '/search')[0]?.search, '?query=Heat');
    assert.deepEqual(
      linkCalls(calls).map((call) => call.body),
      [{ plexItemId: '11' }]
    );
  });

  it('should answer the dialog form with the same redirect', async (t) => {
    const calls = stubApi(t, { plex: enabled, results: [result('Heat', '11')] });
    const response = await auto('?fragment=1');
    assert.equal(response.status, 303);
    assert.equal(linkCalls(calls).length, 1);
  });

  it('should count only matches of the entry kind', async (t) => {
    const calls = stubApi(t, { plex: enabled, results: [result('Heat', '11'), result('Heat Show', '12', 'show')] });
    assert.equal((await auto()).status, 303);
    assert.deepEqual(
      linkCalls(calls).map((call) => call.body),
      [{ plexItemId: '11' }]
    );
  });

  it('should redirect to the search page and link nothing for no match or several', async (t) => {
    for (const results of [[], [result('Heat', '11'), result('Heat 2', '12')]]) {
      const calls = stubApi(t, { plex: enabled, results });
      const response = await auto();
      assert.equal(response.status, 303);
      assert.equal(response.headers.get('Location'), '/movies/m1/plex/link');
      assert.equal(linkCalls(calls).length, 0);
    }
  });

  it('should show the results in place for the dialog when there is no single match', async (t) => {
    const calls = stubApi(t, { plex: enabled, results: [result('Heat', '11'), result('Heat 2', '12')] });
    const response = await auto('?fragment=1');
    const html = await response.text();
    assert.equal(response.status, 200);
    assert.match(html, /data-plex-link="fragment"/);
    assert.match(html, /Heat 2 \(1995\)/);
    assert.equal(linkCalls(calls).length, 0);
  });

  it('should never auto-apply a relink of an already linked movie', async (t) => {
    const calls = stubApi(t, { plex: enabled, movie: { plex: {} }, results: [result('Heat', '11')] });
    const response = await auto();
    assert.equal(response.status, 303);
    assert.equal(response.headers.get('Location'), '/movies/m1/plex/link');
    assert.equal(linkCalls(calls).length, 0);
    assert.equal(apiCalls(calls, 'GET', '/search').length, 0);
    // The relink page itself shows the search box and the lone result
    const html = await (await get('/movies/m1/plex/link')).text();
    assert.match(html, /id="plex-query"/);
    assert.match(html, /Heat \(1995\)/);
  });

  it('should not search when the movie has no usable title', async (t) => {
    const calls = stubApi(t, { plex: enabled, movie: { title: ' ' }, results: [result('Heat', '11')] });
    const response = await auto();
    assert.equal(response.status, 303);
    assert.equal(response.headers.get('Location'), '/movies/m1/plex/link');
    assert.equal(apiCalls(calls, 'GET', '/search').length, 0);
    assert.equal(linkCalls(calls).length, 0);
  });

  it('should show the search step to the dialog, not redirect, for a blank title', async (t) => {
    const calls = stubApi(t, { plex: enabled, movie: { title: 'A' }, results: [result('A', '11')] });
    const response = await auto('?fragment=1');
    const html = await response.text();
    assert.equal(response.status, 200);
    assert.match(html, /data-plex-link="fragment"/);
    assert.match(html, /at least 2 characters/);
    assert.equal(linkCalls(calls).length, 0);
  });

  it('should show the search step to the dialog for a movie already linked elsewhere', async (t) => {
    const calls = stubApi(t, { plex: enabled, movie: { plex: {} }, results: [result('Heat', '11')] });
    const response = await auto('?fragment=1');
    const html = await response.text();
    assert.equal(response.status, 200);
    assert.match(html, /Relink to Plex: Heat/);
    assert.match(html, /Heat \(1995\)/);
    assert.equal(linkCalls(calls).length, 0);
  });

  it('should skip the pick step but still ask for the seasons of a TV entry', async (t) => {
    const calls = stubApi(t, {
      plex: enabled,
      movie: tv,
      results: [result('Show', '2001', 'show', 2010)],
      scope: { kind: 'season', from: 2, to: 2 },
    });
    const response = await auto();
    assert.equal(response.status, 303);
    assert.equal(
      response.headers.get('Location'),
      '/movies/m1/plex/link?plexItemId=2001&plexTitle=Show+%282010%29&query=Show%3A+Season+2'
    );
    assert.equal(linkCalls(calls).length, 0);

    const fragment = await (await auto('?fragment=1')).text();
    assert.match(fragment, /Linking to <strong>Show \(2010\)<\/strong>/);
    assert.match(fragment, /id="season"[^>]*value="2"/);
    assert.equal(linkCalls(calls).length, 0);
  });

  for (const [name, response, status, message] of [
    ['Plex refuses the link', problem(502, 'Plex request failed'), 502, /could not reach your Plex server/],
    ['the API fails', problem(500, 'Boom'), 500, /Something went wrong while linking/],
  ] as const) {
    it(`should render the standard error page when ${name}`, async (t) => {
      stubApi(t, { plex: enabled, results: [result('Heat', '11')], link: response });
      const result_ = await auto();
      assert.equal(result_.status, status);
      assert.match(await result_.text(), message);
    });
  }

  it('should render the error page when the search fails', async (t) => {
    stubApi(t, { plex: enabled, search: problem(502, 'Plex request failed') });
    assert.equal((await auto()).status, 502);
  });
});

describe('plex link scope step for a TV entry', () => {
  const tv = { title: 'Show: Season 2', isTvSeries: true };
  const choose = '/movies/m1/plex/link?plexItemId=2001&plexTitle=Show%20(2010)&query=Show';

  it('should list only shows, each leading to the scope step', async (t) => {
    stubApi(t, {
      plex: enabled,
      movie: tv,
      results: [result('Show', '2001', 'show'), result('Show 2', '2002', 'show'), result('Show Movie', '9')],
    });
    const html = await (await get('/movies/m1/plex/link')).text();
    assert.match(html, /name="plexItemId" value="2001"/);
    assert.doesNotMatch(html, /value="9"/);
    assert.match(html, /<form method="get" action="\/movies\/m1\/plex\/link">/);
    assert.doesNotMatch(html, /<form method="post"/);
  });

  it('should prefill one season from the scope endpoint', async (t) => {
    const calls = stubApi(t, { plex: enabled, movie: tv, scope: { kind: 'season', from: 2, to: 2 } });
    const html = await (await get(choose)).text();
    assert.equal(apiCalls(calls, 'GET', '/scope').length, 1);
    assert.match(html, /Linking to <strong>Show \(2010\)<\/strong>/);
    assert.match(html, /value="season" checked/);
    assert.match(html, /id="season"[^>]*value="2"/);
  });

  it('should prefill a range from the scope endpoint', async (t) => {
    stubApi(t, { plex: enabled, movie: tv, scope: { kind: 'range', from: 1, to: 3 } });
    const html = await (await get(choose)).text();
    assert.match(html, /value="range" checked/);
    assert.match(html, /id="from"[^>]*value="1"/);
    assert.match(html, /id="to"[^>]*value="3"/);
  });

  for (const [name, scope] of [
    ['the title is unreadable', { kind: 'unreadable' }],
    ['the title says the complete series', { kind: 'all' }],
    ['the scope endpoint fails', problem(500, 'Boom')],
  ] as const) {
    it(`should fall back to Complete series when ${name}`, async (t) => {
      stubApi(t, { plex: enabled, movie: tv, scope });
      const html = await (await get(choose)).text();
      assert.match(html, /value="all" checked/);
    });
  }

  it('should not show the scope step for a movie entry', async (t) => {
    const calls = stubApi(t, { plex: enabled, results: [result('Heat', '11')] });
    const html = await (await get('/movies/m1/plex/link?plexItemId=2001')).text();
    assert.equal(apiCalls(calls, 'GET', '/scope').length, 0);
    assert.doesNotMatch(html, /Seasons covered/);
  });
});

describe('plex link submit', () => {
  const tv = { title: 'Show: Season 2', isTvSeries: true };
  const linkCalls = (calls: Call[]) => apiCalls(calls, 'PUT', '/plex/movies/items/m1/link');

  it('should link a movie then redirect 303 to the detail page', async (t) => {
    const calls = stubApi(t, { plex: enabled });
    const response = await post('/movies/m1/plex/link', { plexItemId: '11' });
    assert.equal(response.status, 303);
    assert.equal(response.headers.get('Location'), '/movies/m1');
    assert.deepEqual(
      linkCalls(calls).map((call) => call.body),
      [{ plexItemId: '11' }]
    );
  });

  for (const [name, fields, scope] of [
    ['one season', { scopeKind: 'season', season: '2' }, { kind: 'season', season: 2 }],
    ['a range', { scopeKind: 'range', from: '1', to: '3' }, { kind: 'range', from: 1, to: 3 }],
    ['a range of one season', { scopeKind: 'range', from: '2', to: '2' }, { kind: 'season', season: 2 }],
    ['the complete series', { scopeKind: 'all', season: '7' }, { kind: 'all' }],
  ] as const) {
    it(`should link a TV entry with ${name}`, async (t) => {
      const calls = stubApi(t, { plex: enabled, movie: tv });
      const response = await post('/movies/m1/plex/link', { plexItemId: '2001', ...fields });
      assert.equal(response.status, 303);
      assert.deepEqual(
        linkCalls(calls).map((call) => call.body),
        [{ plexItemId: '2001', scope }]
      );
    });
  }

  for (const [name, fields, message] of [
    ['no scope kind is chosen', {}, /Choose which seasons/],
    ['the season is missing', { scopeKind: 'season' }, /Enter the season number/],
    ['the season is too large', { scopeKind: 'season', season: '100' }, /from 0 to 99/],
    ['the range is reversed', { scopeKind: 'range', from: '3', to: '1' }, /must not be before the first/],
    ['the range is incomplete', { scopeKind: 'range', from: '1' }, /Enter the last season/],
  ] as const) {
    it(`should re-render the scope step with 400 and link nothing when ${name}`, async (t) => {
      const calls = stubApi(t, { plex: enabled, movie: tv });
      const response = await post('/movies/m1/plex/link', {
        plexItemId: '2001',
        plexTitle: 'Show (2010)',
        ...fields,
      });
      const html = await response.text();
      assert.equal(response.status, 400);
      assert.match(html, message);
      assert.match(html, /aria-invalid|id="scopeKind-error"/);
      assert.match(html, /Linking to <strong>Show \(2010\)<\/strong>/);
      assert.equal(linkCalls(calls).length, 0);
    });
  }

  it('should ignore a stale invalid season when another scope is chosen', async (t) => {
    const calls = stubApi(t, { plex: enabled, movie: tv });
    const response = await post('/movies/m1/plex/link', {
      plexItemId: '2001',
      scopeKind: 'all',
      season: '150',
      from: 'x',
    });
    assert.equal(response.status, 303);
    assert.deepEqual(
      linkCalls(calls).map((call) => call.body),
      [{ plexItemId: '2001', scope: { kind: 'all' } }]
    );
  });

  it('should render only the scope step, for the dialog, when a dialog post fails validation', async (t) => {
    stubApi(t, { plex: enabled, movie: tv });
    const response = await post('/movies/m1/plex/link?fragment=1', { plexItemId: '2001', scopeKind: 'season' });
    const html = await response.text();
    assert.equal(response.status, 400);
    assert.doesNotMatch(html, /<html|<body/);
    assert.match(html, /data-plex-link="fragment"/);
    assert.match(html, /Enter the season number/);
  });

  it('should answer 400 when no Plex item was chosen', async (t) => {
    const calls = stubApi(t, { plex: enabled });
    const response = await post('/movies/m1/plex/link', {});
    assert.equal(response.status, 400);
    assert.equal(linkCalls(calls).length, 0);
  });

  it('should answer 404 and link nothing when Plex is off', async (t) => {
    const calls = stubApi(t, { plex: { enabled: false } });
    const response = await post('/movies/m1/plex/link', { plexItemId: '11' });
    assert.equal(response.status, 404);
    assert.equal(linkCalls(calls).length, 0);
  });

  for (const [name, response, status, message] of [
    ['Plex is unreachable', problem(502, 'Plex request failed'), 502, /could not reach your Plex server/],
    ['the item is missing on Plex', problem(404, 'Plex item not found'), 404, /Plex no longer has the item you picked/],
    ['a sync is running', problem(409, 'Plex sync already in progress'), 409, /sync is already in progress/],
    ['the movie is gone', problem(404, 'Movie not found'), 404, /couldn't find that movie/],
    ['the API fails otherwise', problem(500, 'Boom'), 500, /Something went wrong while linking/],
  ] as const) {
    it(`should render the entity error page when ${name}`, async (t) => {
      stubApi(t, { plex: enabled, link: response });
      const result = await post('/movies/m1/plex/link', { plexItemId: '11' });
      const html = await result.text();
      assert.equal(result.status, status);
      assert.match(html, message);
      assert.match(html, /href="\/movies\/m1"/);
    });
  }
});

describe('plex unlink', () => {
  const deleteCalls = (calls: Call[]) => apiCalls(calls, 'DELETE', '/plex/movies/items/m1/link');

  it('should show a confirmation page without unlinking', async (t) => {
    const calls = stubApi(t, { plex: enabled, movie: { plex: {} } });
    const response = await get('/movies/m1/plex/unlink');
    const html = await response.text();
    assert.equal(response.status, 200);
    assert.match(html, /Unlink <strong>Heat<\/strong> from Plex\?/);
    assert.match(html, /deletes the Plex data stored for it/);
    assert.match(html, /<form method="post" action="\/movies\/m1\/plex\/unlink"/);
    const cancel = html.match(/<button[^>]*>\s*Cancel/)?.[0] ?? '';
    assert.match(cancel, /class="secondary"/);
    assert.match(cancel, /formaction="\/movies\/m1"/);
    assert.match(cancel, /formmethod="get"/);
    assert.doesNotMatch(html, /<a [^>]*>\s*Cancel/);
    assert.equal(deleteCalls(calls).length, 0);
  });

  it('should answer the not-linked error on the confirmation page for an unlinked movie', async (t) => {
    stubApi(t, { plex: enabled });
    const response = await get('/movies/m1/plex/unlink');
    assert.equal(response.status, 404);
    assert.match(await response.text(), /nothing to unlink/);
  });

  it('should unlink then redirect 303 to the detail page', async (t) => {
    const calls = stubApi(t, { plex: enabled, movie: { plex: {} } });
    const response = await post('/movies/m1/plex/unlink');
    assert.equal(response.status, 303);
    assert.equal(response.headers.get('Location'), '/movies/m1');
    assert.equal(deleteCalls(calls).length, 1);
  });

  it('should answer 404 and unlink nothing when Plex is off', async (t) => {
    const calls = stubApi(t, { plex: { enabled: false } });
    assert.equal((await get('/movies/m1/plex/unlink')).status, 404);
    assert.equal((await post('/movies/m1/plex/unlink')).status, 404);
    assert.equal(deleteCalls(calls).length, 0);
  });

  for (const [name, response, status, message] of [
    ['the movie is not linked', problem(404, 'Not linked to Plex'), 404, /nothing to unlink/],
    ['the movie is gone', problem(404, 'Movie not found'), 404, /couldn't find that movie/],
    ['the API fails otherwise', problem(500, 'Boom'), 500, /Something went wrong while removing the Plex link/],
  ] as const) {
    it(`should render the entity error page when ${name}`, async (t) => {
      stubApi(t, { plex: enabled, unlink: response });
      const result = await post('/movies/m1/plex/unlink');
      const html = await result.text();
      assert.equal(result.status, status);
      assert.match(html, message);
      assert.match(html, /href="\/movies\/m1"/);
    });
  }
});
