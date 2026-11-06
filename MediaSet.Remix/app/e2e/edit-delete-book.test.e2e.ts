import * as assert from 'remix/assert';
import { createTestServer } from 'remix/node-fetch-server/test';
import { describe, it } from 'remix/test';

import { submitButton } from './form-buttons.ts';
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

  it('leaves the edit form for the item page on Cancel, saving nothing', async (t) => {
    const requests = stubApi(t);
    const page = await t.serve(await createTestServer(router.fetch));
    await page.goto('/books/b1/edit');
    await page.waitForLoadState('networkidle');
    await page.locator('#title').fill('Not saved');

    await page.getByRole('button', { name: 'Cancel' }).click();
    await page.waitForURL((url) => url.pathname === '/books/b1');
    await page.getByRole('heading', { name: 'Dune', level: 1 }).waitFor();
    assert.deepEqual(requests, []);
  });

  it('saves an edit and returns to the detail page', async (t) => {
    const requests = stubApi(t);
    const page = await t.serve(await createTestServer(router.fetch));
    await page.goto('/books/b1/edit');
    await page.waitForLoadState('networkidle');

    await page.locator('#title').fill('Dune Messiah');
    await submitButton(page, 'edit', 'Book').click();
    await page.waitForURL('**/books/b1');
    assert.deepEqual(requests, ['PUT']);
  });
});

describe('edit book multi-select (e2e)', () => {
  it('renders the chosen values without duplicate sibling keys', async (t) => {
    const original = globalThis.fetch;
    globalThis.fetch = (async (input: RequestInfo | URL, init?: RequestInit) => {
      const url = new URL(new Request(input, init).url);
      if (url.pathname.endsWith('/Books/b1')) {
        return Response.json({
          id: 'b1',
          type: 'Books',
          title: 'Dune',
          authors: ['Frank Herbert'],
          genres: ['Sci-Fi'],
        });
      }
      return Response.json([]);
    }) as typeof fetch;
    t.after(() => {
      globalThis.fetch = original;
    });
    const page = await t.serve(await createTestServer(router.fetch));
    const warnings: string[] = [];
    page.on('console', (message) => {
      if (message.type() === 'warning' || message.type() === 'error') warnings.push(message.text());
    });
    await page.goto('/books/b1/edit');
    await page.waitForLoadState('networkidle');

    await page.getByRole('button', { name: 'Remove Frank Herbert' }).waitFor();
    assert.deepEqual(warnings, []);
  });
});

describe('edit book metadata lookup (e2e)', () => {
  it('looks up on an edit form and applies a result into it, keeping the saved image and saving only on Save', async (t) => {
    const original = globalThis.fetch;
    const requests: Array<{ method: string; body: Record<string, unknown> }> = [];
    globalThis.fetch = (async (input: RequestInfo | URL, init?: RequestInit) => {
      const request = new Request(input, init);
      const url = new URL(request.url);
      if (url.pathname.endsWith('/Books/b1')) {
        if (request.method === 'PUT') {
          requests.push({ method: 'PUT', body: (await request.json()) as Record<string, unknown> });
          return new Response(null, { status: 204 });
        }
        return Response.json({
          id: 'b1',
          type: 'Books',
          title: 'Dune',
          subtitle: 'Kept subtitle',
          coverImage: { filePath: 'books/b1.jpg' },
        });
      }
      if (url.pathname.endsWith('/config/lookup-capabilities')) return Response.json({ supportsBookLookup: true });
      // The match has no subtitle and no image, so those keep what the form had.
      if (url.pathname.includes('/lookup/Books/')) {
        return Response.json([
          { title: 'Dune Messiah', authors: [{ name: 'Frank Herbert', url: '' }], numberOfPages: 331 },
        ]);
      }
      return Response.json([]);
    }) as typeof fetch;
    t.after(() => {
      globalThis.fetch = original;
    });

    const page = await t.serve(await createTestServer(router.fetch));
    await page.setViewportSize({ width: 1280, height: 900 });
    // The saved cover is served by the API in production; answer its request with a one-pixel image.
    const pixel = Buffer.from(
      'iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg==',
      'base64'
    );
    await page.route('**/static/images/**', (route) => route.fulfill({ contentType: 'image/png', body: pixel }));
    await page.goto('/books/b1/edit');
    await page.waitForLoadState('networkidle');

    await page.getByLabel(/Remove the current image/).check();
    await page.getByRole('button', { name: 'Search by title and author' }).click();
    const dialog = page.getByRole('dialog', { name: 'Look up book' });
    await dialog.waitFor();
    assert.match(await dialog.innerText(), /Dune Messiah/);
    await dialog.getByRole('button', { name: 'Use this result' }).click();
    await dialog.waitFor({ state: 'detached' });

    // The form is filled, the saved image is still shown and the remove box is still ticked; nothing was saved.
    assert.equal(await page.locator('#title').inputValue(), 'Dune Messiah');
    assert.equal(await page.getByLabel('Pages').inputValue(), '331');
    assert.equal(await page.getByLabel('Subtitle').inputValue(), 'Kept subtitle');
    assert.match((await page.getByRole('img', { name: 'Cover preview' }).getAttribute('src')) ?? '', /books\/b1\.jpg$/);
    assert.match(await page.getByTestId('image-preview').innerText(), /Current cover image/);
    assert.equal(await page.getByLabel(/Remove the current image/).isChecked(), true);
    assert.equal(requests.length, 0);

    await page.getByLabel(/Remove the current image/).uncheck();
    await submitButton(page, 'edit', 'Book').click();
    await page.waitForURL('**/books/b1');
    assert.equal(requests.length, 1);
    assert.equal(requests[0].body.title, 'Dune Messiah');
    assert.equal(requests[0].body.pages, 331);
    assert.deepEqual(requests[0].body.coverImage, { filePath: 'books/b1.jpg' });
  });
});
