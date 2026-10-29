import * as assert from 'remix/assert';
import { createTestServer } from 'remix/node-fetch-server/test';
import { describe, it } from 'remix/test';

import { router } from '../router.ts';

const PAGE_SIZE = 75;
// 160 books so there are three pages; pages grows with the number, so sorting by pages reverses nothing
// unless the direction is honoured.
const books = Array.from({ length: 160 }, (_, index) => {
  const n = String(index + 1).padStart(3, '0');
  return {
    id: `b${n}`,
    type: 'Books',
    title: `Book ${n}`,
    authors: [`Author ${n}`],
    format: 'Paperback',
    pages: index + 1,
  };
});

// Serves a paged API from memory that honours search, sort and page, so navigation changes the rendered rows.
function stubApi(t: { after: (fn: () => void) => void }) {
  const original = globalThis.fetch;
  globalThis.fetch = (async (input: RequestInfo | URL) => {
    const url = new URL(input instanceof Request ? input.url : String(input));
    const search = (url.searchParams.get('searchText') ?? '').toLowerCase();
    const [field, direction] = (url.searchParams.get('orderBy') ?? 'title:asc').split(':');
    const key = field === 'pages' ? 'pages' : 'title';
    const matches = books
      .filter((book) => book.title.toLowerCase().includes(search))
      .sort((a, b) => (a[key] < b[key] ? -1 : a[key] > b[key] ? 1 : 0) * (direction === 'desc' ? -1 : 1));
    const page = Number(url.searchParams.get('page') ?? '1');
    const items = matches.slice((page - 1) * PAGE_SIZE, page * PAGE_SIZE);
    return Response.json({
      items,
      totalCount: matches.length,
      page,
      pageSize: PAGE_SIZE,
      totalPages: Math.ceil(matches.length / PAGE_SIZE),
    });
  }) as typeof fetch;
  t.after(() => {
    globalThis.fetch = original;
  });
}

describe('collection lists (e2e)', () => {
  it('sorts, pages, searches and clears with client navigation in the same document', async (t) => {
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
    const firstTitle = () => page.locator('tbody tr td:nth-child(2)').first().innerText();
    const rowCount = () => page.locator('tbody tr').count();

    assert.equal(await firstTitle(), 'Book 001');
    assert.equal(await rowCount(), PAGE_SIZE);

    // Sorting twice by pages reverses the order, and the URL carries the sort.
    await page.getByRole('link', { name: 'Pages' }).click();
    await page.waitForURL('**/books?orderBy=pages%3Aasc');
    await page.locator('th[aria-sort="ascending"]', { hasText: 'Pages' }).waitFor();
    await page.getByRole('link', { name: 'Pages' }).click();
    await page.waitForURL('**/books?orderBy=pages%3Adesc');
    await page.locator('th[aria-sort="descending"]', { hasText: 'Pages' }).waitFor();
    assert.equal(await firstTitle(), 'Book 160');

    // Next keeps the sort and shows the next slice.
    await page.getByRole('link', { name: 'Next' }).click();
    await page.waitForURL('**/books?orderBy=pages%3Adesc&page=2');
    await page.locator('tbody').getByText('Book 085', { exact: true }).waitFor();
    assert.equal(await firstTitle(), 'Book 085');

    // The page selector jumps straight to the last page.
    await page.getByRole('combobox', { name: 'Select page' }).selectOption('3');
    await page.waitForURL('**/books?orderBy=pages%3Adesc&page=3');
    await page.locator('tbody').getByText('Book 010', { exact: true }).waitFor();
    assert.equal(await rowCount(), 10);

    // Searching starts again from the first page and filters the rows.
    await page.getByRole('searchbox', { name: 'Search Books' }).fill('book 042');
    await page.getByRole('button', { name: 'Search' }).click();
    await page.waitForURL('**/books?orderBy=pages%3Adesc&searchText=book+042');
    await page.locator('tbody tr').nth(1).waitFor({ state: 'detached' });
    assert.equal(await firstTitle(), 'Book 042');

    // Clearing the search returns to the unfiltered list.
    await page.getByRole('link', { name: 'Clear search' }).click();
    await page.waitForURL('**/books');
    await page.locator('tbody').getByText('Book 001', { exact: true }).waitFor();
    assert.equal(await rowCount(), PAGE_SIZE);
    assert.equal(await page.getByRole('searchbox', { name: 'Search Books' }).inputValue(), '');
    assert.equal(await sameDocument(), true);
  });
});
