import * as assert from 'remix/assert';
import { describe, it } from 'remix/test';

import { router } from '../../router.ts';

const existing = {
  id: 'm1',
  type: 'Movies',
  title: 'Alien',
  isTvSeries: true,
  imageUrl: 'https://example.com/alien.jpg',
  coverImage: { filePath: 'movies/m1.jpg' },
};

type Saved = {
  method: string;
  contentType: string;
  json?: Record<string, unknown>;
  entity?: Record<string, unknown>;
  file?: File;
};

// Stubs the API: the movie is loaded, POST /Movies and PUT /Movies/m1 are recorded as JSON or multipart.
function stubApi(t: { after: (fn: () => void) => void }) {
  const original = globalThis.fetch;
  const saved: Saved[] = [];
  globalThis.fetch = (async (input: RequestInfo | URL, init?: RequestInit) => {
    const request = new Request(input, init);
    const url = new URL(request.url);
    const isSave = (request.method === 'POST' && url.pathname.endsWith('/Movies')) || request.method === 'PUT';
    if (isSave) {
      if ((request.headers.get('content-type') ?? '').includes('multipart/form-data')) {
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
    if (url.pathname.endsWith('/Movies/m1')) return Response.json(existing);
    return Response.json([]);
  }) as typeof fetch;
  t.after(() => {
    globalThis.fetch = original;
  });
  return saved;
}

const png = () => new File([new Uint8Array(8)], 'poster.png', { type: 'image/png' });

function submit(path: string, fields: Record<string, string | File>) {
  const body = new FormData();
  for (const [name, value] of Object.entries(fields)) body.set(name, value);
  return router.fetch(new Request(new URL(path, 'http://localhost'), { method: 'POST', body }));
}

describe('movie image on add', () => {
  it('uploads a file as multipart and leaves the URL out', async (t) => {
    const saved = stubApi(t);
    const response = await submit('/movies/add', {
      title: 'Alien',
      imageUrl: 'https://example.com/ignored.jpg',
      coverImage: png(),
    });

    assert.equal(response.status, 303);
    assert.equal(saved[0]?.contentType, 'multipart');
    assert.equal(saved[0]?.entity?.imageUrl, undefined);
    assert.equal(saved[0]?.file?.name, 'poster.png');
  });

  it('answers 400 for an image URL that is not http or https', async (t) => {
    const saved = stubApi(t);
    const response = await submit('/movies/add', { title: 'Alien', imageUrl: 'ftp://example.com/p.jpg' });

    assert.equal(response.status, 400);
    assert.match(await response.text(), /id="imageUrl-error"[^>]*>Enter a valid http or https image URL/);
    assert.equal(saved.length, 0);
  });
});

describe('movie image and TV series on edit', () => {
  it('shows the current image, a remove option and the TV series checkbox checked', async (t) => {
    stubApi(t);
    const html = await (await router.fetch(new URL('/movies/m1/edit', 'http://localhost'))).text();

    assert.match(html, /movies\/m1\.jpg/);
    assert.match(html, /name="removeImage"/);
    assert.match(html, /name="isTvSeries"[^>]*checked/);
  });

  it('unchecking Is TV Series saves false', async (t) => {
    const saved = stubApi(t);
    const response = await submit('/movies/m1/edit', { title: 'Alien' });

    assert.equal(response.status, 303);
    assert.equal(saved[0]?.json?.isTvSeries, false);
  });

  it('replaces the image with an uploaded file, keeping the existing fields', async (t) => {
    const saved = stubApi(t);
    const response = await submit('/movies/m1/edit', { title: 'Alien', coverImage: png() });

    assert.equal(response.status, 303);
    assert.equal(saved[0]?.contentType, 'multipart');
    assert.equal(saved[0]?.entity?.id, 'm1');
    assert.equal(saved[0]?.entity?.imageUrl, undefined);
    assert.equal(saved[0]?.file?.name, 'poster.png');
  });

  it('drops the saved image and its URL when it is removed', async (t) => {
    const saved = stubApi(t);
    const response = await submit('/movies/m1/edit', {
      title: 'Alien',
      imageUrl: 'https://example.com/alien.jpg',
      removeImage: 'on',
    });

    assert.equal(response.status, 303);
    assert.equal(saved[0]?.contentType, 'json');
    assert.equal(saved[0]?.json?.coverImage, undefined);
    assert.equal(saved[0]?.json?.imageUrl, undefined);
  });
});
