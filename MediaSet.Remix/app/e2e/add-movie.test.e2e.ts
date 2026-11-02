import * as assert from 'remix/assert';
import { createTestServer } from 'remix/node-fetch-server/test';
import { describe, it } from 'remix/test';

import { router } from '../router.ts';

const metadata: Record<string, string[]> = {
  studios: ['Universal', '20th Century Fox'],
  genres: ['Horror'],
  format: ['Blu-ray', 'DVD'],
};

// Serves metadata and the (empty) movie list, and records the movies the form saves.
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
    if (url.pathname.endsWith('/Movies/new-id')) {
      return Response.json({ id: 'new-id', type: 'Movies', title: 'Alien' });
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

describe('add movie (e2e)', () => {
  it('shows the error with input preserved, then saves and redirects, without a full reload', async (t) => {
    const saved = stubApi(t);
    const page = await t.serve(await createTestServer(router.fetch));
    await page.setViewportSize({ width: 1280, height: 900 });
    await page.goto('/movies/add');
    await page.waitForLoadState('networkidle');
    await page.evaluate(() => {
      (window as unknown as { sameDocument: boolean }).sameDocument = true;
    });
    const sameDocument = () =>
      page.evaluate(() => (window as unknown as { sameDocument?: boolean }).sameDocument === true);

    await page.getByLabel('Runtime (minutes)').fill('117');
    await page.getByLabel('Is TV Series').check();
    const studios = page.getByRole('combobox', { name: 'Studios' });
    await studios.click();
    await page.getByRole('option', { name: '20th Century Fox' }).click();
    await page.getByRole('combobox', { name: 'Format' }).click();
    await page.getByRole('option', { name: 'Blu-ray' }).click();

    await page.getByRole('button', { name: 'Save' }).click();
    await page.locator('#title-error').waitFor();
    assert.equal(await page.locator('#title-error').innerText(), 'Title is required');
    assert.equal(await page.getByLabel('Runtime (minutes)').inputValue(), '117');
    assert.equal(await page.getByLabel('Is TV Series').isChecked(), true);
    await page.getByRole('button', { name: 'Remove 20th Century Fox' }).waitFor();
    await page.getByRole('button', { name: 'Clear Blu-ray' }).waitFor();
    assert.equal(new URL(page.url()).pathname, '/movies/add');
    assert.equal(saved.length, 0);

    await page.locator('#title').fill('Alien');
    await page.getByRole('button', { name: 'Save' }).click();
    await page.waitForURL('**/movies/new-id');

    assert.equal(await sameDocument(), true);
    assert.deepEqual(saved, [
      {
        type: 'Movies',
        title: 'Alien',
        format: 'Blu-ray',
        runtime: 117,
        isTvSeries: true,
        studios: ['20th Century Fox'],
        genres: [],
      },
    ]);
  });
});
