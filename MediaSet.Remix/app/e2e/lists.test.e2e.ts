import * as assert from 'remix/assert';
import { createTestServer } from 'remix/node-fetch-server/test';
import { describe, it } from 'remix/test';

import { router } from '../router.ts';

const books = [
  { id: 'b1', type: 'Books', title: 'Dune', authors: ['Frank Herbert'], format: 'Hardcover', pages: 412 },
  { id: 'b2', type: 'Books', title: 'Emma', authors: ['Jane Austen'], format: 'Paperback', pages: 474 },
];

// Serves a paged API from memory that honours search and page, so navigation changes the rendered rows.
function stubApi(t: { after: (fn: () => void) => void }) {
  const original = globalThis.fetch;
  globalThis.fetch = (async (input: RequestInfo | URL) => {
    const url = new URL(input instanceof Request ? input.url : String(input));
    const search = (url.searchParams.get('searchText') ?? '').toLowerCase();
    const items = books.filter((book) => book.title.toLowerCase().includes(search));
    return Response.json({ items, totalCount: items.length, page: 1, pageSize: 75, totalPages: 1 });
  }) as typeof fetch;
  t.after(() => {
    globalThis.fetch = original;
  });
}

describe('collection lists (e2e)', () => {
  it('searches, sorts and clears with client navigation in the same document', async (t) => {
    stubApi(t);
    const page = await t.serve(await createTestServer(router.fetch));
    await page.setViewportSize({ width: 1280, height: 800 });
    await page.goto('/books');
    await page.waitForLoadState('networkidle');
    await page.evaluate(() => {
      (window as unknown as { sameDocument: boolean }).sameDocument = true;
    });
    const sameDocument = () =>
      page.evaluate(() => (window as unknown as { sameDocument?: boolean }).sameDocument === true);
    const titles = () => page.locator('tbody tr td:nth-child(2)').allTextContents();

    assert.deepEqual(await titles(), ['Dune', 'Emma']);

    await page.getByRole('link', { name: 'Pages' }).click();
    await page.waitForURL('**/books?orderBy=pages%3Aasc');
    await page.locator('th[aria-sort="ascending"]', { hasText: 'Pages' }).waitFor();
    assert.equal(await sameDocument(), true);

    await page.getByRole('searchbox', { name: 'Search Books' }).fill('emma');
    await page.getByRole('button', { name: 'Search' }).click();
    await page.waitForURL('**/books?orderBy=pages%3Aasc&searchText=emma');
    await page.locator('tbody tr').nth(1).waitFor({ state: 'detached' });
    assert.deepEqual(await titles(), ['Emma']);
    assert.equal(await sameDocument(), true);

    await page.getByRole('link', { name: 'Clear search' }).click();
    await page.waitForURL('**/books');
    await page.locator('tbody tr').nth(1).waitFor();
    assert.deepEqual(await titles(), ['Dune', 'Emma']);
    assert.equal(await page.getByRole('searchbox', { name: 'Search Books' }).inputValue(), '');
    assert.equal(await sameDocument(), true);
  });
});
