import * as assert from 'remix/assert';
import { describe, it } from 'remix/test';

import type { PlexEpisode, PlexItem, PlexRollup } from '../../models.ts';
import { router } from '../../router.ts';

const get = (path: string) => router.fetch(new URL(path, 'http://localhost'));
const post = (path: string) => router.fetch(new Request(new URL(path, 'http://localhost'), { method: 'POST' }));

const plexItem: PlexItem = {
  movieId: 'm1',
  plexItemId: '1234',
  onPlex: true,
  linkSource: 'auto',
  broken: false,
  library: 'Movies',
  resolution: '4k',
  hdr: true,
  videoCodec: 'hevc',
  bestAudioTrack: { codec: 'eac3', channels: 6, language: 'English' },
  subtitleLanguages: ['English', 'French'],
  watched: true,
  lastWatchedAt: '2025-02-10T20:00:00Z',
  viewCount: 3,
  dateAdded: '2024-12-01T00:00:00Z',
  deepLink: 'https://app.plex.tv/desktop/#!/server/abc/details?key=%2Flibrary%2Fmetadata%2F1234',
  durationSeconds: 6780,
  container: 'mkv',
  sizeBytes: 5_000_000_000,
  lastSyncedAt: new Date(Date.now() - 3 * 3600 * 1000).toISOString(),
};

interface StubOptions {
  /** What `/config/integrations` reports for Plex; omit for no Plex entry at all. */
  plex?: { enabled: boolean; entityTypes?: readonly string[] };
  /** The Plex data on the movie; omit for an unlinked movie. */
  item?: Partial<PlexItem>;
  /** The API's answer to the refresh POST. */
  refresh?: Response;
}

// Stubs the API and records every call as "METHOD /path".
function stubApi(t: { after: (fn: () => void) => void }, options: StubOptions = {}) {
  const original = globalThis.fetch;
  const calls: string[] = [];
  globalThis.fetch = (async (input: RequestInfo | URL, init?: RequestInit) => {
    const request = new Request(input, init);
    const url = new URL(request.url);
    calls.push(`${request.method} ${url.pathname}`);
    if (url.pathname.endsWith('/config/integrations')) {
      return Response.json(options.plex ? [{ key: 'plex', displayName: 'Plex', ...options.plex }] : []);
    }
    if (url.pathname.endsWith('/refresh')) return options.refresh ?? Response.json({});
    if (url.pathname.endsWith('/Movies/m1')) {
      return Response.json({
        id: 'm1',
        type: 'Movies',
        title: 'Heat',
        ...(options.item ? { plex: { ...plexItem, ...options.item } } : {}),
      });
    }
    return Response.json([]);
  }) as typeof fetch;
  t.after(() => {
    globalThis.fetch = original;
  });
  return calls;
}

const enabled = { enabled: true, entityTypes: ['movies'] };
const hasPlexUi = (html: string) => /data-plex-icon|data-testid="plex-card"/.test(html);

describe('plex gate on the movie detail page', () => {
  for (const [name, plex] of [
    ['no Plex integration is listed', undefined],
    ['Plex is disabled', { enabled: false, entityTypes: ['movies'] }],
    ['Plex is enabled without movies', { enabled: true, entityTypes: [] }],
    ['Plex is enabled for another type only', { enabled: true, entityTypes: ['music'] }],
  ] as const) {
    it(`should render nothing Plex-related when ${name}`, async (t) => {
      stubApi(t, { plex, item: {} });
      const html = await (await get('/movies/m1')).text();
      assert.equal(hasPlexUi(html), false);
      assert.doesNotMatch(html, /Open in Plex|Link to Plex|Refresh/);
    });
  }

  it('should render the icon and card when Plex is enabled for movies', async (t) => {
    stubApi(t, { plex: enabled, item: {} });
    const html = await (await get('/movies/m1')).text();
    assert.match(html, /data-plex-icon="onPlex"/);
    assert.match(html, /data-testid="plex-card"/);
  });

  it('should not show the Plex card or icon on other entity types', async (t) => {
    stubApi(t, { plex: enabled });
    const html = await (await get('/books/b1')).text();
    assert.equal(hasPlexUi(html), false);
  });
});

