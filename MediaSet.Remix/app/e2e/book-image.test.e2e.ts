import * as assert from 'remix/assert';
import { createTestServer } from 'remix/node-fetch-server/test';
import { describe, it } from 'remix/test';

import { router } from '../router.ts';

// 1x1 transparent PNG.
const PNG = Buffer.from(
  'iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg==',
  'base64'
);

// Serves empty lists and metadata, and records the multipart book the form saves.
function stubApi(t: { after: (fn: () => void) => void }) {
  const original = globalThis.fetch;
  const updates: Array<{ entity: Record<string, unknown>; file?: string }> = [];
  const saved: Array<{ entity: Record<string, unknown>; file: { name: string; type: string; size: number } }> = [];
  globalThis.fetch = (async (input: RequestInfo | URL, init?: RequestInit) => {
    const request = new Request(input, init);
    const url = new URL(request.url);
    if (request.method === 'PUT') {
      const type = request.headers.get('content-type') ?? '';
      if (type.includes('multipart/form-data')) {
        const form = await request.formData();
        const file = form.get('coverImage') as File;
        updates.push({ entity: JSON.parse(String(form.get('entity'))) as Record<string, unknown>, file: file.name });
      } else {
        updates.push({ entity: (await request.json()) as Record<string, unknown> });
      }
      return new Response(null, { status: 204 });
    }
    if (request.method === 'POST') {
      const form = await request.formData();
      const file = form.get('coverImage') as File;
      saved.push({
        entity: JSON.parse(String(form.get('entity'))) as Record<string, unknown>,
        file: { name: file.name, type: file.type, size: file.size },
      });
      return Response.json({ id: 'new-id' }, { status: 201 });
    }
    if (url.pathname.endsWith('/Books/b1')) {
      return Response.json({ id: 'b1', type: 'Books', title: 'Dune', coverImage: { filePath: 'books/b1.png' } });
    }
    if (url.pathname.endsWith('/Books/new-id')) return Response.json({ id: 'new-id', type: 'Books', title: 'Dune' });
    if (url.pathname.endsWith('/pagedsearch')) {
      return Response.json({ items: [], totalCount: 0, page: 1, pageSize: 75, totalPages: 0 });
    }
    return Response.json([]);
  }) as typeof fetch;
  t.after(() => {
    globalThis.fetch = original;
  });
  return { saved, updates };
}

describe('book cover image (e2e)', () => {
  it('previews a chosen file, then saves it with the book', async (t) => {
    const { saved } = stubApi(t);
    const page = await t.serve(await createTestServer(router.fetch));
    await page.goto('/books/add');
    await page.waitForLoadState('networkidle');
    assert.equal(await page.getByTestId('image-preview').count(), 0);

    await page.locator('#title').fill('Dune');
    await page.setInputFiles('#coverImage', { name: 'dune.png', mimeType: 'image/png', buffer: PNG });
    const preview = page.getByTestId('image-preview');
    await preview.waitFor();
    assert.match(await preview.innerText(), /Uploaded file: dune\.png/);
    assert.match((await preview.locator('img').getAttribute('src')) ?? '', /^blob:/);

    await page.getByRole('button', { name: 'Save' }).click();
    await page.waitForURL('**/books/new-id');

    assert.equal(saved.length, 1);
    assert.equal(saved[0]?.entity.title, 'Dune');
    assert.deepEqual(saved[0]?.file, { name: 'dune.png', type: 'image/png', size: PNG.length });
  });

  it('previews an image URL and says when it fails to load', async (t) => {
    stubApi(t);
    const page = await t.serve(await createTestServer(router.fetch));
    await page.goto('/books/add');
    await page.waitForLoadState('networkidle');

    // A URL that cannot load (nothing listens on this port) shows the failure message.
    await page.locator('#imageUrl').fill('http://127.0.0.1:9/missing.jpg');
    await page.getByText('Unable to load image').waitFor();
    assert.match(await page.getByTestId('image-preview').innerText(), /From URL/);

    // Clearing the URL removes the preview.
    await page.locator('#imageUrl').fill('');
    await page.getByTestId('image-preview').waitFor({ state: 'detached' });
  });

  it('previews a URL that loads, after typing pauses', async (t) => {
    stubApi(t);
    const page = await t.serve(await createTestServer(router.fetch));
    const requested: string[] = [];
    await page.route('http://images.test/**', (route) => {
      requested.push(route.request().url());
      return route.fulfill({ status: 200, contentType: 'image/png', body: PNG });
    });
    await page.goto('/books/add');
    await page.waitForLoadState('networkidle');

    // Typing keeps what is typed, and only the finished URL (after a pause) is requested.
    await page.locator('#imageUrl').click();
    await page.keyboard.type('http://images.test/typed.png', { delay: 20 });
    assert.equal(await page.locator('#imageUrl').inputValue(), 'http://images.test/typed.png');
    await page.getByTestId('image-preview').waitFor();
    assert.deepEqual(requested, ['http://images.test/typed.png']);
    await page.locator('#imageUrl').fill('');
    await page.getByTestId('image-preview').waitFor({ state: 'detached' });
    requested.length = 0;

    // A partial URL replaced before typing pauses is never requested; only the finished one is.
    await page.locator('#imageUrl').fill('http://images.test/co');
    await page.locator('#imageUrl').fill('http://images.test/cover.png');
    const preview = page.getByTestId('image-preview');
    await preview.waitFor();
    assert.match(await preview.innerText(), /From URL/);
    await page.waitForFunction(() => {
      const image = document.querySelector<HTMLImageElement>('[data-testid="image-preview"] img');
      return Boolean(image?.complete && image.naturalWidth > 0);
    });
    assert.equal(await page.getByText('Unable to load image').count(), 0);
    assert.deepEqual(requested, ['http://images.test/cover.png']);
  });

  it('shows the current cover on edit, then removes it', async (t) => {
    const { updates } = stubApi(t);
    const page = await t.serve(await createTestServer(router.fetch));
    await page.route('**/static/images/**', (route) =>
      route.fulfill({ status: 200, contentType: 'image/png', body: PNG })
    );
    await page.goto('/books/b1/edit');
    await page.waitForLoadState('networkidle');
    assert.match(await page.getByTestId('image-preview').innerText(), /Current cover image/);

    await page.getByLabel('Remove the current image').check();
    await page.getByRole('button', { name: 'Save' }).click();
    await page.waitForURL('**/books/b1');

    assert.equal(updates.length, 1);
    assert.equal(updates[0]?.entity.title, 'Dune');
    assert.equal(updates[0]?.entity.coverImage, undefined);
  });

  it('replaces the cover on edit with a chosen file', async (t) => {
    const { updates } = stubApi(t);
    const page = await t.serve(await createTestServer(router.fetch));
    await page.route('**/static/images/**', (route) =>
      route.fulfill({ status: 200, contentType: 'image/png', body: PNG })
    );
    await page.goto('/books/b1/edit');
    await page.waitForLoadState('networkidle');

    await page.setInputFiles('#coverImage', { name: 'new.png', mimeType: 'image/png', buffer: PNG });
    assert.match(await page.getByTestId('image-preview').innerText(), /Uploaded file: new\.png/);
    await page.getByRole('button', { name: 'Save' }).click();
    await page.waitForURL('**/books/b1');

    assert.equal(updates[0]?.file, 'new.png');
    assert.equal(updates[0]?.entity.id, 'b1');
  });
});
