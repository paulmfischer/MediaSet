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

    await page.getByRole('link', { name: 'Dune: Book One', exact: true }).click();
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
    await page.getByRole('link', { name: 'Dune: Book One', exact: true }).first().waitFor();
  });

  it('lines every value up in one column, with long values wrapping inside it', async (t) => {
    const long = 'A very long description that goes on and on '.repeat(12);
    const game = {
      id: 'g1',
      type: 'Games',
      title: 'Alan Wake',
      platform: 'Xbox 360',
      developers: ['Remedy'],
      publishers: ['Microsoft Game Studios', 'Xbox Game Studios', 'Another Publisher Name'],
      description: long,
    };
    const original = globalThis.fetch;
    globalThis.fetch = (async (input: RequestInfo | URL) => {
      const url = new URL(input instanceof Request ? input.url : String(input));
      if (url.pathname === '/Games/g1') return Response.json(game);
      return new Response('missing', { status: 404 });
    }) as typeof fetch;
    t.after(() => {
      globalThis.fetch = original;
    });
    const page = await t.serve(await createTestServer(router.fetch));
    for (const width of [1280, 390]) {
      await page.setViewportSize({ width, height: 800 });
      await page.goto('/games/g1');
      await page.waitForLoadState('networkidle');

      const boxes = await page.locator('[data-field]').evaluateAll((nodes) =>
        nodes.map((node) => {
          const value = node.getBoundingClientRect();
          const label = node.previousElementSibling!.getBoundingClientRect();
          return { left: Math.round(value.left), labelRight: Math.round(label.right), right: Math.round(value.right) };
        })
      );
      const lefts = new Set(boxes.map((box) => box.left));
      assert.equal(lefts.size, 1, `values share one left edge at ${width}px`);
      for (const box of boxes) assert.ok(box.left > box.labelRight, 'a value starts after its label');
      assert.ok(Math.max(...boxes.map((box) => box.right)) <= width, 'nothing overflows the viewport');
    }
  });
});
