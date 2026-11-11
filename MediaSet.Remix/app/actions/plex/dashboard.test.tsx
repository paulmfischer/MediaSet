import * as assert from 'remix/assert';
import { describe, it } from 'remix/test';

import { router } from '../../router.ts';

const get = (path: string) => router.fetch(new URL(path, 'http://localhost'));

const enabled = [{ key: 'plex', displayName: 'Plex', enabled: true, entityTypes: ['movies'] }];

const emptyBreakdowns = { formatBreakdown: {}, decadeBreakdown: {}, genreBreakdown: {}, topStudios: [] };

const baseStats = {
  bookStats: { total: 0 },
  movieStats: { total: 10, totalFormats: 1, formats: ['DVD'], totalTvSeries: 2, ...emptyBreakdowns },
  gameStats: { total: 0 },
  musicStats: { total: 0 },
};

const plexStats = {
  onPlex: 6,
  notOnPlex: 4,
  watched: 2,
  unwatched: 4,
  resolutionBreakdown: { '1080': 3, '4k': 2, unknown: 1 },
  needsLinking: 3,
};

function stubApi(t: { after: (fn: () => void) => void }, stats: unknown, integrations: unknown = enabled) {
  const original = globalThis.fetch;
  globalThis.fetch = (async (input: RequestInfo | URL) => {
    const url = new URL(input instanceof Request ? input.url : String(input));
    if (url.pathname === '/stats') return Response.json(stats);
    if (url.pathname === '/config/integrations') return Response.json(integrations);
    return new Response('not found', { status: 404 });
  }) as typeof fetch;
  t.after(() => {
    globalThis.fetch = original;
  });
}

const moviesPanel = (html: string) =>
  html.match(/<div id="dashboard-panel-movies"[\s\S]*?(?=<div id="dashboard-panel-|$)/)?.[0] ?? '';

describe('dashboard Plex section', () => {
  it('should show the Plex stat cards on the Movies tab when Plex is enabled', async (t) => {
    stubApi(t, { ...baseStats, plexStats });

    const movies = moviesPanel(await (await get('/')).text());

    assert.match(movies, /<h3[^>]*>Plex<\/h3>/);
    for (const [title, value] of [
      ['On Plex', '6'],
      ['Not on Plex', '4'],
      ['Watched', '2'],
      ['Unwatched', '4'],
    ]) {
      assert.match(movies, new RegExp(`${title}</p><p class="[^"]*">${value}</p>`));
    }
  });

  it('should show the resolution breakdown as a pie chart', async (t) => {
    stubApi(t, { ...baseStats, plexStats });

    const movies = moviesPanel(await (await get('/')).text());

    assert.match(movies, /<svg[^>]*aria-label="Plex Resolution pie chart"/);
    assert.match(movies, /<title>1080: 3 · 50\.0%<\/title>/);
    assert.match(movies, /<title>4k: 2 · 33\.3%<\/title>/);
    assert.match(movies, /<title>Unknown: 1 · 16\.7%<\/title>/);
  });

  it('should show a Needs linking card with the count that links to the not-on-Plex list', async (t) => {
    stubApi(t, { ...baseStats, plexStats });

    const movies = moviesPanel(await (await get('/')).text());

    const card = movies.match(/<a[^>]*href="\/movies\?plex=notOnPlex"[^>]*>[\s\S]*?<\/a>/)?.[0] ?? '';
    assert.match(card, /Needs linking/);
    assert.match(card, />\s*(<svg[\s\S]*?<\/svg>)?\s*3\s*</);
  });

  it('should render no Plex section when Plex is not enabled', async (t) => {
    stubApi(t, baseStats, []);

    const html = await (await get('/')).text();

    assert.doesNotMatch(moviesPanel(html), /Plex/);
    assert.doesNotMatch(html, /Needs linking/);
  });

  it('should render no Plex section when Plex is enabled for other types only', async (t) => {
    stubApi(t, { ...baseStats, plexStats }, [{ key: 'plex', displayName: 'Plex', enabled: true, entityTypes: [] }]);

    assert.doesNotMatch(moviesPanel(await (await get('/')).text()), /Plex/);
  });

  it('should render no Plex section when the API sends no Plex statistics', async (t) => {
    stubApi(t, baseStats);

    assert.doesNotMatch(moviesPanel(await (await get('/')).text()), /Needs linking|dashboard-plex-heading/);
  });

  it('should show zero cards and no pie when nothing is on Plex', async (t) => {
    stubApi(t, {
      ...baseStats,
      plexStats: { onPlex: 0, notOnPlex: 10, watched: 0, unwatched: 0, resolutionBreakdown: {}, needsLinking: 10 },
    });

    const movies = moviesPanel(await (await get('/')).text());

    assert.match(movies, /On Plex<\/p><p class="[^"]*">0<\/p>/);
    assert.match(movies, /Not on Plex<\/p><p class="[^"]*">10<\/p>/);
    assert.doesNotMatch(movies, /Plex Resolution/);
    assert.match(movies, /<a[^>]*data-plex-needs-linking[^>]*sm:col-span-2/);
    assert.match(movies, /Needs linking/);
  });
});
