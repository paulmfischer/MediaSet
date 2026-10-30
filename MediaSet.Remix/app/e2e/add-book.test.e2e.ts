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

describe('add book metadata lookup (e2e)', () => {
  it('looks a title up, shows the results in a modal dialog and applies the chosen one to the form', async (t) => {
    const original = globalThis.fetch;
    const saved: Array<Record<string, unknown>> = [];
    const match = (title: string, publisher: string) => ({
      title,
      subtitle: 'Book One',
      authors: [{ name: 'Frank Herbert', url: '' }],
      numberOfPages: 412,
      publishers: [{ name: publisher }],
      publishDate: '1990',
      subjects: [{ name: 'Science Fiction', url: '' }],
      format: 'Paperback',
    });
    globalThis.fetch = (async (input: RequestInfo | URL, init?: RequestInit) => {
      const request = new Request(input, init);
      const url = new URL(request.url);
      if (request.method === 'POST') {
        saved.push((await request.json()) as Record<string, unknown>);
        return Response.json({ id: 'new-id' }, { status: 201 });
      }
      if (url.pathname.endsWith('/config/lookup-capabilities')) return Response.json({ supportsBookLookup: true });
      if (url.pathname.includes('/lookup/Books/'))
        return Response.json([match('Dune', 'Ace'), match('Dune Messiah', 'Berkley')]);
      if (url.pathname.endsWith('/Books/new-id')) return Response.json({ id: 'new-id', type: 'Books', title: 'Dune' });
      return Response.json(metadata[url.pathname.split('/').pop() ?? ''] ?? []);
    }) as typeof fetch;
    t.after(() => {
      globalThis.fetch = original;
    });

    const page = await t.serve(await createTestServer(router.fetch));
    await page.setViewportSize({ width: 1280, height: 900 });
    await page.goto('/books/add');
    await page.waitForLoadState('networkidle');

    await page.locator('#title').fill('Dune');
    await page.getByLabel('Pages').fill('1');
    // A cover over the upload limit must not get in the way of a lookup, which doesn't send it.
    await page
      .getByLabel(/Upload an image/)
      .setInputFiles({ name: 'big.png', mimeType: 'image/png', buffer: Buffer.alloc(6 * 1024 * 1024) });
    await page.getByRole('button', { name: 'Look up by title' }).click();

    // The results open in a modal dialog; the form behind it keeps what was typed.
    const dialog = page.getByRole('dialog', { name: 'Look up book' });
    await dialog.waitFor();
    assert.equal(await page.evaluate(() => document.querySelector('dialog')?.matches(':modal')), true);
    assert.equal(await dialog.getByRole('listitem').count(), 2);
    assert.match(await dialog.innerText(), /Dune Messiah/);

    // Closing the dialog leaves the form as it was.
    await dialog.getByRole('button', { name: 'Close' }).click();
    await dialog.waitFor({ state: 'hidden' });
    await page.waitForFunction(() => document.activeElement?.id === 'title', undefined, { timeout: 2000 });
    assert.equal(await page.getByLabel('Pages').inputValue(), '1');
    await page.getByRole('button', { name: 'Look up by title' }).click();
    await dialog.waitFor();

    // Applying the second match fills the form, including the select islands.
    await dialog.getByRole('button', { name: 'Use this result' }).nth(1).click();
    await dialog.waitFor({ state: 'detached' });
    await page.waitForFunction(() => document.activeElement?.id === 'title', undefined, { timeout: 2000 });
    assert.equal(await page.locator('#title').inputValue(), 'Dune Messiah');
    assert.equal(await page.getByLabel('Subtitle').inputValue(), 'Book One');
    assert.equal(await page.getByLabel('Pages').inputValue(), '412');
    assert.equal(await page.getByLabel('Publication Date').inputValue(), '1990');
    await page.getByRole('button', { name: 'Remove Frank Herbert' }).waitFor();
    await page.getByRole('button', { name: 'Clear Berkley' }).waitFor();
    await page.getByRole('button', { name: 'Clear Paperback' }).waitFor();

    await page.getByRole('button', { name: 'Save' }).click();
    await page.waitForURL('**/books/new-id');
    assert.deepEqual(saved, [
      {
        type: 'Books',
        title: 'Dune Messiah',
        subtitle: 'Book One',
        format: 'Paperback',
        pages: 412,
        publicationDate: '1990',
        publisher: 'Berkley',
        authors: ['Frank Herbert'],
        genres: ['Science Fiction'],
      },
    ]);
  });
});
