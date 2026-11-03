import * as assert from 'remix/assert';
import { createTestServer } from 'remix/node-fetch-server/test';
import { describe, it } from 'remix/test';

import { router } from '../router.ts';

const metadata: Record<string, string[]> = {
  artist: ['Pink Floyd'],
  label: ['Harvest'],
  genres: ['Rock', 'Pop'],
  format: ['CD', 'Vinyl'],
};

// Serves metadata and the (empty) music list, and records the albums the form saves.
function stubApi(t: { after: (fn: () => void) => void }) {
  const original = globalThis.fetch;
  const saved: Array<Record<string, unknown>> = [];
  globalThis.fetch = (async (input: RequestInfo | URL, init?: RequestInit) => {
    const request = new Request(input, init);
    const url = new URL(request.url);
    if (request.method === 'POST') {
      saved.push((await request.json()) as Record<string, unknown>);
      return Response.json({ id: 'new-id' }, { status: 201 });
    }
    if (url.pathname.endsWith('/Musics/new-id')) {
      return Response.json({ id: 'new-id', type: 'Musics', title: 'The Wall', artist: 'Pink Floyd' });
    }
    if (url.pathname.endsWith('/pagedsearch')) {
      return Response.json({ items: [], totalCount: 0, page: 1, pageSize: 75, totalPages: 0 });
    }
    return Response.json(metadata[url.pathname.split('/').pop() ?? ''] ?? []);
  }) as typeof fetch;
  t.after(() => {
    globalThis.fetch = original;
  });
  return saved;
}

describe('add music (e2e)', () => {
  it('shows the error with input preserved, then saves and redirects, without a full reload', async (t) => {
    const saved = stubApi(t);
    const page = await t.serve(await createTestServer(router.fetch));
    await page.setViewportSize({ width: 1280, height: 900 });
    await page.goto('/musics/add');
    await page.waitForLoadState('networkidle');
    await page.evaluate(() => {
      (window as unknown as { sameDocument: boolean }).sameDocument = true;
    });
    const sameDocument = () =>
      page.evaluate(() => (window as unknown as { sameDocument?: boolean }).sameDocument === true);

    await page.getByLabel('Tracks').fill('26');
    await page.getByLabel('Duration (minutes)').fill('81');
    await page.getByLabel('Duration (seconds)').fill('5');
    await page.getByRole('combobox', { name: 'Genres' }).click();
    await page.getByRole('option', { name: 'Rock' }).click();
    await page.getByRole('combobox', { name: 'Format' }).click();
    await page.getByRole('option', { name: 'Vinyl' }).click();

    await page.getByRole('button', { name: 'Save' }).click();
    await page.locator('#title-error').waitFor();
    assert.equal(await page.locator('#title-error').innerText(), 'Title is required');
    assert.equal(await page.locator('#artist-error').innerText(), 'Artist is required');
    assert.equal(await page.getByLabel('Tracks').inputValue(), '26');
    assert.equal(await page.getByLabel('Duration (minutes)').inputValue(), '81');
    await page.getByRole('button', { name: 'Remove Rock' }).waitFor();
    await page.getByRole('button', { name: 'Clear Vinyl' }).waitFor();
    assert.equal(new URL(page.url()).pathname, '/musics/add');
    assert.equal(saved.length, 0);

    await page.locator('#title').fill('The Wall');
    await page.getByRole('combobox', { name: 'Artist' }).click();
    await page.getByRole('option', { name: 'Pink Floyd' }).click();
    await page.getByRole('button', { name: 'Save' }).click();
    await page.waitForURL('**/musics/new-id');

    assert.equal(await sameDocument(), true);
    assert.deepEqual(saved, [
      {
        type: 'Musics',
        title: 'The Wall',
        artist: 'Pink Floyd',
        format: 'Vinyl',
        tracks: 26,
        duration: 4865000,
        genres: ['Rock'],
      },
    ]);
  });
});
