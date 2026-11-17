import * as assert from 'remix/assert';
import { describe, it } from 'remix/test';

import type { PlexMusicItem, PlexMusicSummary } from '../../models.ts';
import { router } from '../../router.ts';

const get = (path: string) => router.fetch(new URL(path, 'http://localhost'));

const enabled = { enabled: true, entityTypes: ['music'] };

const summary = (overrides: Partial<PlexMusicSummary>): PlexMusicSummary => ({
  onPlex: true,
  broken: false,
  playCount: 12,
  lastPlayedAt: '2025-03-01T00:00:00Z',
  tracksPresent: 10,
  tracksExpected: 10,
  audioCodec: 'flac',
  bitrate: 900,
  ...overrides,
});

const albums = [
  { id: 'a1', type: 'Musics', title: 'Played', artist: 'A', plex: summary({}) },
  { id: 'a2', type: 'Musics', title: 'Never', artist: 'B', plex: summary({ playCount: 0, lastPlayedAt: null }) },
  { id: 'a3', type: 'Musics', title: 'Partial', artist: 'C', plex: summary({ tracksPresent: 7 }) },
  { id: 'a4', type: 'Musics', title: 'Gone', artist: 'D', plex: summary({ onPlex: false }) },
  { id: 'a5', type: 'Musics', title: 'Unlinked', artist: 'E' },
];

const plexItem: PlexMusicItem = {
  ...summary({}),
  musicId: 'a1',
  plexItemId: '7001',
  linkSource: 'auto',
  library: 'Music',
  sizeBytes: 350_000_000,
  dateAdded: '2024-12-01T00:00:00Z',
  deepLink: 'https://app.plex.tv/desktop/#!/server/abc/details?key=%2Flibrary%2Fmetadata%2F7001',
  lastSyncedAt: new Date(Date.now() - 3 * 3600 * 1000).toISOString(),
};

interface StubOptions {
  plex?: { enabled: boolean; entityTypes?: readonly string[] };
  items?: unknown[];
  item?: Partial<PlexMusicItem> | null;
}

// Stubs the API; `plex` is what /config/integrations reports. Records every non-config call URL.
function stubApi(t: { after: (fn: () => void) => void }, options: StubOptions = {}) {
  const original = globalThis.fetch;
  const calls: URL[] = [];
  globalThis.fetch = (async (input: RequestInfo | URL) => {
    const url = new URL(input instanceof Request ? input.url : String(input));
    if (url.pathname === '/config/integrations') {
      return Response.json(options.plex ? [{ key: 'plex', displayName: 'Plex', ...options.plex }] : []);
    }
    calls.push(url);
    if (url.pathname.endsWith('/Musics/a1')) {
      return Response.json({
        id: 'a1',
        type: 'Musics',
        title: 'Abbey Road',
        artist: 'The Beatles',
        ...(options.item === null ? {} : { plex: { ...plexItem, ...options.item } }),
      });
    }
    const items = options.items ?? albums;
    return Response.json({ items, totalCount: items.length, page: 1, pageSize: 75, totalPages: 1 });
  }) as typeof fetch;
  t.after(() => {
    globalThis.fetch = original;
  });
  return calls;
}

const iconCount = (html: string) => (html.match(/data-plex-icon=/g) ?? []).length;