describe('plex icon on the movie detail title', () => {
  it('should show the normal icon with a tooltip and label when on Plex', async (t) => {
    stubApi(t, { plex: enabled, item: { watched: false } });
    const html = await (await get('/movies/m1')).text();
    assert.match(html, /role="img" aria-label="On Plex" title="On Plex"/);
    assert.match(html, /src="\/integrations\/plex-light\.svg"/);
    assert.doesNotMatch(html, /grayscale|data-plex-badge/);
  });

  it('should add a check badge when watched', async (t) => {
    stubApi(t, { plex: enabled, item: { watched: true } });
    const html = await (await get('/movies/m1')).text();
    assert.match(html, /aria-label="On Plex, watched"/);
    assert.match(html, /data-plex-badge="watched"/);
  });

  it('should mute the icon when off Plex', async (t) => {
    stubApi(t, { plex: enabled, item: { onPlex: false } });
    const html = await (await get('/movies/m1')).text();
    assert.match(html, /aria-label="No longer on Plex" title="No longer on Plex"/);
    assert.match(html, /opacity-40 grayscale/);
  });

  it('should add a warning dot when a manual link is broken', async (t) => {
    stubApi(t, { plex: enabled, item: { onPlex: false, broken: true, linkSource: 'manual' } });
    const html = await (await get('/movies/m1')).text();
    assert.match(html, /aria-label="Plex link is broken"/);
    assert.match(html, /data-plex-badge="warning"/);
  });

  it('should show no icon for an unlinked movie', async (t) => {
    stubApi(t, { plex: enabled });
    const html = await (await get('/movies/m1')).text();
    assert.doesNotMatch(html, /data-plex-icon/);
  });
});

describe('plex card', () => {
  const field = (html: string, name: string) =>
    html.match(new RegExp(`data-plex-field="${name}"[^>]*>([\\s\\S]*?)</div>`))?.[1]?.trim() ?? null;

  it('should show every stored fact, the sync age and the links for a movie on Plex', async (t) => {
    const calls = stubApi(t, { plex: enabled, item: {} });
    const html = await (await get('/movies/m1')).text();

    assert.equal(field(html, 'Resolution'), '4k HDR');
    assert.equal(field(html, 'Video codec'), 'HEVC');
    assert.equal(field(html, 'Audio'), '5.1 EAC3 English');
    assert.equal(field(html, 'Subtitles'), 'English, French');
    assert.equal(field(html, 'Watched'), 'Yes');
    assert.equal(field(html, 'Last watched'), 'Feb 10, 2025');
    assert.equal(field(html, 'Views'), '3');
    assert.equal(field(html, 'Date added'), 'Dec 1, 2024');
    assert.equal(field(html, 'Duration'), '1h 53m');
    assert.equal(field(html, 'Container'), 'mkv');
    assert.equal(field(html, 'Size'), '4.7 GB');
    assert.match(html, /Synced 3 hours ago/);
    assert.match(html, /<a href="https:\/\/app\.plex\.tv\/[^"]*"[^>]*>Open in Plex<\/a>/);
    assert.match(html, /<form method="post" action="\/movies\/m1\/plex\/refresh">/);
    // Loading the page reads the movie and the config only; it never refreshes or asks for file paths.
    assert.deepEqual(
      calls.filter((call) => /plex/i.test(call) && !call.includes('/config/')),
      []
    );
    assert.doesNotMatch(html, /filePath|Attribution/);
  });

  it('should show a dimmed card with a notice when off Plex', async (t) => {
    stubApi(t, { plex: enabled, item: { onPlex: false } });
    const html = await (await get('/movies/m1')).text();
    assert.match(html, /data-plex-state="offPlex"/);
    assert.match(html, /opacity-70/);
    assert.match(html, /No longer on Plex\. This is the last data MediaSet synced\./);
    assert.equal(field(html, 'Resolution'), '4k HDR');
  });

  it('should show a warning notice with Relink and Unlink when the link is broken', async (t) => {
    stubApi(t, { plex: enabled, item: { onPlex: false, broken: true, linkSource: 'manual' } });
    const html = await (await get('/movies/m1')).text();
    assert.match(html, /data-plex-state="broken"/);
    assert.match(html, /The Plex link for this movie is broken/);
    assert.match(html, />\s*Relink\s*</);
    assert.match(html, />\s*Unlink\s*</);
  });

  it('should show only a Link to Plex button for an unlinked movie', async (t) => {
    stubApi(t, { plex: enabled });
    const html = await (await get('/movies/m1')).text();
    assert.match(html, /data-plex-state="unlinked"/);
    assert.match(html, />\s*Link to Plex\s*</);
    assert.doesNotMatch(html, /Not on Plex|Refresh|Open in Plex/);
  });
});

