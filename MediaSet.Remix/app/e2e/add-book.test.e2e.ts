import * as assert from 'remix/assert';
import { createTestServer } from 'remix/node-fetch-server/test';
import { describe, it } from 'remix/test';

import { router } from '../router.ts';

const metadata: Record<string, string[]> = {
  authors: ['Frank Herbert'],
  genres: ['Science Fiction'],
  publisher: ['Ace'],
  format: ['Hardcover', 'Paperback'],
};

// Serves metadata and the (empty) book list, and records the books the form saves.
function stubApi(t: { after: (fn: () => void) => void }, saveDelayMs = 0) {
  const original = globalThis.fetch;
  const saved: Array<Record<string, unknown>> = [];
  globalThis.fetch = (async (input: RequestInfo | URL, init?: RequestInit) => {
    const request = new Request(input, init);
    const url = new URL(request.url);
    if (request.method === 'POST') {
      if (saveDelayMs) await new Promise((resolve) => setTimeout(resolve, saveDelayMs));
      saved.push((await request.json()) as Record<string, unknown>);
      return Response.json({ id: 'new-id' }, { status: 201 });
    }
    if (url.pathname.endsWith('/Books/new-id')) {
      return Response.json({ id: 'new-id', type: 'Books', title: 'Dune' });
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

describe('add book (e2e)', () => {
  it('shows the error with input preserved, then saves and redirects, without a full reload', async (t) => {
    const saved = stubApi(t);
    const page = await t.serve(await createTestServer(router.fetch));
    await page.setViewportSize({ width: 1280, height: 900 });
    await page.goto('/books/add');
    await page.waitForLoadState('networkidle');
    await page.evaluate(() => {
      (window as unknown as { sameDocument: boolean }).sameDocument = true;
    });
    const sameDocument = () =>
      page.evaluate(() => (window as unknown as { sameDocument?: boolean }).sameDocument === true);

    // Fill everything but the title, using the select islands.
    await page.getByLabel('Subtitle').fill('Book One');
    await page.getByLabel('Pages').fill('412');
    const authors = page.getByRole('combobox', { name: 'Authors' });
    await authors.click();
    await page.getByRole('option', { name: 'Frank Herbert' }).click();
    await authors.fill('Brian Herbert');
    await authors.press('ArrowDown');
    assert.equal(await page.getByRole('option', { name: /Add new Author: Brian Herbert/ }).count(), 1);
    await authors.press('Enter');
    await page.getByRole('combobox', { name: 'Format' }).click();
    await page.getByRole('option', { name: 'Hardcover' }).click();

    await page.getByRole('button', { name: 'Save' }).click();
    await page.locator('#title-error').waitFor();
    assert.equal(await page.locator('#title-error').innerText(), 'Title is required');
    assert.equal(await page.getByLabel('Subtitle').inputValue(), 'Book One');
    assert.equal(await page.getByLabel('Pages').inputValue(), '412');
    await page.getByRole('button', { name: 'Remove Frank Herbert' }).waitFor();
    await page.getByRole('button', { name: 'Remove Brian Herbert' }).waitFor();
    await page.getByRole('button', { name: 'Clear Hardcover' }).waitFor();
    assert.equal(new URL(page.url()).pathname, '/books/add');
    assert.equal(saved.length, 0);

    // Fix the error and save.
    await page.locator('#title').fill('Dune');
    await page.getByRole('button', { name: 'Remove Frank Herbert' }).click();
    await page.getByRole('button', { name: 'Save' }).click();
    await page.waitForURL('**/books/new-id');

    assert.equal(await sameDocument(), true);
    assert.deepEqual(saved, [
      {
        type: 'Books',
        title: 'Dune',
        subtitle: 'Book One',
        format: 'Hardcover',
        pages: 412,
        authors: ['Brian Herbert'],
        genres: [],
      },
    ]);
  });

  it('disables the save button and shows pending text while the submission is in flight', async (t) => {
    stubApi(t, 1500);
    const page = await t.serve(await createTestServer(router.fetch));
    await page.goto('/books/add');
    await page.waitForLoadState('networkidle');
    await page.locator('#title').fill('Dune');

    await page.getByRole('button', { name: 'Save' }).click();
    const pending = page.getByRole('button', { name: 'Saving…' });
    await pending.waitFor();
    assert.equal(await pending.isDisabled(), true);
    await page.waitForURL('**/books/new-id');
  });

  it('reopens a select on click, never re-offers a selected value as new, and lets Enter submit when idle', async (t) => {
    const saved = stubApi(t);
    const page = await t.serve(await createTestServer(router.fetch));
    await page.goto('/books/add');
    await page.waitForLoadState('networkidle');

    // Pick a format, then click the still-focused input: the list opens again.
    const format = page.getByRole('combobox', { name: 'Format' });
    await format.click();
    await page.getByRole('option', { name: 'Hardcover' }).click();
    assert.equal(await page.getByRole('listbox').count(), 0);
    await format.click();
    await page.getByRole('listbox').waitFor();
    await format.press('Escape');
    assert.equal(await page.getByRole('listbox').count(), 0);
    await format.click();
    await page.getByRole('listbox').waitFor();

    // A value that is already a chip is not offered as "add new" again.
    const authors = page.getByRole('combobox', { name: 'Authors' });
    await authors.fill('Brian Herbert');
    await authors.press('ArrowDown');
    await authors.press('Enter');
    await authors.fill('brian herbert');
    assert.equal(await page.getByRole('option', { name: /Add new Author/ }).count(), 0);
    await authors.press('Escape');
    await page.getByRole('button', { name: 'Remove Brian Herbert' }).waitFor();

    // With nothing typed or navigated to, Enter submits the form instead of picking an option.
    await page.locator('#title').fill('Dune');
    await authors.click();
    await authors.press('Enter');
    await page.waitForURL('**/books/new-id');
    assert.equal(saved.length, 1);
    assert.deepEqual(saved[0]?.authors, ['Brian Herbert']);
  });
});