describe('music list with Plex', () => {
  it('should show a Plex icon on each linked album, mobile and table', async (t) => {
    stubApi(t, { plex: enabled });
    const html = await (await get('/musics')).text();

    // Four linked albums, once in the mobile list and once in the table; the unlinked one has none
    assert.equal(iconCount(html), 8);
    assert.match(html, /data-plex-icon="onPlex"/);
    assert.match(html, /data-plex-icon="offPlex"/);
    assert.match(html, /<th[^>]*>On Plex<\/th>/);
  });

  it('should say never played and mark a partial album in the table', async (t) => {
    stubApi(t, { plex: enabled });
    const html = await (await get('/musics')).text();
    const table = html.match(/<table[\s\S]*<\/table>/)?.[0] ?? '';

    assert.equal((table.match(/Never played/g) ?? []).length, 1);
    assert.match(table, /data-plex-partial[^>]*>\s*Partial 7\/10/);
    assert.match(html, /aria-label="On Plex, never played"/);
    assert.match(html, /aria-label="On Plex, 12 plays, partial, 7 of 10 tracks"/);
    assert.match(html, /aria-label="On Plex, 12 plays"/);
  });

  it('should show no notes for a complete, played album', async (t) => {
    stubApi(t, { plex: enabled, items: [albums[0]] });
    const html = await (await get('/musics')).text();
    assert.doesNotMatch(html, /plex-music-notes/);
  });

  it('should render no icon for an unlinked album', async (t) => {
    stubApi(t, { plex: enabled, items: [albums[4]] });
    const html = await (await get('/musics')).text();
    assert.equal(iconCount(html), 0);
    assert.match(html, /Unlinked/);
  });

  it('should render a filter with the on Plex, not on Plex and broken link choices', async (t) => {
    stubApi(t, { plex: enabled });
    const html = await (await get('/musics')).text();

    assert.match(html, /<select[^>]*name="plex"/);
    for (const label of ['All albums', 'On Plex', 'Not on Plex', 'Broken link']) {
      assert.match(html, new RegExp(`>${label}<`));
    }
    assert.doesNotMatch(html, />Unwatched on Plex</);
  });

  it('should pass the plex filter to the paged search and select it', async (t) => {
    const calls = stubApi(t, { plex: enabled });
    const html = await (await get('/musics?plex=notOnPlex')).text();

    assert.equal(calls[0].pathname, '/Musics/pagedsearch');
    assert.equal(calls[0].searchParams.get('plex'), 'notOnPlex');
    assert.match(html, /<option value="notOnPlex" selected/);
  });

  it('should pass the broken filter to the paged search', async (t) => {
    const calls = stubApi(t, { plex: enabled });
    await get('/musics?plex=broken');
    assert.equal(calls[0].searchParams.get('plex'), 'broken');
  });

  it('should drop a filter albums do not have', async (t) => {
    const calls = stubApi(t, { plex: enabled });
    await get('/musics?plex=unwatched');
    assert.equal(calls[0].searchParams.has('plex'), false);
  });

  it('should offer no filter, icons or column when Plex is off', async (t) => {
    const calls = stubApi(t, { plex: { enabled: false, entityTypes: ['music'] } });
    const html = await (await get('/musics?plex=onPlex')).text();

    assert.equal(iconCount(html), 0);
    assert.doesNotMatch(html, /name="plex"/);
    assert.doesNotMatch(html, /On Plex/);
    assert.equal(calls[0].searchParams.has('plex'), false);
  });

  it('should offer no Plex UI when Plex is enabled for movies only', async (t) => {
    const calls = stubApi(t, { plex: { enabled: true, entityTypes: ['movies'] } });
    const html = await (await get('/musics?plex=onPlex')).text();

    assert.equal(iconCount(html), 0);
    assert.doesNotMatch(html, /name="plex"/);
    assert.equal(calls[0].searchParams.has('plex'), false);
  });

  it('should render nothing for Plex when the integration is absent', async (t) => {
    stubApi(t, {});
    const html = await (await get('/musics')).text();
    assert.equal(iconCount(html), 0);
    assert.doesNotMatch(html, /name="plex"/);
  });

  it('should leave the movie list without Plex when only music is enabled', async (t) => {
    stubApi(t, { plex: enabled, items: [{ id: 'm1', type: 'Movies', title: 'Alien', plex: { onPlex: true } }] });
    const html = await (await get('/movies')).text();
    assert.equal(iconCount(html), 0);
    assert.doesNotMatch(html, /name="plex"/);
  });

  it('should say no albums match when the filter finds none', async (t) => {
    stubApi(t, { plex: enabled, items: [] });
    const html = await (await get('/musics?plex=onPlex')).text();
    assert.match(html, /No Musics match\s*this Plex filter\./);
  });
});

