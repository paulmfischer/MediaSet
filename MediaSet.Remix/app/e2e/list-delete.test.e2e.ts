import * as assert from 'remix/assert';
import { createTestServer } from 'remix/node-fetch-server/test';
import { describe, it } from 'remix/test';

import { router } from '../router.ts';

// Serves two movies and records DELETE requests; a deleted movie disappears from the list.
function stubApi(t: { after: (fn: () => void) => void }, deleteStatus = 204) {
  const original = globalThis.fetch;
  const deleted: string[] = [];
  const movies = [
    { id: 'm1', type: 'Movies', title: 'Alien' },
    { id: 'm2', type: 'Movies', title: 'Brazil' },
  ];
  globalThis.fetch = (async (input: RequestInfo | URL, init?: RequestInit) => {
    const request = new Request(input, init);
    const url = new URL(request.url);
    const id = url.pathname.split('/').pop() ?? '';
    if (url.pathname.endsWith('/pagedsearch')) {
      const items = movies.filter((movie) => !deleted.includes(movie.id));
      return Response.json({ items, totalCount: items.length, page: 1, pageSize: 75, totalPages: 1 });
    }
    if (url.pathname.includes('/Movies/') && request.method === 'DELETE') {
      deleted.push(id);
      if (deleteStatus !== 204) {
        deleted.pop();
        return new Response(null, { status: deleteStatus });
      }
      return new Response(null, { status: 204 });
    }
    if (url.pathname.includes('/Movies/')) return Response.json(movies.find((movie) => movie.id === id) ?? null);
    return Response.json([]);
  }) as typeof fetch;
  t.after(() => {
    globalThis.fetch = original;
  });
  return deleted;
}

describe('list row delete (e2e)', () => {
  it('cancels the dialog, then confirms it for the clicked row and returns to the list without it', async (t) => {
    const deleted = stubApi(t);
    const page = await t.serve(await createTestServer(router.fetch));
    await page.setViewportSize({ width: 1280, height: 800 });
    await page.goto('/movies');
    await page.waitForLoadState('networkidle');

    await page.getByRole('link', { name: 'Delete Brazil' }).first().click();
    const dialog = page.getByRole('dialog');
    await dialog.waitFor();
    assert.equal(await dialog.getByRole('heading').innerText(), 'Delete Brazil?');
    await dialog.getByRole('button', { name: 'Cancel' }).click();
    await dialog.waitFor({ state: 'hidden' });
    assert.deepEqual(deleted, []);
    assert.equal(new URL(page.url()).pathname, '/movies');

    await page.getByRole('link', { name: 'Delete Brazil' }).first().click();
    await dialog.getByRole('button', { name: 'Delete' }).click();
    await page.getByRole('link', { name: 'Brazil', exact: true }).waitFor({ state: 'detached' });
    assert.equal(await page.locator('dialog[open]').count(), 0);
    assert.deepEqual(deleted, ['m2']);
    assert.equal(new URL(page.url()).pathname, '/movies');
    assert.equal(await page.getByRole('link', { name: 'Alien' }).first().isVisible(), true);
  });

  it('closes on Escape and returns focus to the delete link that opened it', async (t) => {
    const deleted = stubApi(t);
    const page = await t.serve(await createTestServer(router.fetch));
    await page.setViewportSize({ width: 1280, height: 800 });
    await page.goto('/movies');
    await page.waitForLoadState('networkidle');

    const link = page.getByRole('link', { name: 'Delete Alien' }).first();
    await link.click();
    await page.getByRole('dialog').waitFor();
    await page.keyboard.press('Escape');
    await page.getByRole('dialog').waitFor({ state: 'hidden' });
    assert.equal(await link.evaluate((node) => node === document.activeElement), true);
    assert.deepEqual(deleted, []);
  });

  it('does not hang when the delete fails', async (t) => {
    stubApi(t, 500);
    const page = await t.serve(await createTestServer(router.fetch));
    await page.setViewportSize({ width: 1280, height: 800 });
    await page.goto('/movies');
    await page.waitForLoadState('networkidle');

    await page.getByRole('link', { name: 'Delete Brazil' }).first().click();
    await page.getByRole('dialog').getByRole('button', { name: 'Delete' }).click();
    // Either the delete page's error is shown, or the dialog stays open with its Delete button usable again.
    await page.waitForFunction(
      () =>
        !document.querySelector('dialog[open]') ||
        (document.querySelector('dialog[open] [role=alert]') !== null &&
          [...document.querySelectorAll<HTMLButtonElement>('dialog[open] button[type=submit]')].every(
            (button) => !button.disabled
          ))
    );
    assert.equal(await page.getByRole('button', { name: 'Deleting…' }).count(), 0);
  });

  it('links each row to its delete confirmation page for use without JavaScript', async (t) => {
    stubApi(t);
    const page = await t.serve(await createTestServer(router.fetch));
    await page.setViewportSize({ width: 1280, height: 800 });
    await page.goto('/movies');

    const href = await page.getByRole('link', { name: 'Delete Alien' }).first().getAttribute('href');
    assert.equal(href, '/movies/m1/delete');
    await page.goto(href);
    assert.equal(await page.getByRole('heading', { level: 1 }).innerText(), 'Delete Movie');
  });
});
