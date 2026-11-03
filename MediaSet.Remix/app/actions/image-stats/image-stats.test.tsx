import * as assert from 'remix/assert';
import { describe, it } from 'remix/test';

import { router } from '../../router.ts';

const stats = {
  totalFiles: 12,
  totalSizeBytes: 2048,
  filesByEntityType: { books: 8, movies: 4 },
  sizeByEntityType: { books: 1024, movies: 1024 },
  brokenLinks: [{ entityId: 'b1', entityType: 'books', title: 'Dune', missingFilePath: 'books/b1.jpg' }],
  orphanedFiles: [{ relativePath: 'movies/old.jpg', sizeBytes: 1536 }],
  imageLookupFailures: [
    {
      entityId: 'm1',
      entityType: 'movies',
      title: 'Alien',
      lookupAttemptedAt: '2026-01-01T00:00:00Z',
      permanentFailure: true,
    },
  ],
  lastUpdated: '2026-01-02T00:00:00Z',
};

interface Call {
  method: string;
  pathname: string;
  body: string | null;
}

// Stubs the API; `respond` answers each call. Records every request.
function stubApi(t: { after: (fn: () => void) => void }, respond: (call: Call) => Response) {
  const original = globalThis.fetch;
  const calls: Call[] = [];
  globalThis.fetch = (async (input: RequestInfo | URL, init?: RequestInit) => {
    const request = new Request(input instanceof Request ? input : String(input), init);
    const call = {
      method: request.method,
      pathname: new URL(request.url).pathname,
      body: init?.body ? String(init.body) : null,
    };
    calls.push(call);
    return respond(call);
  }) as typeof fetch;
  t.after(() => {
    globalThis.fetch = original;
  });
  return calls;
}

const get = () => router.fetch(new URL('/image-stats', 'http://localhost'));
const post = (fields: Record<string, string>) =>
  router.fetch(
    new Request(new URL('/image-stats', 'http://localhost'), { method: 'POST', body: new URLSearchParams(fields) })
  );

describe('image statistics page', () => {
  it('renders the stat cards, tabs and tables from the stats', async (t) => {
    stubApi(t, () => Response.json(stats));
    const response = await get();
    const html = await response.text();

    assert.equal(response.status, 200);
    assert.match(html, /<title>Image Statistics \| MediaSet<\/title>/);
    for (const title of ['Total Files', 'Total Size', 'Orphaned Files', 'Broken Links', 'Lookup Failures']) {
      assert.match(html, new RegExp(title));
    }
    assert.match(html, /2\.0 KB/);
    assert.match(html, /bg-yellow-500\/10/);
    assert.match(html, /bg-red-500\/10/);
    assert.match(html, /bg-orange-500\/10/);
    assert.match(html, /id="image-stats-tab-books"/);
    assert.match(html, /id="image-stats-tab-movies"/);
    assert.doesNotMatch(html, /id="image-stats-tab-games"/);
    assert.match(html, /href="\/books\/b1"/);
    assert.match(html, /books\/b1\.jpg/);
    assert.match(html, /movies\/old\.jpg/);
    assert.match(html, /1\.5 KB/);
    assert.match(html, /name="intent" value="delete-orphaned"/);
    assert.match(html, /Reset All/);
  });

  it('shows no alert colours and a no-issues message when nothing is wrong', async (t) => {
    stubApi(t, () => Response.json({ ...stats, brokenLinks: [], orphanedFiles: [], imageLookupFailures: [] }));
    const html = await (await get()).text();

    assert.doesNotMatch(html, /bg-yellow-500\/10/);
    assert.match(html, /No image issues for this entity type\./);
  });

  it('shows the empty state when no statistics exist yet', async (t) => {
    stubApi(t, () => new Response(null, { status: 204 }));
    const response = await get();

    assert.equal(response.status, 200);
    assert.match(await response.text(), /No image statistics available yet/);
  });

  it('shows a friendly error page when the API fails', async (t) => {
    stubApi(t, () => new Response('boom', { status: 500 }));
    const response = await get();

    assert.equal(response.status, 500);
    assert.match(await response.text(), /Couldn&#39;t load image statistics|Couldn't load image statistics/);
  });
});

describe('image statistics actions', () => {
  it('deletes orphaned images and redirects back', async (t) => {
    const calls = stubApi(t, () => Response.json({ deleted: 1 }));
    const response = await post({ intent: 'delete-orphaned' });

    assert.equal(response.status, 303);
    assert.equal(response.headers.get('Location'), '/image-stats');
    assert.deepEqual(
      calls.map((call) => `${call.method} ${call.pathname}`),
      ['DELETE /images/orphaned']
    );
  });

  it('keeps the selected tab in the redirect', async (t) => {
    stubApi(t, () => Response.json({ deleted: 1 }));
    const response = await post({ intent: 'delete-orphaned', tab: 'movies' });

    assert.equal(response.headers.get('Location'), '/image-stats?tab=movies');
  });

  it('rejects a missing or unknown intent with a 400 and calls nothing', async (t) => {
    const calls = stubApi(t, () => Response.json(stats));
    for (const fields of [{} as Record<string, string>, { intent: 'nope' }]) {
      const response = await post(fields);
      assert.equal(response.status, 400);
      assert.match(await response.text(), /Unknown action\./);
    }
    assert.equal(calls.filter((call) => call.method === 'DELETE').length, 0);
  });

  it('shows the requested tab first', async (t) => {
    stubApi(t, () => Response.json(stats));
    const html = await (await router.fetch(new URL('/image-stats?tab=movies', 'http://localhost'))).text();

    assert.match(html, /id="image-stats-tab-movies"[^>]*aria-pressed="true"/);
    assert.match(html, /<time datetime="2026-01-01T00:00:00Z"/);
  });

  it('resets the lookup for the submitted ids and redirects back', async (t) => {
    const calls = stubApi(t, () => Response.json({ reset: 2 }));
    const response = await post({ intent: 'reset-lookup', entityType: 'movies', entityIds: '["m1","m2"]' });

    assert.equal(response.status, 303);
    assert.equal(calls[0].method, 'DELETE');
    assert.equal(calls[0].pathname, '/images/lookup/Movies');
    assert.deepEqual(JSON.parse(calls[0].body ?? ''), { entityIds: ['m1', 'm2'] });
  });

  for (const entityIds of ['not json', '{}', '[1]']) {
    it(`rejects invalid entity ids ${JSON.stringify(entityIds)} without resetting`, async (t) => {
      const calls = stubApi(t, () => Response.json(stats));
      const response = await post({ intent: 'reset-lookup', entityType: 'books', entityIds });

      assert.equal(response.status, 400);
      assert.match(await response.text(), /Invalid entity IDs format\./);
      assert.equal(calls.filter((call) => call.method === 'DELETE').length, 0);
    });
  }

  it('rejects an unknown entity type without resetting', async (t) => {
    const calls = stubApi(t, () => Response.json({ reset: 0 }));
    const response = await post({ intent: 'reset-lookup', entityType: 'nope', entityIds: '["x"]' });

    assert.equal(response.status, 404);
    assert.equal(calls.length, 0);
  });

  it('shows an error page when the API call fails', async (t) => {
    stubApi(t, () => new Response('boom', { status: 500 }));
    const response = await post({ intent: 'delete-orphaned' });

    assert.equal(response.status, 500);
  });
});
