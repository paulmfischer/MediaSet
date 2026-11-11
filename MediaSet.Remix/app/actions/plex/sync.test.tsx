import * as assert from 'remix/assert';
import { describe, it } from 'remix/test';

import type { PlexEntityStatus, PlexSyncRun } from '../../models.ts';
import { router } from '../../router.ts';

const get = (path: string) => router.fetch(new URL(path, 'http://localhost'));
const post = (path: string, fields: Record<string, string>) =>
  router.fetch(new Request(new URL(path, 'http://localhost'), { method: 'POST', body: new URLSearchParams(fields) }));

const run = (overrides: Partial<PlexSyncRun> = {}): PlexSyncRun => ({
  id: 'r1',
  entityType: 'movies',
  trigger: 'manual',
  state: 'succeeded',
  startedAt: '2026-03-01T03:00:00Z',
  endedAt: '2026-03-01T03:04:00Z',
  itemsScanned: 120,
  matchedCount: 100,
  unmatchedCount: 20,
  erroredCount: 0,
  externalIdsBackfilled: 4,
  backfillSkipped: 1,
  ...overrides,
});

const status = (overrides: Partial<PlexEntityStatus> = {}): PlexEntityStatus => ({
  entityType: 'movies',
  enabled: true,
  lastRun: run(),
  nextRun: '2026-03-08T03:00:00Z',
  schedulePolicy: 'queue',
  ...overrides,
});

interface StubOptions {
  plex?: { enabled: boolean; entityTypes?: readonly string[] };
  /** What `/plex/status` answers, one entry per call (the last one repeats). */
  statuses?: PlexEntityStatus[][];
  statusFailure?: Response;
  /** The answer to POST /plex/movies/sync. */
  sync?: Response;
}

const enabled = { enabled: true, entityTypes: ['movies'] };

// Stubs the API and records every call as "METHOD /path".
function stubApi(t: { after: (fn: () => void) => void }, options: StubOptions = {}) {
  const original = globalThis.fetch;
  const calls: string[] = [];
  let statusCalls = 0;
  globalThis.fetch = (async (input: RequestInfo | URL, init?: RequestInit) => {
    const request = new Request(input, init);
    const url = new URL(request.url);
    calls.push(`${request.method} ${url.pathname}`);
    if (url.pathname.endsWith('/config/integrations')) {
      return Response.json(options.plex ? [{ key: 'plex', displayName: 'Plex', ...options.plex }] : []);
    }
    if (url.pathname === '/plex/status') {
      if (options.statusFailure) return options.statusFailure.clone();
      const list = options.statuses ?? [[status()]];
      return Response.json(list[Math.min(statusCalls++, list.length - 1)]);
    }
    if (url.pathname === '/plex/movies/sync')
      return options.sync ?? Response.json(run({ state: 'running' }), { status: 202 });
    return Response.json([]);
  }) as typeof fetch;
  t.after(() => {
    globalThis.fetch = original;
  });
  return calls;
}

describe('gear menu Plex link', () => {
  for (const [name, plex] of [
    ['no Plex integration is listed', undefined],
    ['Plex is disabled', { enabled: false, entityTypes: ['movies'] }],
    ['Plex is enabled without movies', { enabled: true, entityTypes: ['music'] }],
  ] as const) {
    it(`should not show the link or serve /plex when ${name}`, async (t) => {
      const calls = stubApi(t, { plex });
      const home = await (await get('/books/b1')).text();
      assert.doesNotMatch(home, /"href":"\/plex"/);

      const page = await get('/plex');
      assert.equal(page.status, 404);
      assert.equal((await get('/plex/status')).status, 404);
      assert.equal((await post('/plex', { entityType: 'movies' })).status, 404);
      assert.equal(
        calls.some((call) => call.startsWith('POST') || call === 'GET /plex/status'),
        false
      );
    });
  }

  it('should show the link on every page when Plex is enabled for movies', async (t) => {
    stubApi(t, { plex: enabled });
    for (const path of ['/books/b1', '/plex']) {
      const html = await (await get(path)).text();
      assert.match(html, /"href":"\/plex"[^]*?"Plex"/, path);
    }
  });
});

