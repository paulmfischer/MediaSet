import * as assert from 'remix/assert';
import { describe, it } from 'remix/test';

import { router } from '../../router.ts';

const existing = { id: 'b1', type: 'Books', title: 'Dune', coverImage: { filePath: 'books/b1.jpg' } };

type Saved = {
  method: string;
  contentType: string;
  json?: Record<string, unknown>;
  entity?: Record<string, unknown>;
  file?: File;
};

// Stubs the API: books are loaded, POST /Books and PUT /Books/b1 are recorded as JSON or multipart.
function stubApi(t: { after: (fn: () => void) => void }) {
  const original = globalThis.fetch;
  const saved: Saved[] = [];
  globalThis.fetch = (async (input: RequestInfo | URL, init?: RequestInit) => {
    const request = new Request(input, init);
    const url = new URL(request.url);
    const isSave = (request.method === 'POST' && url.pathname.endsWith('/Books')) || request.method === 'PUT';
    if (isSave) {
      const contentType = request.headers.get('content-type') ?? '';
      if (contentType.includes('multipart/form-data')) {
        const form = await request.formData();
        const file = form.get('coverImage');
        saved.push({
          method: request.method,
          contentType: 'multipart',
          entity: JSON.parse(String(form.get('entity'))) as Record<string, unknown>,
          ...(file instanceof File ? { file } : {}),
        });
      } else {
        saved.push({
          method: request.method,
          contentType: 'json',
          json: (await request.json()) as Record<string, unknown>,
        });
      }
      return request.method === 'POST'
        ? Response.json({ id: 'new-id' }, { status: 201 })
        : new Response(null, { status: 204 });
    }
    if (url.pathname.endsWith('/Books/b1')) return Response.json(existing);
    if (url.pathname.endsWith('/config/integrations')) {
      return Response.json([
        {
          key: 'tmdb',
          displayName: 'TMDB',
          enabled: true,
          logoPath: '/integrations/tmdb.svg',
          attributionText: 'Data by TMDB',
        },
        { key: 'off', displayName: 'Disabled Source', enabled: false, logoPath: '/integrations/off.svg' },
      ]);
    }
    return Response.json([]);
  }) as typeof fetch;
  t.after(() => {
    globalThis.fetch = original;
  });
  return saved;
}

const png = (bytes = 8) => new File([new Uint8Array(bytes)], 'cover.png', { type: 'image/png' });

function submit(path: string, fields: Record<string, string | File>) {
  const body = new FormData();
  for (const [name, value] of Object.entries(fields)) body.set(name, value);
  return router.fetch(new Request(new URL(path, 'http://localhost'), { method: 'POST', body }));
}

