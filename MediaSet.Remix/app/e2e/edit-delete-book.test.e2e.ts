import * as assert from 'remix/assert';
import { createTestServer } from 'remix/node-fetch-server/test';
import { describe, it } from 'remix/test';

import { router } from '../router.ts';

// Serves one book and a list, and records the PUT and DELETE requests.
function stubApi(t: { after: (fn: () => void) => void }) {
  const original = globalThis.fetch;
  const requests: string[] = [];
  globalThis.fetch = (async (input: RequestInfo | URL, init?: RequestInit) => {
    const request = new Request(input, init);
    const url = new URL(request.url);
    if (url.pathname.endsWith('/Books/b1')) {
      if (request.method !== 'GET') {
        requests.push(request.method);
        if (request.method === 'DELETE') await new Promise((resolve) => setTimeout(resolve, 800));
        return new Response(null, { status: 204 });
      }
      return Response.json({ id: 'b1', type: 'Books', title: 'Dune' });
    }
    if (url.pathname.endsWith('/pagedsearch')) {
      return Response.json({ items: [], totalCount: 0, page: 1, pageSize: 75, totalPages: 0 });
    }
    return Response.json([]);
  }) as typeof fetch;
  t.after(() => {
    globalThis.fetch = original;
  });
  return requests;
}

describe('edit and delete book (e2e)', () => {
  it('cancels the delete dialog, then confirms it and lands on the list', async (t) => {
    const requests = stubApi(t);
    const page = await t.serve(await createTestServer(router.fetch));
    await page.goto('/books/b1');
    await page.waitForLoadState('networkidle');

    await page.getByRole('link', { name: 'Delete' }).click();
    const dialog = page.getByRole('dialog');
    await dialog.waitFor();
    assert.equal(await dialog.getByRole('heading').innerText(), 'Delete Dune?');
    await dialog.getByRole('button', { name: 'Cancel' }).click();
    await dialog.waitFor({ state: 'hidden' });
    assert.deepEqual(requests, []);
    assert.equal(new URL(page.url()).pathname, '/books/b1');

    await page.getByRole('link', { name: 'Delete' }).click();
    await page.getByRole('dialog').getByRole('button', { name: 'Delete' }).click();
    const pending = page.getByRole('button', { name: 'Deleting…' });
    await pending.waitFor();
    assert.equal(await pending.isDisabled(), true);
    await page.waitForURL('**/books');
    assert.deepEqual(requests, ['DELETE']);
  });

  it('saves an edit and returns to the detail page', async (t) => {
    const requests = stubApi(t);
    const page = await t.serve(await createTestServer(router.fetch));
    await page.goto('/books/b1/edit');
    await page.waitForLoadState('networkidle');

    await page.locator('#title').fill('Dune Messiah');
    await page.getByRole('button', { name: 'Save' }).click();
    await page.waitForURL('**/books/b1');
    assert.deepEqual(requests, ['PUT']);
  });
});