describe('music detail with Plex', () => {
  it('should show the icon and a card with plays, last played, tracks, quality, size and a link', async (t) => {
    stubApi(t, { plex: enabled });
    const html = await (await get('/musics/a1')).text();

    assert.match(html, /data-plex-icon="onPlex"/);
    assert.match(html, /data-testid="plex-card"/);
    assert.match(html, /data-plex-field="Plays"[^>]*>12 plays</);
    assert.match(html, /data-plex-field="Last played"[^>]*>Mar 1, 2025</);
    assert.match(html, /data-plex-field="Tracks"[^>]*>10 of 10 tracks</);
    assert.match(html, /data-plex-field="Quality"[^>]*>FLAC, 900 kbps</);
    assert.match(html, /data-plex-field="Size"[^>]*>334 MB</);
    assert.match(html, /data-plex-field="Date added"[^>]*>Dec 1, 2024</);
    assert.match(html, /href="https:\/\/app\.plex\.tv\/desktop[^"]*"[^>]*>\s*Open in Plex/);
    assert.match(html, /Synced 3 hours ago/);
  });

  it('should say never played and omit the last played date', async (t) => {
    stubApi(t, { plex: enabled, item: { playCount: 0, lastPlayedAt: null } });
    const html = await (await get('/musics/a1')).text();

    assert.match(html, /data-plex-field="Plays"[^>]*>Never played</);
    assert.doesNotMatch(html, /Last played/);
  });

  it('should mark a partial album with its tracks', async (t) => {
    stubApi(t, { plex: enabled, item: { tracksPresent: 7, tracksExpected: 10 } });
    const html = await (await get('/musics/a1')).text();

    assert.match(html, /data-plex-field="Tracks"[^>]*>7 of 10 tracks \(partial\)</);
  });

  it('should show only the Plex track count when MediaSet expects none', async (t) => {
    stubApi(t, { plex: enabled, item: { tracksPresent: 7, tracksExpected: null } });
    const html = await (await get('/musics/a1')).text();

    assert.match(html, /data-plex-field="Tracks"[^>]*>7 tracks</);
    assert.doesNotMatch(html, /partial/);
  });

  it('should warn on a stale album and dim the card', async (t) => {
    stubApi(t, { plex: enabled, item: { onPlex: false } });
    const html = await (await get('/musics/a1')).text();

    assert.match(html, /data-plex-state="offPlex"/);
    assert.match(html, /No longer on Plex/);
  });

  it('should only link web addresses to Plex', async (t) => {
    stubApi(t, { plex: enabled, item: { deepLink: 'javascript:alert(1)' } });
    const html = await (await get('/musics/a1')).text();
    assert.doesNotMatch(html, /Open in Plex/);
  });

  it('should render only a Link to Plex button and no icon for an unlinked album', async (t) => {
    stubApi(t, { plex: enabled, item: null });
    const html = await (await get('/musics/a1')).text();
    assert.doesNotMatch(html, /data-plex-icon|Open in Plex/);
    assert.match(html, /data-plex-state="unlinked"/);
    assert.match(
      html,
      /<form method="post" action="\/musics\/a1\/plex\/link"><input type="hidden" name="intent" value="auto"/
    );
  });

  it('should offer Relink and Unlink but no Refresh on a linked album', async (t) => {
    stubApi(t, { plex: enabled });
    const html = await (await get('/musics/a1')).text();
    assert.match(html, /<form method="get" action="\/musics\/a1\/plex\/link">/);
    assert.match(html, /<form method="get" action="\/musics\/a1\/plex\/unlink">/);
    assert.match(html, /The album is kept/);
    assert.doesNotMatch(html, /plex\/refresh|>\s*Refresh\s*</);
  });

  for (const [name, plex] of [
    ['no Plex integration is listed', undefined],
    ['Plex is disabled', { enabled: false, entityTypes: ['music'] }],
    ['Plex is enabled for movies only', { enabled: true, entityTypes: ['movies'] }],
  ] as const) {
    it(`should render nothing Plex-related when ${name}`, async (t) => {
      stubApi(t, { plex });
      const html = await (await get('/musics/a1')).text();
      assert.doesNotMatch(html, /data-plex-icon|data-testid="plex-card"|Open in Plex/);
    });
  }
});
