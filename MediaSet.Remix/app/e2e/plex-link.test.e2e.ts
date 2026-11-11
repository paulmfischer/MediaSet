import * as assert from 'remix/assert';
import { createTestServer } from 'remix/node-fetch-server/test';
import { describe, it } from 'remix/test';

import { router } from '../router.ts';

interface Request_ {
  method: string;
  path: string;
  body: unknown;
}

// Serves one unlinked entry, Plex search results and the scope, and records the link calls.
function stubApi(t: { after: (fn: () => void) => void }, entry: { title: string; isTvSeries: boolean }) {
  const original = globalThis.fetch;
  const requests: Request_[] = [];
  globalThis.fetch = (async (input: RequestInfo | URL, init?: RequestInit) => {
    const request = new Request(input, init);
    const url = new URL(request.url);
    if (url.pathname.endsWith('/config/integrations')) {
      return Response.json([{ key: 'plex', displayName: 'Plex', enabled: true, entityTypes: ['movies'] }]);
    }
    if (url.pathname.endsWith('/plex/movies/search')) {
      return Response.json([
        { title: entry.title.split(':')[0], plexItemId: '2001', year: 2010, library: 'TV Shows', type: 'show' },
        { title: entry.title, plexItemId: '1001', year: 1995, library: 'Movies', type: 'movie' },
      ]);
    }
    if (url.pathname.endsWith('/scope')) return Response.json({ kind: 'season', from: 2, to: 2 });
    if (url.pathname.endsWith('/link') && request.method === 'PUT') {
      requests.push({ method: 'PUT', path: url.pathname, body: await request.json() });
      return Response.json({});
    }
    if (url.pathname.endsWith('/Movies/m1')) return Response.json({ id: 'm1', type: 'Movies', ...entry });
    return Response.json([]);
  }) as typeof fetch;
  t.after(() => {
    globalThis.fetch = original;
  });
  return requests;
}

describe('link a movie to Plex (e2e)', () => {
  it('searches in the dialog, picks a result and lands on the detail page', async (t) => {
    const requests = stubApi(t, { title: 'Heat', isTvSeries: false });
    const page = await t.serve(await createTestServer(router.fetch));
    await page.goto('/movies/m1');
    await page.waitForLoadState('networkidle');

    await page.getByRole('button', { name: 'Link to Plex' }).click();
    const dialog = page.getByRole('dialog');
    await dialog.getByRole('heading', { name: 'Link to Plex: Heat' }).waitFor();
    assert.equal(await dialog.getByLabel('Search Plex for a movie').inputValue(), 'Heat');
    await dialog.getByText('1995').first().waitFor();
    assert.equal(new URL(page.url()).pathname, '/movies/m1');
    assert.deepEqual(requests, []);

    await dialog.getByRole('button', { name: 'Link to Heat (1995)' }).click();
    await page.waitForURL((url) => url.pathname === '/movies/m1');
    await dialog.waitFor({ state: 'hidden' });
    assert.deepEqual(requests, [{ method: 'PUT', path: '/plex/movies/items/m1/link', body: { plexItemId: '1001' } }]);
  });

  it('picks a show then its seasons for a TV entry, prefilled from the title', async (t) => {
    const requests = stubApi(t, { title: 'Show: Season 2', isTvSeries: true });
    const page = await t.serve(await createTestServer(router.fetch));
    await page.goto('/movies/m1');
    await page.waitForLoadState('networkidle');

    await page.getByRole('button', { name: 'Link to Plex' }).click();
    const dialog = page.getByRole('dialog');
    await dialog.getByRole('button', { name: 'Choose Show (2010)' }).click();
    await dialog.getByText('Which seasons does this entry cover?').waitFor();
    assert.equal(await dialog.getByRole('radio', { name: 'One season' }).isChecked(), true);
    assert.equal(await dialog.getByLabel('Season number').inputValue(), '2');

    await dialog.getByRole('button', { name: 'Link', exact: true }).click();
    await page.waitForURL((url) => url.pathname === '/movies/m1');
    assert.deepEqual(requests, [
      {
        method: 'PUT',
        path: '/plex/movies/items/m1/link',
        body: { plexItemId: '2001', scope: { kind: 'season', season: 2 } },
      },
    ]);
  });

  it('closes the dialog on Cancel without linking', async (t) => {
    const requests = stubApi(t, { title: 'Heat', isTvSeries: false });
    const page = await t.serve(await createTestServer(router.fetch));
    await page.goto('/movies/m1');
    await page.waitForLoadState('networkidle');

    await page.getByRole('button', { name: 'Link to Plex' }).click();
    const dialog = page.getByRole('dialog');
    await dialog.getByRole('button', { name: 'Cancel' }).click();
    await dialog.waitFor({ state: 'hidden' });
    assert.deepEqual(requests, []);
  });
});
