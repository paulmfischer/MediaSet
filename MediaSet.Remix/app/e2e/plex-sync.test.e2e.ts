import * as assert from 'remix/assert';
import { createTestServer } from 'remix/node-fetch-server/test';
import { describe, it } from 'remix/test';

import { router } from '../router.ts';

interface StubOptions {
  plexEnabled: boolean;
  /** How many status reads report a running run before it shows as finished. */
  runningReads?: number;
}

const finished = {
  entityType: 'movies',
  trigger: 'manual',
  state: 'succeeded',
  startedAt: '2026-03-01T03:00:00Z',
  endedAt: '2026-03-01T03:04:00Z',
  itemsScanned: 120,
  matchedCount: 100,
  unmatchedCount: 20,
  erroredCount: 0,
};
const running = { ...finished, state: 'running', endedAt: null, startedAt: '2026-03-02T10:00:00Z' };

// Serves the integrations, the Plex status (a run that is running for the first reads, then finished) and the sync.
function stubApi(t: { after: (fn: () => void) => void }, options: StubOptions) {
  const original = globalThis.fetch;
  const requests: string[] = [];
  let runningLeft = options.runningReads ?? 0;
  globalThis.fetch = (async (input: RequestInfo | URL, init?: RequestInit) => {
    const request = new Request(input, init);
    const url = new URL(request.url);
    requests.push(`${request.method} ${url.pathname}`);
    if (url.pathname.endsWith('/config/integrations')) {
      return Response.json(
        options.plexEnabled ? [{ key: 'plex', displayName: 'Plex', enabled: true, entityTypes: ['movies'] }] : []
      );
    }
    if (url.pathname === '/plex/status') {
      const lastRun = runningLeft > 0 ? running : finished;
      if (runningLeft > 0) runningLeft--;
      return Response.json([
        { entityType: 'movies', enabled: true, lastRun, nextRun: '2026-03-08T03:00:00Z', schedulePolicy: 'queue' },
      ]);
    }
    if (url.pathname === '/plex/movies/sync') {
      runningLeft = 1;
      return Response.json(running, { status: 202 });
    }
    return Response.json([]);
  }) as typeof fetch;
  t.after(() => {
    globalThis.fetch = original;
  });
  return requests;
}

describe('Plex sync page (e2e)', () => {
  it('shows the gear menu link only when Plex is enabled', async (t) => {
    stubApi(t, { plexEnabled: true });
    const on = await t.serve(await createTestServer(router.fetch));
    await on.goto('/');
    await on.waitForLoadState('networkidle');
    await on.getByRole('button', { name: 'Tools' }).click();
    await on.getByRole('link', { name: 'Plex' }).click();
    await on.waitForURL((url) => url.pathname === '/plex');
    await on.getByRole('heading', { name: 'Plex Statistics' }).waitFor();
  });

  it('has no gear menu link and no /plex page when Plex is off', async (t) => {
    stubApi(t, { plexEnabled: false });
    const page = await t.serve(await createTestServer(router.fetch));
    await page.goto('/');
    await page.waitForLoadState('networkidle');
    await page.getByRole('button', { name: 'Tools' }).click();
    await page.getByRole('link', { name: 'Image Statistics' }).waitFor();
    assert.equal(await page.getByRole('link', { name: 'Plex', exact: true }).count(), 0);

    const response = await page.goto('/plex');
    assert.equal(response?.status(), 404);
  });

  it('starts a sync, shows it running, then updates itself when the run ends', async (t) => {
    const requests = stubApi(t, { plexEnabled: true });
    const page = await t.serve(await createTestServer(router.fetch));
    await page.goto('/plex');
    await page.waitForLoadState('networkidle');

    const section = page.getByRole('region', { name: 'Movies' });
    await section.getByText('Succeeded').waitFor();
    await section.getByText('Items scanned').waitFor();

    await section.getByRole('button', { name: 'Sync now' }).click();
    await section.getByText(/Running since/).waitFor();
    assert.equal(await section.getByRole('button', { name: 'Sync now' }).isDisabled(), true);
    assert.ok(requests.includes('POST /plex/movies/sync'));

    // The poller reloads the page once the status reports the run finished.
    await section.getByText('Succeeded').waitFor({ timeout: 15_000 });
    await section.getByText(/Running since/).waitFor({ state: 'detached' });
    assert.equal(await section.getByRole('button', { name: 'Sync now' }).isEnabled(), true);
  });
});
