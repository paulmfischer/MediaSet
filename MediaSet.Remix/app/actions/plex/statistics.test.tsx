import * as assert from 'remix/assert';
import { describe, it } from 'remix/test';

import type { PlexKindStatistics, PlexStatistics } from '../../models.ts';
import { router } from '../../router.ts';

const get = (path: string) => router.fetch(new URL(path, 'http://localhost'));

const enabled = [{ key: 'plex', displayName: 'Plex', enabled: true, entityTypes: ['movies'] }];

const kind = (overrides: Partial<PlexKindStatistics> = {}): PlexKindStatistics => ({
  kind: 'movies',
  coverage: { total: 10, onPlex: 6, notOnPlex: 4, broken: 1 },
  units: 6,
  resolution: [
    { name: '4k', count: 2 },
    { name: '1080', count: 3 },
    { name: 'unknown', count: 1 },
  ],
  videoCodec: [
    { name: 'hevc', count: 4 },
    { name: 'h264', count: 2 },
  ],
  hdr: [
    { name: 'HDR', count: 2 },
    { name: 'SDR', count: 4 },
  ],
  subtitles: [
    { name: 'With subtitles', count: 5 },
    { name: 'Without subtitles', count: 1 },
  ],
  watched: [
    { name: 'Watched', count: 3 },
    { name: 'Not watched', count: 3 },
  ],
  audioCodec: [{ name: 'eac3', count: 6 }],
  audioChannels: [
    { name: '5.1', count: 4 },
    { name: 'Stereo', count: 2 },
  ],
  storage: { totalBytes: 12_000_000_000, averageBytes: 2_000_000_000, unitsWithSize: 6 },
  runtime: { totalSeconds: 36_000, averageSeconds: 6_000, unitsWithRuntime: 6 },
  mostWatched: [{ id: 'm1', title: 'Heat', viewCount: 7 }],
  completeness: null,
  ...overrides,
});

const tvKind = (overrides: Partial<PlexKindStatistics> = {}): PlexKindStatistics =>
  kind({
    kind: 'tv',
    coverage: { total: 2, onPlex: 2, notOnPlex: 0, broken: 0 },
    units: 8,
    mostWatched: [],
    completeness: { episodesPresent: 8, episodesExpected: 10, entriesComplete: 1, entriesIncomplete: 1 },
    ...overrides,
  });

const statistics = (): PlexStatistics => ({ movies: kind(), tv: tvKind() });

interface StubOptions {
  plex?: unknown;
  statistics?: Response;
}

function stubApi(t: { after: (fn: () => void) => void }, options: StubOptions = {}) {
  const original = globalThis.fetch;
  const calls: string[] = [];
  globalThis.fetch = (async (input: RequestInfo | URL, init?: RequestInit) => {
    const url = new URL(new Request(input, init).url);
    calls.push(url.pathname);
    if (url.pathname.endsWith('/config/integrations')) return Response.json(options.plex ?? enabled);
    if (url.pathname === '/plex/statistics') return (options.statistics ?? Response.json(statistics())).clone();
    if (url.pathname === '/plex/status') {
      return Response.json([
        { entityType: 'movies', enabled: true, lastRun: null, nextRun: null, schedulePolicy: 'queue' },
      ]);
    }
    return Response.json([]);
  }) as typeof fetch;
  t.after(() => {
    globalThis.fetch = original;
  });
  return calls;
}

describe('plex statistics section', () => {
  it('should show the statistics before the sync section', async (t) => {
    stubApi(t);

    const html = await (await get('/plex')).text();

    const stats = html.indexOf('data-testid="plex-statistics"');
    assert.ok(stats > -1, 'statistics are rendered');
    assert.ok(stats < html.indexOf('data-testid="plex-entity-status"'), 'statistics come first');
  });

  it('should show a section per entity with its coverage and charts', async (t) => {
    stubApi(t);

    const html = await (await get('/plex')).text();

    assert.match(html, /data-testid="plex-stats-kind" data-kind="movies"/);
    assert.match(html, /data-testid="plex-stats-kind" data-kind="tv"/);
    assert.match(html, /Movies in MediaSet<\/p><p class="[^"]*">10</);
    assert.match(html, /Broken links<\/p><p class="[^"]*">1</);
    for (const chart of ['Resolution', 'Video codec', 'HDR vs SDR', 'Subtitles', 'Watched']) {
      assert.match(html, new RegExp(`aria-label="${chart} pie chart"`));
    }
    assert.match(html, /Audio codec/);
    assert.match(html, /Audio channel layout/);
    assert.match(html, /Total size \(6 movies\)<\/p><p class="[^"]*">12\.0 GB</);
    assert.match(html, /Average runtime<\/p><p class="[^"]*">1h 40m</);
  });

  it('should show most watched and TV completeness', async (t) => {
    stubApi(t);

    const html = await (await get('/plex')).text();

    assert.match(html, /Most watched/);
    assert.match(html, />Heat</);
    assert.match(html, /8 of 10 episodes/);
    assert.match(html, /1 complete, 1 missing episodes/);
  });

  it('should say there is nothing to break down for a section with no on-Plex items', async (t) => {
    stubApi(t, {
      statistics: Response.json({
        movies: kind(),
        tv: tvKind({ units: 0, coverage: { total: 3, onPlex: 0, notOnPlex: 3, broken: 0 }, completeness: null }),
      }),
    });

    const html = await (await get('/plex')).text();

    assert.match(html, /No episodes on Plex yet/);
  });

  it('should still render the sync section when the statistics cannot be read', async (t) => {
    stubApi(t, { statistics: new Response('boom', { status: 500 }) });

    const response = await get('/plex');
    const html = await response.text();

    assert.equal(response.status, 200);
    assert.doesNotMatch(html, /data-testid="plex-statistics"/);
    assert.match(html, /data-testid="plex-entity-status"/);
  });

  it('should not request the statistics when Plex is not enabled for movies', async (t) => {
    const calls = stubApi(t, { plex: [] });

    const response = await get('/plex');

    assert.equal(response.status, 404);
    assert.equal(calls.includes('/plex/statistics'), false);
  });
});
