import * as assert from 'remix/assert';
import { createTestServer } from 'remix/node-fetch-server/test';
import { describe, it } from 'remix/test';

import { router } from '../router.ts';

const metadata: Record<string, string[]> = {
  developers: ['Nintendo', 'Capcom'],
  genres: ['Adventure'],
  platform: ['Switch'],
  format: ['Cartridge', 'Digital'],
};

// Serves metadata and the (empty) game list, and records the games the form saves.
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
    if (url.pathname.endsWith('/Games/new-id')) {
      return Response.json({ id: 'new-id', type: 'Games', title: 'Zelda' });
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

describe('add game (e2e)', () => {
  it('shows the error with input preserved, then saves and redirects, without a full reload', async (t) => {
    const saved = stubApi(t);
    const page = await t.serve(await createTestServer(router.fetch));
    await page.setViewportSize({ width: 1280, height: 900 });
    await page.goto('/games/add');
    await page.waitForLoadState('networkidle');
    await page.evaluate(() => {
      (window as unknown as { sameDocument: boolean }).sameDocument = true;
    });
    const sameDocument = () =>
      page.evaluate(() => (window as unknown as { sameDocument?: boolean }).sameDocument === true);

    await page.getByLabel('Rating').fill('E10+');
    await page.getByRole('combobox', { name: 'Developers' }).click();
    await page.getByRole('option', { name: 'Nintendo' }).click();
    await page.getByRole('combobox', { name: 'Format' }).click();
    await page.getByRole('option', { name: 'Cartridge' }).click();

    await page.getByRole('button', { name: /^(Add (Book|Movie|Game|Music)|Update)$/ }).click();
    await page.locator('#title-error').waitFor();
    assert.equal(await page.locator('#title-error').innerText(), 'Title is required');
    assert.equal(await page.getByLabel('Rating').inputValue(), 'E10+');
    await page.getByRole('button', { name: 'Remove Nintendo' }).waitFor();
    await page.getByRole('button', { name: 'Clear Cartridge' }).waitFor();
    assert.equal(new URL(page.url()).pathname, '/games/add');
    assert.equal(saved.length, 0);

    await page.locator('#title').fill('Zelda');
    await page.getByRole('button', { name: /^(Add (Book|Movie|Game|Music)|Update)$/ }).click();
    await page.waitForURL('**/games/new-id');

    assert.equal(await sameDocument(), true);
    assert.deepEqual(saved, [
      {
        type: 'Games',
        title: 'Zelda',
        format: 'Cartridge',
        rating: 'E10+',
        developers: ['Nintendo'],
        publishers: [],
        genres: [],
      },
    ]);
  });
});