describe('plex refresh', () => {
  it('should POST to the API and redirect back to the detail page', async (t) => {
    const calls = stubApi(t, { plex: enabled, item: {} });
    const response = await post('/movies/m1/plex/refresh');
    assert.equal(response.status, 303);
    assert.equal(response.headers.get('location'), '/movies/m1');
    assert.ok(calls.includes('POST /plex/movies/items/m1/refresh'));
  });

  it('should not refresh when Plex is off', async (t) => {
    const calls = stubApi(t, { plex: { enabled: false } });
    const response = await post('/movies/m1/plex/refresh');
    assert.equal(response.status, 404);
    assert.equal(
      calls.some((call) => call.startsWith('POST')),
      false
    );
  });

  const problem = (status: number, title: string) =>
    new Response(JSON.stringify({ title, status }), {
      status,
      headers: { 'Content-Type': 'application/problem+json' },
    });

  for (const [name, response, status, message] of [
    ['Plex is unreachable', problem(502, 'Plex request failed'), 502, /could not reach your Plex server/],
    ['a sync is running', problem(409, 'Plex sync already in progress'), 409, /sync is already in progress/],
    ['the item is missing on Plex', problem(404, 'Plex item not found'), 404, /Plex no longer has this item/],
    ['the movie is not linked', problem(400, 'Not linked to Plex'), 400, /not linked to a Plex item/],
    ['the API fails otherwise', problem(500, 'Boom'), 500, /Something went wrong while refreshing/],
  ] as const) {
    it(`should render the error page when ${name}`, async (t) => {
      stubApi(t, { plex: enabled, item: {}, refresh: response });
      const result = await post('/movies/m1/plex/refresh');
      const html = await result.text();
      assert.equal(result.status, status);
      assert.match(html, message);
      assert.match(html, /href="\/movies\/m1"/);
    });
  }
});

