import * as assert from 'remix/assert';
import { createTestServer } from 'remix/node-fetch-server/test';
import { describe, it } from 'remix/test';

import { router } from '../router.ts';

const stats = {
  bookStats: { total: 2, totalFormats: 1, totalPages: 800, avgPages: 400, uniqueAuthors: 1 },
  movieStats: { total: 1, totalFormats: 1, totalTvSeries: 0 },
  gameStats: { total: 0 },
  musicStats: { total: 0 },
};

describe('home dashboard (e2e)', () => {
  it('switches the visible panel with the CSS-only tabs, by click and keyboard', async (t) => {
    const original = globalThis.fetch;
    globalThis.fetch = (async (input: RequestInfo | URL) => {
      const url = new URL(input instanceof Request ? input.url : String(input));
      return Response.json(url.pathname === '/stats' ? stats : []);
    }) as typeof fetch;
    t.after(() => {
      globalThis.fetch = original;
    });
    const page = await t.serve(await createTestServer(router.fetch));
    await page.setViewportSize({ width: 1280, height: 800 });
    await page.emulateMedia({ colorScheme: 'dark' });
    await page.goto('/');

    const books = page.locator('#dashboard-panel-books');
    const movies = page.locator('#dashboard-panel-movies');
    assert.equal(await books.isVisible(), true);
    assert.equal(await movies.isVisible(), false);

    await page.locator('label[for="dashboard-tab-movies"]').click();
    assert.equal(await movies.isVisible(), true);
    assert.equal(await books.isVisible(), false);

    await page.locator('#dashboard-tab-movies').focus();
    await page.keyboard.press('ArrowLeft');
    assert.equal(await books.isVisible(), true);
    assert.equal(await movies.isVisible(), false);
  });
});
