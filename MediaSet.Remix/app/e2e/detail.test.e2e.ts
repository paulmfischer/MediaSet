import * as assert from 'remix/assert';
import { createTestServer } from 'remix/node-fetch-server/test';
import { describe, it } from 'remix/test';

import { router } from '../router.ts';

const book = {
  id: 'b1',
  type: 'Books',
  title: 'Dune',
  subtitle: 'Book One',
  format: 'Hardcover',
  pages: 412,
  authors: ['Frank Herbert', 'Brian Herbert'],
  genres: ['Science Fiction'],
  coverImage: { filePath: 'books/b1.jpg' },
};

// Serves the list and the one book; any other item is missing.
function stubApi(t: { after: (fn: () => void) => void }) {
  const original = globalThis.fetch;
  globalThis.fetch = (async (input: RequestInfo | URL) => {
    const url = new URL(input instanceof Request ? input.url : String(input));
    if (url.pathname.endsWith('/pagedsearch')) {
      return Response.json({ items: [book], totalCount: 1, page: 1, pageSize: 75, totalPages: 1 });
    }
    if (url.pathname === '/Books/b1') return Response.json(book);
    if (url.pathname.startsWith('/static/')) return new Response(null, { status: 404 });
    return new Response('missing', { status: 404 });
  }) as typeof fetch;
  t.after(() => {
    globalThis.fetch = original;
  });
}

describe('item detail (e2e)', () => {
  it('opens an item from the list with client navigation and shows its fields and links', async (t) => {
    stubApi(t);
    const page = await t.serve(await createTestServer(router.fetch));
    await page.setViewportSize({ width: 1280, height: 800 });
    await page.goto('/books');
    await page.waitForLoadState('networkidle');
    await page.evaluate(() => {
      (window as unknown as { sameDocument: boolean }).sameDocument = true;
    });

    await page.getByRole('link', { name: 'Dune: Book One' }).click();
    await page.waitForURL('**/books/b1');
    await page.getByRole('heading', { name: 'Dune', level: 1 }).waitFor();

    assert.equal(await page.getByRole('heading', { name: 'Book One', level: 2 }).count(), 1);
    assert.equal(await page.locator('[data-field="Authors"]').innerText(), 'Frank Herbert, Brian Herbert');
    assert.equal(await page.locator('[data-field="Pages"]').innerText(), '412');
    assert.equal(await page.getByRole('link', { name: 'Edit' }).getAttribute('href'), '/books/b1/edit');
    assert.equal(await page.getByRole('link', { name: 'Delete' }).getAttribute('href'), '/books/b1/delete');
    assert.equal(await page.getByRole('img', { name: 'Dune' }).count(), 1);
    assert.equal(
      await page.evaluate(() => (window as unknown as { sameDocument?: boolean }).sameDocument === true),
      true
    );
  });

  it('shows the not-found page for a missing item, with a way back to the list', async (t) => {
    stubApi(t);
    const page = await t.serve(await createTestServer(router.fetch));
    const response = await page.goto('/books/nope');
    await page.waitForLoadState('networkidle');

    assert.equal(response?.status(), 404);
    await page.getByRole('heading', { name: 'Book not found' }).waitFor();
    await page.getByRole('link', { name: 'Back to Books' }).click();
    await page.waitForURL('**/books');
    await page.getByRole('link', { name: 'Dune: Book One' }).waitFor();
  });
});