describe('plex card for a TV entry', () => {
  const field = (html: string, name: string) =>
    html.match(new RegExp(`data-plex-field="${name}"[^>]*>([\\s\\S]*?)</div>`))?.[1]?.trim() ?? null;

  const rollup = (overrides: Partial<PlexRollup> = {}): PlexRollup => ({
    episodesPresent: 14,
    episodesExpected: 16,
    watchedCount: 12,
    watched: false,
    lastWatchedAt: '2025-03-01T00:00:00Z',
    resolution: '1080p',
    resolutionMixed: false,
    hdr: false,
    audioLanguages: ['English'],
    audioLanguagesDiffer: false,
    subtitleLanguages: ['English', 'French'],
    subtitleLanguagesDiffer: false,
    ...overrides,
  });

  const episode = (season: number, number: number, overrides: Partial<PlexEpisode> = {}): PlexEpisode => ({
    seasonNumber: season,
    episodeNumber: number,
    title: `Episode ${season}.${number}`,
    plexEpisodeId: `e${season}${number}`,
    watched: false,
    resolution: '1080p',
    hdr: false,
    sizeBytes: 1024 * 1024 * 1024,
    onPlex: true,
    ...overrides,
  });

  const tv: Partial<PlexItem> = {
    scope: { kind: 'range', from: 1, to: 2 },
    rollup: rollup(),
    seasons: [
      {
        index: 1,
        plexSeasonId: 's1',
        episodeCount: 8,
        rollup: rollup({ episodesPresent: 8, episodesExpected: 8, watchedCount: 8 }),
      },
      {
        index: 2,
        plexSeasonId: 's2',
        episodeCount: 8,
        rollup: rollup({
          episodesPresent: 6,
          episodesExpected: 8,
          watchedCount: 4,
          resolution: '1080p',
          resolutionMixed: true,
          audioLanguages: ['English', 'German'],
          audioLanguagesDiffer: true,
        }),
      },
    ],
    episodes: [
      episode(1, 1, { watched: true }),
      episode(2, 1, { resolution: '720p', sizeBytes: 500 * 1024 * 1024 }),
      episode(2, 2, { onPlex: false, watched: true }),
    ],
  };

  it('should show progress, scope and rollup facts in the details, not beside the title', async (t) => {
    stubApi(t, { plex: enabled, item: tv });
    const html = await (await get('/movies/m1')).text();
    assert.equal(field(html, 'Episodes'), '14/16 episodes | 12 watched');
    assert.equal(field(html, 'Scope'), 'Seasons 1-2');
    assert.equal(field(html, 'Resolution'), '1080p');
    assert.equal(field(html, 'Audio'), 'English');
    assert.equal(field(html, 'Subtitles'), 'English, French');
    assert.doesNotMatch(html, /Video codec|data-plex-field="Size"/);
    // The title icon never carries the episode line.
    assert.doesNotMatch(html.match(/<h1[\s\S]*?<\/h1>/)?.[0] ?? '', /episodes/);
  });

  it('should describe a single season and the complete series', async (t) => {
    stubApi(t, { plex: enabled, item: { ...tv, scope: { kind: 'season', from: 2, to: 2 } } });
    assert.equal(field(await (await get('/movies/m1')).text(), 'Scope'), 'Season 2');
    stubApi(t, { plex: enabled, item: { ...tv, scope: { kind: 'all' } } });
    assert.equal(field(await (await get('/movies/m1')).text(), 'Scope'), 'Complete series');
  });

  it('should list each season in a native details element with its own rollup', async (t) => {
    stubApi(t, { plex: enabled, item: tv });
    const html = await (await get('/movies/m1')).text();
    assert.equal((html.match(/<details[^>]*data-plex-season/g) ?? []).length, 2);
    const summary = (n: number) =>
      html.match(new RegExp(`data-plex-season="${n}"[^>]*>\\s*<summary[^>]*>([\\s\\S]*?)</summary>`))?.[1] ?? '';
    assert.match(summary(1), /Season 1.*8\/8 episodes \| 8 watched/);
    assert.match(summary(2), /Season 2.*6\/8 episodes \| 4 watched/);
    assert.doesNotMatch(html, /<details[^>]* open/);
  });

  it('should show a mixed-quality season and the differs note', async (t) => {
    stubApi(t, { plex: enabled, item: tv });
    const html = await (await get('/movies/m1')).text();
    assert.match(html, /Mixed \| Audio: English, German \(differs per episode\) \| Subtitles: English, French/);
  });

  it('should show Mixed and HDR in the entry rollup', async (t) => {
    stubApi(t, { plex: enabled, item: { ...tv, rollup: rollup({ resolutionMixed: true, hdr: true }) } });
    assert.equal(field(await (await get('/movies/m1')).text(), 'Resolution'), 'Mixed HDR');
  });

  it('should list episodes with title, watched tick, resolution and size', async (t) => {
    stubApi(t, { plex: enabled, item: tv });
    const html = await (await get('/movies/m1')).text();
    const first = html.match(/data-plex-episode="S01E01"[\s\S]*?<\/li>/)?.[0] ?? '';
    assert.match(first, /Episode 1\.1/);
    assert.match(first, /aria-label="Watched"/);
    assert.match(first, /1080p/);
    assert.match(first, /1\.0 GB/);
    const second = html.match(/data-plex-episode="S02E01"[\s\S]*?<\/li>/)?.[0] ?? '';
    assert.doesNotMatch(second, /aria-label="Watched"/);
    assert.match(second, /720p/);
    assert.match(second, /500 MB/);
  });

  it('should mark an episode that is no longer on Plex as missing', async (t) => {
    stubApi(t, { plex: enabled, item: tv });
    const html = await (await get('/movies/m1')).text();
    const missing = html.match(/data-plex-episode="S02E02"[\s\S]*?<\/li>/)?.[0] ?? '';
    assert.match(missing, /data-plex-episode-state="missing"/);
    assert.match(missing, /Missing from Plex/);
    assert.equal((html.match(/Missing from Plex/g) ?? []).length, 1);
  });

  it('should show episodes of a season missing from the seasons list under Other episodes', async (t) => {
    stubApi(t, { plex: enabled, item: { ...tv, episodes: [...(tv.episodes ?? []), episode(5, 1)] } });
    const html = await (await get('/movies/m1')).text();
    const other = html.match(/data-plex-season="other"[\s\S]*?<\/details>/)?.[0] ?? '';
    assert.match(other, /Other episodes/);
    assert.match(other, /data-plex-episode="S05E01"/);
    assert.doesNotMatch(other, /S01E01|S02E01/);
  });

  it('should not render season lists for a movie', async (t) => {
    stubApi(t, { plex: enabled, item: {} });
    const html = await (await get('/movies/m1')).text();
    assert.doesNotMatch(html, /data-plex-season|plex-seasons/);
  });
});
