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

function stubApi(
  t: { after: (fn: () => void) => void },
  stats: unknown,
  integrations: unknown = enabled,
  plexStatistics: unknown = null,
  statisticsRequests?: string[]
) {
  const original = globalThis.fetch;
  globalThis.fetch = (async (input: RequestInfo | URL) => {
    const url = new URL(input instanceof Request ? input.url : String(input));
    if (url.pathname === '/stats') return Response.json(stats);
    if (url.pathname === '/config/integrations') return Response.json(integrations);
    if (url.pathname === '/plex/statistics') {
      statisticsRequests?.push(url.search);
      return Response.json(plexStatistics, { status: plexStatistics ? 200 : 500 });
    }
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

const musicStats = { ...baseStats, musicStats: { total: 5, totalTracks: 50 } };
const musicEnabled = [{ key: 'plex', displayName: 'Plex', enabled: true, entityTypes: ['music'] }];
const bothEnabled = [{ key: 'plex', displayName: 'Plex', enabled: true, entityTypes: ['movies', 'music'] }];
const plexMusic = {
  music: {
    coverage: { total: 5, onPlex: 3, notOnPlex: 2, broken: 1 },
    albums: 3,
    totalPlays: 42,
    neverPlayed: 1,
  },
};

const musicsPanel = (html: string) =>
  html.match(/<div id="dashboard-panel-musics"[\s\S]*?(?=<div id="dashboard-panel-|$)/)?.[0] ?? '';

describe('dashboard Plex music section', () => {
  it('should request only the music section of the Plex statistics', async (t) => {
    const requests: string[] = [];
    stubApi(t, musicStats, bothEnabled, plexMusic, requests);

    await get('/');

    assert.deepEqual(requests, ['?sections=music']);
  });

  it('should show the Plex stat cards and a link to the not-on-Plex list on the Music tab', async (t) => {
    stubApi(t, musicStats, musicEnabled, plexMusic);

    const music = musicsPanel(await (await get('/')).text());

    assert.match(music, /<h3[^>]*>Plex<\/h3>/);
    for (const [title, value] of [
      ['On Plex', '3'],
      ['Not on Plex', '2'],
      ['Total plays', '42'],
      ['Never played', '1'],
    ]) {
      assert.match(music, new RegExp(`${title}</p><p class="[^"]*">${value}</p>`));
    }
    assert.match(music, /<a[^>]*href="\/musics\?plex=notOnPlex"/);
  });

  it('should show the movie and music Plex sections together when both are enabled', async (t) => {
    stubApi(t, { ...musicStats, plexStats }, bothEnabled, plexMusic);

    const html = await (await get('/')).text();

    assert.match(moviesPanel(html), /data-plex-needs-linking/);
    assert.match(musicsPanel(html), /dashboard-plex-music-heading/);
  });

  it('should leave the movie panel without a Plex section when only music is enabled', async (t) => {
    stubApi(t, { ...musicStats, plexStats }, musicEnabled, plexMusic);

    const html = await (await get('/')).text();

    assert.doesNotMatch(moviesPanel(html), /Plex/);
    assert.match(musicsPanel(html), /dashboard-plex-music-heading/);
  });

  it('should render no music Plex section when Plex is enabled for movies only', async (t) => {
    stubApi(t, { ...musicStats, plexStats }, enabled, plexMusic);

    assert.doesNotMatch(musicsPanel(await (await get('/')).text()), /dashboard-plex-music-heading/);
  });

  it('should render no music Plex section when Plex is not enabled', async (t) => {
    stubApi(t, musicStats, [], plexMusic);

    assert.doesNotMatch(musicsPanel(await (await get('/')).text()), /dashboard-plex-music-heading/);
  });

  it('should still render the dashboard without the music section when the Plex statistics fail', async (t) => {
    stubApi(t, musicStats, musicEnabled, null);

    const response = await get('/');

    assert.equal(response.status, 200);
    assert.doesNotMatch(musicsPanel(await response.text()), /dashboard-plex-music-heading/);
  });
});
