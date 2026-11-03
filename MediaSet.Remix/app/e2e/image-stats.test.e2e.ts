import * as assert from 'remix/assert';
import { createTestServer } from 'remix/node-fetch-server/test';
import { describe, it } from 'remix/test';

import { router } from '../router.ts';

const stats = {
  totalFiles: 12,
  totalSizeBytes: 2048,
  filesByEntityType: { books: 8, movies: 4 },
  sizeByEntityType: { books: 1024, movies: 1024 },
  brokenLinks: [{ entityId: 'b1', entityType: 'books', title: 'Dune', missingFilePath: 'books/b1.jpg' }],
  orphanedFiles: [{ relativePath: 'movies/old.jpg', sizeBytes: 1536 }],
  imageLookupFailures: [],
  lastUpdated: '2026-01-02T00:00:00Z',
};

describe('image statistics (e2e)', () => {
  it('switches the visible entity panel when a tab is clicked', async (t) => {
    const original = globalThis.fetch;
    globalThis.fetch = (async (input: RequestInfo | URL) => {
      const url = new URL(input instanceof Request ? input.url : String(input));
      return Response.json(url.pathname === '/stats/images' ? stats : []);
    }) as typeof fetch;
    t.after(() => {
      globalThis.fetch = original;
    });
    const page = await t.serve(await createTestServer(router.fetch));
    await page.setViewportSize({ width: 1280, height: 800 });
    await page.emulateMedia({ colorScheme: 'dark' });
    await page.goto('/image-stats');

    const books = page.locator('#image-stats-panel-books');
    const movies = page.locator('#image-stats-panel-movies');
    await page.waitForSelector('#image-stats-tab-movies[aria-pressed="false"]');
    assert.equal(await books.isVisible(), true);
    assert.equal(await movies.isVisible(), false);

    await page.locator('#image-stats-tab-movies').click();
    assert.equal(await movies.isVisible(), true);
    assert.equal(await books.isVisible(), false);
    assert.match((await movies.textContent()) ?? '', /movies\/old\.jpg/);

    await page.locator('#image-stats-tab-books').click();
    assert.equal(await books.isVisible(), true);
    assert.equal(await movies.isVisible(), false);
  });
});