describe('plex sync page', () => {
  it('should show the last run state, time and counts and the next run for an idle type', async (t) => {
    stubApi(t, { plex: enabled });
    const response = await get('/plex');
    const html = await response.text();

    assert.equal(response.status, 200);
    assert.match(html, /<title>Plex sync \| MediaSet<\/title>/);
    assert.match(html, /data-entity-type="movies"/);
    assert.match(html, /data-testid="plex-run-state"[^>]*>Succeeded/);
    assert.match(html, /<time datetime="2026-03-01T03:04:00Z"/);
    assert.match(html, /<time datetime="2026-03-08T03:00:00Z"/);
    for (const [name, value] of [
      ['Items scanned', 120],
      ['Matched', 100],
      ['Unmatched', 20],
      ['IDs backfilled', 4],
      ['Backfill skipped', 1],
    ] as const) {
      assert.match(html, new RegExp(`<dt>${name}</dt><dd>${value}</dd>`));
    }
    assert.match(html, /<form method="post" action="\/plex"/);
    assert.match(html, /<button type="submit" data-testid="plex-sync-now">/);
    assert.doesNotMatch(html, /Running since|data-testid="plex-sync-poller"/);
  });

  it('should say so when no sync has run and nothing is scheduled', async (t) => {
    stubApi(t, { plex: enabled, statuses: [[status({ lastRun: null, nextRun: null })]] });
    const html = await (await get('/plex')).text();
    assert.match(html, /No sync has run yet\./);
    assert.match(html, /Not scheduled/);
  });

  it('should list only the enabled entity types', async (t) => {
    stubApi(t, { plex: enabled, statuses: [[status(), status({ entityType: 'music', enabled: false })]] });
    const html = await (await get('/plex')).text();
    assert.match(html, /data-entity-type="movies"/);
    assert.doesNotMatch(html, /data-entity-type="music"/);
  });

  it('should disable Sync now and show Running since, with the poller, while a run is active', async (t) => {
    stubApi(t, {
      plex: enabled,
      statuses: [[status({ lastRun: run({ state: 'running', startedAt: '2026-03-02T10:00:00Z', endedAt: null }) })]],
    });
    const html = await (await get('/plex')).text();
    assert.match(
      html,
      /<button type="button" disabled aria-disabled="true" aria-describedby="plex-movies-running" data-testid="plex-sync-now">/
    );
    assert.match(html, /Running since <!-- rmx:h:[a-z0-9]+ --><time datetime="2026-03-02T10:00:00Z"/);
    assert.match(html, /data-testid="plex-sync-poller"/);
    assert.doesNotMatch(html, /<form method="post" action="\/plex"/);
  });

  it('should show a queued run as queued', async (t) => {
    stubApi(t, { plex: enabled, statuses: [[status({ lastRun: run({ state: 'queued', endedAt: null }) })]] });
    const html = await (await get('/plex')).text();
    assert.match(html, /Queued since/);
    assert.match(html, /disabled/);
  });

  it('should show the failure message of a failed last run and allow a new sync', async (t) => {
    stubApi(t, {
      plex: enabled,
      statuses: [[status({ lastRun: run({ state: 'failed', message: 'Plex was unreachable', matchedCount: null }) })]],
    });
    const html = await (await get('/plex')).text();
    assert.match(html, /data-testid="plex-run-state"[^>]*>Failed/);
    assert.match(html, /Plex was unreachable/);
    assert.match(html, /<button type="submit" data-testid="plex-sync-now">/);
    assert.doesNotMatch(html, /<dt>Matched<\/dt>/);
  });

  it('should render the error page when the status cannot be read', async (t) => {
    stubApi(t, { plex: enabled, statusFailure: new Response('boom', { status: 502 }) });
    const response = await get('/plex');
    assert.equal(response.status, 502);
    assert.match(await response.text(), /reach Plex/);
  });
});

describe('plex sync now', () => {
  it('should start the sync and redirect back to the page', async (t) => {
    const calls = stubApi(t, { plex: enabled });
    const response = await post('/plex', { entityType: 'movies' });
    assert.equal(response.status, 303);
    assert.equal(response.headers.get('Location'), '/plex');
    assert.equal(calls.filter((call) => call.endsWith('/config/integrations')).length, 1);
    assert.ok(calls.includes('POST /plex/movies/sync'));
  });

  it('should redirect to the page with a notice, not an error, on a conflict, and show the current run there', async (t) => {
    const active = run({ state: 'running', startedAt: '2026-03-02T10:00:00Z', endedAt: null });
    const calls = stubApi(t, {
      plex: enabled,
      statuses: [[status({ lastRun: active })]],
      sync: Response.json({ title: 'Plex sync already in progress', run: active }, { status: 409 }),
    });
    const response = await post('/plex', { entityType: 'movies' });
    assert.equal(response.status, 303);
    assert.equal(response.headers.get('Location'), '/plex?notice=running');
    assert.ok(calls.includes('POST /plex/movies/sync'));
    assert.equal(calls.includes('GET /plex/status'), false);

    const html = await (await get('/plex?notice=running')).text();
    assert.match(html, /A sync was already in progress/);
    assert.match(html, /Running since <!-- rmx:h:[a-z0-9]+ --><time datetime="2026-03-02T10:00:00Z"/);
  });

  it('should reject an entity type that cannot be synced without calling the API', async (t) => {
    const calls = stubApi(t, { plex: enabled });
    const response = await post('/plex', { entityType: 'music' });
    assert.equal(response.status, 400);
    assert.equal(calls.includes('POST /plex/music/sync'), false);
    assert.equal(calls.includes('GET /plex/status'), false);
  });

  it('should render the failure page when Plex cannot be reached', async (t) => {
    stubApi(t, { plex: enabled, sync: new Response('{}', { status: 502 }) });
    const response = await post('/plex', { entityType: 'movies' });
    assert.equal(response.status, 502);
    assert.match(await response.text(), /Couldn&#39;t reach Plex|Couldn't reach Plex/);
  });
});

describe('plex status route', () => {
  it('should forward the enabled types status uncached for the page script', async (t) => {
    stubApi(t, { plex: enabled, statuses: [[status(), status({ entityType: 'music', enabled: false })]] });
    const response = await get('/plex/status');
    assert.equal(response.status, 200);
    assert.equal(response.headers.get('Cache-Control'), 'no-store');
    const body = (await response.json()) as PlexEntityStatus[];
    assert.deepEqual(
      body.map((entry) => entry.entityType),
      ['movies']
    );
    assert.equal(body[0].lastRun?.state, 'succeeded');
  });

  it('should look the gate up once per poll', async (t) => {
    const calls = stubApi(t, { plex: enabled });
    await get('/plex/status');
    assert.equal(calls.filter((call) => call.endsWith('/config/integrations')).length, 1);
  });

  it('should answer 502 when the API fails', async (t) => {
    stubApi(t, { plex: enabled, statusFailure: new Response('boom', { status: 500 }) });
    assert.equal((await get('/plex/status')).status, 502);
  });
});
