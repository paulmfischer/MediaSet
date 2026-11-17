import * as assert from 'remix/assert';
import { createTestServer } from 'remix/node-fetch-server/test';
import { describe, it } from 'remix/test';

import { router } from '../router.ts';

interface ApiRequest {
  method: string;
  path: string;
  body: unknown;
}

const linkedPlex = {
  musicId: 'a1',
  plexItemId: '7001',
  onPlex: true,
  broken: false,
  linkSource: 'manual',
  playCount: 3,
  library: 'Music',
  lastSyncedAt: new Date().toISOString(),
};

// Serves one album and its Plex search, and records the link and unlink calls. Unlinking clears the stored Plex data.
function stubApi(t: { after: (fn: () => void) => void }, options: { linked: boolean; ambiguous: boolean }) {
  const original = globalThis.fetch;
  const requests: ApiRequest[] = [];
  let linked = options.linked;
  globalThis.fetch = (async (input: RequestInfo | URL, init?: RequestInit) => {
    const request = new Request(input, init);
    const url = new URL(request.url);
    if (url.pathname.endsWith('/config/integrations')) {
      return Response.json([{ key: 'plex', displayName: 'Plex', enabled: true, entityTypes: ['music'] }]);
    }
    if (url.pathname.endsWith('/plex/music/search')) {
      const album = { title: 'Abbey Road', year: 1969, library: 'Music', type: 'album', artist: 'The Beatles' };
      return Response.json([
        { ...album, plexItemId: '7001' },
        ...(options.ambiguous ? [{ ...album, plexItemId: '7002', title: 'Abbey Road (Remaster)' }] : []),
      ]);
    }
    if (url.pathname.endsWith('/link') && (request.method === 'PUT' || request.method === 'DELETE')) {
      requests.push({
        method: request.method,
        path: url.pathname,
        body: request.method === 'PUT' ? await request.json() : null,
      });
      linked = request.method === 'PUT';
      return request.method === 'PUT' ? Response.json({}) : new Response(null, { status: 204 });
    }
    if (url.pathname.endsWith('/Musics/a1')) {
      return Response.json({
        id: 'a1',
        type: 'Musics',
        title: 'Abbey Road',
        artist: 'The Beatles',
        ...(linked ? { plex: linkedPlex } : {}),
      });
    }
    return Response.json([]);
  }) as typeof fetch;
  t.after(() => {
    globalThis.fetch = original;
  });
  return requests;
}

describe('link and unlink an album with Plex (e2e)', () => {
  it('links a single match straight away and shows the Plex card afterwards', async (t) => {
    const requests = stubApi(t, { linked: false, ambiguous: false });
    const page = await t.serve(await createTestServer(router.fetch));
    await page.goto('/musics/a1');
    await page.waitForLoadState('networkidle');

    await page.getByRole('button', { name: 'Link to Plex' }).click();
    await page.getByRole('dialog').waitFor({ state: 'hidden' });
    assert.deepEqual(requests, [{ method: 'PUT', path: '/plex/music/items/a1/link', body: { plexItemId: '7001' } }]);
    await page.locator('[data-plex-state="onPlex"]').waitFor();
  });

  it('shows the results with their artist and links the chosen album', async (t) => {
    const requests = stubApi(t, { linked: false, ambiguous: true });
    const page = await t.serve(await createTestServer(router.fetch));
    await page.goto('/musics/a1');
    await page.waitForLoadState('networkidle');

    await page.getByRole('button', { name: 'Link to Plex' }).click();
    const dialog = page.getByRole('dialog');
    await dialog.getByLabel('Search Plex for an album').waitFor();
    assert.equal(await dialog.locator('[data-plex-artist]').count(), 2);
    await dialog.getByRole('button', { name: 'Link to Abbey Road (Remaster) (1969)' }).click();
    await dialog.waitFor({ state: 'hidden' });
    assert.deepEqual(requests, [{ method: 'PUT', path: '/plex/music/items/a1/link', body: { plexItemId: '7002' } }]);
  });

  it('unlinks through the confirmation dialog and offers Link to Plex again', async (t) => {
    const requests = stubApi(t, { linked: true, ambiguous: false });
    const page = await t.serve(await createTestServer(router.fetch));
    await page.goto('/musics/a1');
    await page.waitForLoadState('networkidle');

    assert.equal(await page.getByRole('button', { name: 'Refresh' }).count(), 0);
    await page.locator('summary[aria-label="Plex actions"]').click();
    await page.getByRole('button', { name: 'Unlink' }).click();
    const dialog = page.getByRole('dialog', { name: /Unlink/ });
    await dialog.getByText('The album is kept').waitFor();
    await dialog.getByRole('button', { name: 'Unlink' }).click();
    await dialog.waitFor({ state: 'hidden' });
    assert.deepEqual(requests, [{ method: 'DELETE', path: '/plex/music/items/a1/link', body: null }]);
    await page.locator('[data-plex-state="unlinked"]').waitFor();
  });
});