describe('book cover image on add', () => {
  it('serves a multipart form with the file and URL inputs', async (t) => {
    stubApi(t);
    const html = await (await router.fetch(new URL('/books/add', 'http://localhost'))).text();

    assert.match(html, /<form method="post" action="\/books\/add" enctype="multipart\/form-data"/);
    assert.match(html, /name="coverImage"[^>]*type="file"|type="file"[^>]*name="coverImage"/);
    assert.match(html, /name="imageUrl"/);
  });

  it('saves an uploaded file as multipart with the entity, and no URL', async (t) => {
    const saved = stubApi(t);
    const response = await submit('/books/add', {
      title: 'Dune',
      imageUrl: 'https://example.com/ignored.jpg',
      coverImage: png(),
    });

    assert.equal(response.status, 303);
    assert.equal(response.headers.get('location'), '/books/new-id');
    assert.equal(saved.length, 1);
    assert.equal(saved[0]?.contentType, 'multipart');
    assert.equal(saved[0]?.entity?.title, 'Dune');
    assert.equal(saved[0]?.entity?.imageUrl, undefined);
    assert.equal(saved[0]?.file?.name, 'cover.png');
    assert.equal(saved[0]?.file?.type, 'image/png');
  });

  it('saves a URL as JSON for the API to download', async (t) => {
    const saved = stubApi(t);
    const response = await submit('/books/add', { title: 'Dune', imageUrl: 'https://example.com/cover.jpg' });

    assert.equal(response.status, 303);
    assert.equal(saved[0]?.contentType, 'json');
    assert.equal(saved[0]?.json?.imageUrl, 'https://example.com/cover.jpg');
  });

  it('saves without an image when the file input is left empty', async (t) => {
    const saved = stubApi(t);
    const empty = new File([], '', { type: 'application/octet-stream' });
    const response = await submit('/books/add', { title: 'Dune', coverImage: empty });

    assert.equal(response.status, 303);
    assert.equal(saved[0]?.contentType, 'json');
  });

  it('answers 400 with the message and keeps the input for an unsupported file type', async (t) => {
    const saved = stubApi(t);
    const text = new File(['hello'], 'notes.txt', { type: 'text/plain' });
    const response = await submit('/books/add', { title: 'Dune', coverImage: text });

    assert.equal(response.status, 400);
    const html = await response.text();
    assert.match(html, /id="coverImage-error"[^>]*>Only JPEG and PNG images are allowed/);
    assert.match(html, /value="Dune"/);
    assert.equal(saved.length, 0);
  });

  it('answers 400 for an image URL that is not http or https', async (t) => {
    const saved = stubApi(t);
    const response = await submit('/books/add', { title: 'Dune', imageUrl: 'ftp://example.com/cover.jpg' });

    assert.equal(response.status, 400);
    assert.match(await response.text(), /id="imageUrl-error"[^>]*>Enter a valid http or https image URL/);
    assert.equal(saved.length, 0);
  });

  it('answers 413 when the file is over the size limit', async (t) => {
    const saved = stubApi(t);
    const response = await submit('/books/add', { title: 'Dune', coverImage: png(5 * 1024 * 1024 + 1) });

    assert.equal(response.status, 413);
    assert.match(await response.text(), /That upload is too large/);
    assert.equal(saved.length, 0);
  });

  it('answers 413 when the request has more than one file', async (t) => {
    const saved = stubApi(t);
    const body = new FormData();
    body.set('title', 'Dune');
    body.append('coverImage', png());
    body.append('coverImage', png());
    const response = await router.fetch(
      new Request(new URL('/books/add', 'http://localhost'), { method: 'POST', body })
    );

    assert.equal(response.status, 413);
    assert.equal(saved.length, 0);
  });
});

describe('book cover image on edit', () => {
  it('shows the current image and a remove option', async (t) => {
    stubApi(t);
    const html = await (await router.fetch(new URL('/books/b1/edit', 'http://localhost'))).text();

    assert.match(html, /books\/b1\.jpg/);
    assert.match(html, /Current cover image/);
    assert.match(html, /name="removeImage"/);
  });

  it('replaces the image with an uploaded file, keeping the existing fields', async (t) => {
    const saved = stubApi(t);
    const response = await submit('/books/b1/edit', { title: 'Dune', coverImage: png() });

    assert.equal(response.status, 303);
    assert.equal(saved[0]?.method, 'PUT');
    assert.equal(saved[0]?.contentType, 'multipart');
    assert.equal(saved[0]?.entity?.id, 'b1');
    assert.equal(saved[0]?.file?.name, 'cover.png');
  });

  it('keeps the saved image when nothing is chosen', async (t) => {
    const saved = stubApi(t);
    await submit('/books/b1/edit', { title: 'Dune' });

    assert.deepEqual(saved[0]?.json?.coverImage, { filePath: 'books/b1.jpg' });
  });

  it('drops the saved image when it is removed', async (t) => {
    const saved = stubApi(t);
    const response = await submit('/books/b1/edit', { title: 'Dune', removeImage: 'on' });

    assert.equal(response.status, 303);
    assert.equal(saved[0]?.contentType, 'json');
    assert.equal(saved[0]?.json?.coverImage, undefined);
  });

  it('answers 413 for an oversized upload', async (t) => {
    const saved = stubApi(t);
    const response = await submit('/books/b1/edit', { title: 'Dune', coverImage: png(5 * 1024 * 1024 + 1) });

    assert.equal(response.status, 413);
    assert.equal(saved.length, 0);
  });
});

describe('attribution on the detail page', () => {
  it('credits the enabled integrations only', async (t) => {
    stubApi(t);
    const html = await (await router.fetch(new URL('/books/b1', 'http://localhost'))).text();

    assert.match(html, /id="attribution-heading"/);
    assert.match(html, /aria-label="TMDB"/);
    assert.match(html, /Data by TMDB/);
    assert.doesNotMatch(html, /Disabled Source/);
  });
});
